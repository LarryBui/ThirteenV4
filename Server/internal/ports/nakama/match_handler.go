package nakama

import (
	"context"
	"database/sql"
	"encoding/json"
	"math/rand"
	"strconv"
	"strings"
	"time"

	"tienlen/internal/app"
	"tienlen/internal/bot"
	"tienlen/internal/domain"
	pb "tienlen/proto"

	"github.com/heroiclabs/nakama-common/runtime"
	"google.golang.org/protobuf/proto"
)

const (
	MatchLabelKey_OpenSeats = "open" // Key for the open seats in the match label
)

// MatchState holds the authoritative runtime state for the Nakama match handler.
type MatchState struct {
	Seats             [4]string                   `json:"seats"`           // Array of user IDs, empty string means seat is empty
	OwnerSeat         int                         `json:"owner_seat"`      // Seat index of the match owner
	LastWinnerSeat    int                         `json:"last_winner_seat"` // Seat index of the winner of the last game
	Tick              int64                       `json:"tick"`            // Current tick of the match for turn-based logic
	Presences         map[string]runtime.Presence `json:"-"`               // Map UserId -> Presence for targeted messaging
	App               *app.Service                `json:"-"`               // TienLen app service with game logic
	Game              *domain.Game                `json:"-"`               // Current active game state (nil if in lobby)
	BotsEnabled          bool                        `json:"bots_enabled"`           // Whether AI players are allowed
	BotMinDelay          int                         `json:"bot_min_delay"`          // Min seconds a bot waits
	BotMaxDelay          int                         `json:"bot_max_delay"`          // Max seconds a bot waits
	BotAutoFillDelay     int                         `json:"bot_auto_fill_delay"`    // Seconds to wait before auto-filling with bots
	BotWaitUntil         int64                       `json:"bot_wait_until"`         // Tick when the bot should act
	LastSinglePlayerTick int64                       `json:"last_single_player_tick"` // Tick when a single player started waiting
}

func (ms *MatchState) GetOpenSeatsCount() int {
	count := 0
	for _, seat := range ms.Seats {
		if seat == "" {
			count++
		}
	}
	return count
}

func (ms *MatchState) GetOccupiedSeatCount() int {
	count := 0
	for _, seat := range ms.Seats {
		if seat != "" {
			count++
		}
	}
	return count
}

func (ms *MatchState) GetHumanPlayerCount() int {
	count := 0
	for _, seat := range ms.Seats {
		if seat != "" && !isBotUserId(seat) {
			count++
		}
	}
	return count
}

// isBotUserId reports whether the given user id represents a bot seat.
func isBotUserId(userId string) bool {
	return strings.HasPrefix(userId, "bot:")
}

// isHumanSeat reports whether the seat index belongs to a human player.
func isHumanSeat(seats []string, seatIndex int) bool {
	if seatIndex < 0 || seatIndex >= len(seats) {
		return false
	}
	userId := seats[seatIndex]
	return userId != "" && !isBotUserId(userId)
}

// findFirstHumanSeat returns the first seat index with a human occupant or -1 if none exist.
func findFirstHumanSeat(seats []string) int {
	for i, userId := range seats {
		if userId != "" && !isBotUserId(userId) {
			return i
		}
	}
	return -1
}

// shouldTerminateAllBots returns true when there are no humans and at least one bot seat.
func shouldTerminateAllBots(seats []string) bool {
	if findFirstHumanSeat(seats) != -1 {
		return false
	}
	for _, userId := range seats {
		if isBotUserId(userId) {
			return true
		}
	}
	return false
}

// NewMatch is the factory function registered with Nakama.
func NewMatch(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule) (runtime.Match, error) {
	return &matchHandler{}, nil
}

type matchHandler struct{}

// MatchInit is called when the match is created.
func (mh *matchHandler) MatchInit(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, params map[string]interface{}) (interface{}, int, string) {
	logger.Debug("MatchInit: Initializing match handler.")

	state := &MatchState{
		Tick:           time.Now().Unix(),
		Presences:      make(map[string]runtime.Presence),
		App:            app.NewService(nil), // Initialize the app service
		OwnerSeat:      -1,
		LastWinnerSeat: -1,
	}

	// Read environment variables for bot configuration
	env := ctx.Value(runtime.RUNTIME_CTX_ENV).(map[string]string)
	if val, ok := env["tienlen_bots_enabled"]; ok {
		state.BotsEnabled = val == "true"
	}
	if val, ok := env["tienlen_bot_min_delay_sec"]; ok {
		if i, err := strconv.Atoi(val); err == nil {
			state.BotMinDelay = i
		}
	}
	if val, ok := env["tienlen_bot_max_delay_sec"]; ok {
		if i, err := strconv.Atoi(val); err == nil {
			state.BotMaxDelay = i
		}
	}
	if val, ok := env["tienlen_bot_auto_fill_delay_sec"]; ok {
		if i, err := strconv.Atoi(val); err == nil {
			state.BotAutoFillDelay = i
		}
	}

	// Defaults if not set
	if state.BotMinDelay == 0 {
		state.BotMinDelay = 1
	}
	if state.BotMaxDelay == 0 {
		state.BotMaxDelay = 3
	}
	if state.BotAutoFillDelay == 0 {
		state.BotAutoFillDelay = 5
	}

	// Initial match label: 4 open seats
	label := map[string]int{
		MatchLabelKey_OpenSeats: state.GetOpenSeatsCount(),
	}
	labelBytes, err := json.Marshal(label)
	if err != nil {
		logger.Error("MatchInit: Failed to marshal label: %v", err)
		return nil, 0, ""
	}

	tickRate := 1 // 1 tick per second (or whatever logic requires)
	return state, tickRate, string(labelBytes)
}

func (mh *matchHandler) MatchJoinAttempt(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presence runtime.Presence, metadata map[string]string) (interface{}, bool, string) {
	matchState, ok := state.(*MatchState)
	if !ok {
		return state, false, "state not found"
	}

	// Allow join if there is an empty seat OR a bot to replace (if game hasn't started)
	if matchState.GetOpenSeatsCount() <= 0 {
		hasBot := false
		if matchState.Game == nil {
			for _, seat := range matchState.Seats {
				if strings.HasPrefix(seat, "bot:") {
					hasBot = true
					break
				}
			}
		}
		if !hasBot {
			return state, false, "Match full"
		}
	}

	return state, true, ""
}

func (mh *matchHandler) MatchJoin(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	matchState, ok := state.(*MatchState)
	if !ok {
		logger.Error("MatchJoin: state not found")
		return state
	}

	for _, p := range presences {
		// Store presence
		matchState.Presences[p.GetUserId()] = p

		// Assign seat: Try empty seats first, then bots (if lobby)
		assigned := false
		for i, seatUserId := range matchState.Seats {
			if seatUserId == "" {
				matchState.Seats[i] = p.GetUserId()
				assigned = true
				break
			}
		}

		if !assigned && matchState.Game == nil {
			for i, seatUserId := range matchState.Seats {
				if strings.HasPrefix(seatUserId, "bot:") {
					logger.Info("MatchJoin: Replacing bot %s with human %s in seat %d", seatUserId, p.GetUserId(), i)
					matchState.Seats[i] = p.GetUserId()
					assigned = true
					break
				}
			}
		}

		if !assigned {
			logger.Warn("MatchJoin: User %s joined but no seat (empty or bot) was available.", p.GetUserId())
			continue
		}
	}

	// Ensure owner seat is assigned to a human player only.
	if !isHumanSeat(matchState.Seats[:], matchState.OwnerSeat) {
		matchState.OwnerSeat = findFirstHumanSeat(matchState.Seats[:])
		if matchState.OwnerSeat >= 0 {
			logger.Debug("MatchJoin: Owner set to human seat %d.", matchState.OwnerSeat)
		}
	}

	// Update match label
	mh.updateLabel(matchState, dispatcher, logger)

	// Broadcast the current match state to all presences after join.
	mh.broadcastMatchState(matchState, dispatcher, logger)

	return matchState
}

// MatchLeave is called when one or more players leave the match.
func (mh *matchHandler) MatchLeave(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	matchState, ok := state.(*MatchState)
	if !ok {
		logger.Error("MatchLeave: state not found")
		return state
	}

	ownerLeft := false
	for _, p := range presences {
		delete(matchState.Presences, p.GetUserId())

		for i, seatUserId := range matchState.Seats {
			if seatUserId == p.GetUserId() {
				matchState.Seats[i] = ""
				logger.Debug("MatchLeave: User %s left, seat %d freed.", p.GetUserId(), i)

				if matchState.OwnerSeat == i {
					ownerLeft = true
				}
				break
			}
		}
	}

	newOwnerSeat := findFirstHumanSeat(matchState.Seats[:])
	if newOwnerSeat != matchState.OwnerSeat {
		matchState.OwnerSeat = newOwnerSeat
		if newOwnerSeat >= 0 {
			logger.Debug("MatchLeave: Owner set to human seat %d.", newOwnerSeat)
		} else if ownerLeft {
			logger.Debug("MatchLeave: Owner left and no human owner is available.")
		}
	}

	if shouldTerminateAllBots(matchState.Seats[:]) {
		logger.Info("MatchLeave: Terminating match with bots only.")
		return nil
	}

	mh.updateLabel(matchState, dispatcher, logger)

	return matchState
}

func (mh *matchHandler) MatchLoop(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, messages []runtime.MatchData) interface{} {
	matchState, ok := state.(*MatchState)
	if !ok {
		return state
	}

	matchState.Tick = tick

	// Handle incoming messages
	for _, msg := range messages {
		switch msg.GetOpCode() {
		case int64(pb.OpCode_OP_CODE_START_GAME):
			mh.handleStartGame(matchState, dispatcher, logger, msg)
		case int64(pb.OpCode_OP_CODE_PLAY_CARDS):
			mh.handlePlayCards(matchState, dispatcher, logger, msg)
		case int64(pb.OpCode_OP_CODE_PASS_TURN):
			mh.handlePassTurn(matchState, dispatcher, logger, msg)
		default:
			logger.Warn("MatchLoop: Unknown opcode received: %d", msg.GetOpCode())
		}
	}

	// AI Logic
	if matchState.BotsEnabled {
		mh.processBots(matchState, dispatcher, logger)
	}

	return matchState
}

func (mh *matchHandler) processBots(state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger) {
	// 1. Auto-fill lobby with bots if there's only one human player after delay
	if state.Game == nil {
		humanCount := state.GetHumanPlayerCount()
		if humanCount == 1 {
			if state.LastSinglePlayerTick == 0 {
				state.LastSinglePlayerTick = state.Tick
				logger.Debug("processBots: Single player detected, starting auto-fill timer.")
			}

			if state.Tick-state.LastSinglePlayerTick >= int64(state.BotAutoFillDelay) {
				added := false
				for i, seat := range state.Seats {
					if seat == "" {
						botID := "bot:" + strconv.Itoa(i)
						state.Seats[i] = botID
						logger.Info("processBots: Added bot %s to seat %d", botID, i)
						added = true
					}
				}
				if added {
					mh.updateLabel(state, dispatcher, logger)
					mh.broadcastMatchState(state, dispatcher, logger)
				}
				// Reset timer so it doesn't keep "adding" every tick (though seats are full now)
				state.LastSinglePlayerTick = 0
			}
		} else {
			// Reset timer if 0 or >1 humans
			state.LastSinglePlayerTick = 0
		}
	}

	// 2. Handle bot turns in-game
	if state.Game != nil && state.Game.Phase == domain.PhasePlaying {
		currentTurn := state.Game.CurrentTurn
		currentUserID := state.Seats[currentTurn]

		if strings.HasPrefix(currentUserID, "bot:") {
			if state.BotWaitUntil == 0 {
				// Initialize random delay
				delay := rand.Intn(state.BotMaxDelay-state.BotMinDelay+1) + state.BotMinDelay
				state.BotWaitUntil = state.Tick + int64(delay)
				logger.Debug("processBots: Bot %s (seat %d) will act at tick %d (current %d)", currentUserID, currentTurn, state.BotWaitUntil, state.Tick)
			}

			if state.Tick >= state.BotWaitUntil {
				state.BotWaitUntil = 0 // Reset for next turn
				move, err := bot.CalculateMove(state.Game, currentTurn)
				if err != nil {
					logger.Error("processBots: Bot %s failed to calculate move: %v", currentUserID, err)
					return
				}

				if move.Pass {
					events, err := state.App.PassTurn(state.Game, currentTurn)
					if err == nil {
						for _, ev := range events {
							mh.broadcastEvent(state, dispatcher, logger, ev)
						}
					}
				} else {
					events, err := state.App.PlayCards(state.Game, currentTurn, move.Cards)
					if err == nil {
						for _, ev := range events {
							mh.broadcastEvent(state, dispatcher, logger, ev)
						}
					}
				}
			}
		} else {
			// Not a bot turn, reset wait if it was set
			state.BotWaitUntil = 0
		}
	}
}

func (mh *matchHandler) broadcastMatchState(state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger) {
	var playerStates []*pb.PlayerState
	for i, userId := range state.Seats {
		if userId == "" {
			continue
		}

		displayName := userId
		if p, exists := state.Presences[userId]; exists {
			displayName = p.GetUsername()
		} else if strings.HasPrefix(userId, "bot:") {
			displayName = "AI Player " + userId[4:]
		}

		cardsRemaining := 0
		if state.Game != nil {
			for _, p := range state.Game.Players {
				if p.Seat-1 == i {
					cardsRemaining = len(p.Hand)
					break
				}
			}
		}

		playerStates = append(playerStates, &pb.PlayerState{
			UserId:         userId,
			Seat:           int32(i),
			IsOwner:        i == state.OwnerSeat,
			CardsRemaining: int32(cardsRemaining),
			DisplayName:    displayName,
			AvatarIndex:    0,
		})
	}

	snapshot := &pb.MatchStateSnapshot{
		Seats:     state.Seats[:],
		OwnerSeat: int32(state.OwnerSeat),
		Tick:      state.Tick,
		Players:   playerStates,
	}
	bytes, _ := proto.Marshal(snapshot)
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_CODE_PLAYER_JOINED), bytes, nil, nil, true)
}

func (mh *matchHandler) handleStartGame(state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, msg runtime.MatchData) {
	senderID := msg.GetUserId()
	senderSeat := -1
	for i, seatUserId := range state.Seats {
		if seatUserId == senderID {
			senderSeat = i
			break
		}
	}

	logger.Info("StartGame: Request received from %s (seat=%d, owner_seat=%d, occupied=%d)", senderID, senderSeat, state.OwnerSeat, state.GetOccupiedSeatCount())

	// Validate request payload even if it is currently empty; helps detect client/proto mismatches early.
	request := &pb.StartGameRequest{}
	if err := proto.Unmarshal(msg.GetData(), request); err != nil {
		logger.Warn("StartGame: Invalid StartGameRequest from %s: %v", senderID, err)
		return
	}

	if senderSeat != state.OwnerSeat {
		logger.Warn("StartGame: User %s tried to start game but is not owner (owner_seat=%d)", senderID, state.OwnerSeat)
		return
	}

	activeCount := state.GetOccupiedSeatCount()
	if activeCount < app.MinPlayersToStartGame {
		logger.Warn("StartGame: Cannot start with %d players. Need at least %d.", activeCount, app.MinPlayersToStartGame)
		return
	}

	// Initialize the domain Game via the Service
	game, events, err := state.App.StartGame(state.Seats[:], state.LastWinnerSeat)
	if err != nil {
		logger.Error("StartGame: Failed to start game: %v", err)
		return
	}

	// Store the authoritative game state
	state.Game = game

	// Broadcast resulting events
	for _, ev := range events {
		mh.broadcastEvent(state, dispatcher, logger, ev)
	}

	logger.Info("StartGame: Game started with %d players.", activeCount)
}

func (mh *matchHandler) handlePlayCards(state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, msg runtime.MatchData) {
	senderID := msg.GetUserId()
	senderSeat := -1
	for i, seatUserId := range state.Seats {
		if seatUserId == senderID {
			senderSeat = i
			break
		}
	}

	if state.Game == nil {
		logger.Warn("handlePlayCards: Game not started.")
		return
	}

	// Unmarshal client request
	request := &pb.PlayCardsRequest{}
	if err := proto.Unmarshal(msg.GetData(), request); err != nil {
		logger.Error("handlePlayCards: Failed to unmarshal PlayCardsRequest: %v", err)
		return
	}

	domainCards := make([]domain.Card, len(request.GetCards()))
	for i, card := range request.GetCards() {
		domainCards[i] = domain.Card{
			Suit: int32(card.GetSuit()),
			Rank: int32(card.GetRank()),
		}
	}

	// Call app service
	events, err := state.App.PlayCards(state.Game, senderSeat, domainCards)
	if err != nil {
		logger.Warn("handlePlayCards: User %s (seat %d) failed to play cards: %v", senderID, senderSeat, err)
		return
	}

	// Broadcast events
	for _, ev := range events {
		mh.broadcastEvent(state, dispatcher, logger, ev)
	}
}

func (mh *matchHandler) handlePassTurn(state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, msg runtime.MatchData) {
	senderID := msg.GetUserId()
	senderSeat := -1
	for i, seatUserId := range state.Seats {
		if seatUserId == senderID {
			senderSeat = i
			break
		}
	}

	if state.Game == nil {
		logger.Warn("handlePassTurn: Game not started.")
		return
	}

	// Call app service
	events, err := state.App.PassTurn(state.Game, senderSeat)
	if err != nil {
		logger.Warn("handlePassTurn: User %s (seat %d) failed to pass turn: %v", senderID, senderSeat, err)
		return
	}

	// Broadcast events
	for _, ev := range events {
		mh.broadcastEvent(state, dispatcher, logger, ev)
	}
}

// broadcastEvent handles the conversion and dispatching of app events to Nakama.
func (mh *matchHandler) broadcastEvent(state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, ev app.Event) {
	var opCode int64
	var payload proto.Message

	switch ev.Kind {
	case app.EventGameStarted:
		opCode = int64(pb.OpCode_OP_CODE_GAME_STARTED)
		p := ev.Payload.(app.GameStartedPayload)
		logger.Debug("Event: game_started (firstTurnSeat=%d, handCount=%d, recipients=%d)", p.FirstTurnSeat, len(p.Hand), len(ev.Recipients))
		payload = &pb.GameStartedEvent{
			Phase:         pb.GamePhase_PHASE_PLAYING,
			FirstTurnSeat: int32(p.FirstTurnSeat),
			Hand:          toProtoCards(p.Hand),
		}
	case app.EventCardPlayed:
		opCode = int64(pb.OpCode_OP_CODE_CARD_PLAYED)
		p := ev.Payload.(app.CardPlayedPayload)
		payload = &pb.CardPlayedEvent{
			Seat:         int32(p.Seat),
			Cards:        toProtoCards(p.Cards),
			NextTurnSeat: int32(p.NextTurnSeat),
			NewRound:     p.NewRound,
		}
	case app.EventTurnPassed:
		opCode = int64(pb.OpCode_OP_CODE_TURN_PASSED)
		p := ev.Payload.(app.TurnPassedPayload)
		payload = &pb.TurnPassedEvent{
			Seat:         int32(p.Seat),
			NextTurnSeat: int32(p.NextTurnSeat),
			NewRound:     p.NewRound,
		}
	case app.EventGameEnded:
		opCode = int64(pb.OpCode_OP_CODE_GAME_ENDED)
		p := ev.Payload.(app.GameEndedPayload)
		protoSeats := make([]int32, len(p.FinishOrderSeats))
		for i, seat := range p.FinishOrderSeats {
			protoSeats[i] = int32(seat)
		}
		payload = &pb.GameEndedEvent{
			FinishOrderSeats: protoSeats,
		}
		// Save the winner for the next game
		if len(p.FinishOrderSeats) > 0 {
			state.LastWinnerSeat = p.FinishOrderSeats[0]
		}
	default:
		logger.Warn("Unknown event kind: %v", ev.Kind)
		return
	}

	bytes, err := proto.Marshal(payload)
	if err != nil {
		logger.Error("Failed to marshal event %v: %v", ev.Kind, err)
		return
	}

	// Determine recipients (default to broadcast)
	var recipients []runtime.Presence
	if len(ev.Recipients) > 0 {
		for _, uid := range ev.Recipients {
			if p, ok := state.Presences[uid]; ok {
				recipients = append(recipients, p)
			}
		}
	}

	dispatcher.BroadcastMessage(opCode, bytes, recipients, nil, true)
}

func toProtoCards(domainCards []domain.Card) []*pb.Card {
	protoCards := make([]*pb.Card, len(domainCards))
	for i, card := range domainCards {
		protoCards[i] = toProtoCard(card)
	}
	return protoCards
}

// toProtoCard maps a domain card to the protobuf card representation.
func toProtoCard(card domain.Card) *pb.Card {
	return &pb.Card{
		Suit: pb.Suit(card.Suit),
		Rank: pb.Rank(card.Rank),
	}
}

func (mh *matchHandler) updateLabel(state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger) {
	label := map[string]int{
		MatchLabelKey_OpenSeats: state.GetOpenSeatsCount(),
	}
	labelBytes, err := json.Marshal(label)
	if err != nil {
		logger.Error("UpdateLabel: Failed to marshal: %v", err)
		return
	}
	if err := dispatcher.MatchLabelUpdate(string(labelBytes)); err != nil {
		logger.Error("UpdateLabel: Failed to update: %v", err)
	}
}

func (mh *matchHandler) MatchTerminate(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, reason int) interface{} {
	logger.Debug("MatchTerminate: Match terminated for reason %d", reason)
	return state
}

func (mh *matchHandler) MatchSignal(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, data string) (interface{}, string) {
	return state, ""
}
