package nakama

import (
	"context"
	"database/sql"
	"encoding/json"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
	"google.golang.org/protobuf/proto"
	"tienlen/internal/app"
	"tienlen/internal/domain"
	pb "tienlen/proto"
)

const (
	MatchLabelKey_OpenSeats = "open" // Key for the open seats in the match label
)

// MatchState holds the authoritative runtime state for the Nakama match handler.
type MatchState struct {
	Seats        [4]string                   `json:"seats"`          // Array of user IDs, empty string means seat is empty
	OwnerID      string                      `json:"owner_id"`       // User ID of the match owner
	LastWinnerID string                      `json:"last_winner_id"` // User ID of the winner of the last game
	Tick         int64                       `json:"tick"`           // Current tick of the match for turn-based logic
	Presences    map[string]runtime.Presence `json:"-"`              // Map UserId -> Presence for targeted messaging
	App          *app.Service                `json:"-"`              // TienLen app service with game logic
	Game         *domain.Game                `json:"-"`              // Current active game state (nil if in lobby)
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

// NewMatch is the factory function registered with Nakama.
func NewMatch(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule) (runtime.Match, error) {
	return &matchHandler{}, nil
}

type matchHandler struct{}

// MatchInit is called when the match is created.
func (mh *matchHandler) MatchInit(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, params map[string]interface{}) (interface{}, int, string) {
	logger.Debug("MatchInit: Initializing match handler.")

	state := &MatchState{
		Tick:      time.Now().Unix(),
		Presences: make(map[string]runtime.Presence),
		App:       app.NewService(nil), // Initialize the app service
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

	if matchState.GetOpenSeatsCount() <= 0 {
		return state, false, "Match full"
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

		// Assign seat
		assigned := false
		for i, seatUserId := range matchState.Seats {
			if seatUserId == "" {
				matchState.Seats[i] = p.GetUserId()
				assigned = true
				break
			}
		}

		if !assigned {
			logger.Warn("MatchJoin: User %s joined but no seat was empty.", p.GetUserId())
			continue
		}

		// Assign owner if none exists
		if matchState.OwnerID == "" {
			matchState.OwnerID = p.GetUserId()
		}
	}

	// Update match label
	mh.updateLabel(matchState, dispatcher, logger)

	// Build PlayerState list for snapshot
	var playerStates []*pb.PlayerState
	for i, userId := range matchState.Seats {
		if userId == "" {
			continue
		}

		p, exists := matchState.Presences[userId]
		displayName := ""
		if exists {
			displayName = p.GetUsername()
		}

		playerStates = append(playerStates, &pb.PlayerState{
			UserId:         userId,
			Seat:           int32(i + 1), // 1-based seat
			IsOwner:        userId == matchState.OwnerID,
			CardsRemaining: 0, // Lobby state
			DisplayName:    displayName,
			AvatarIndex:    0, // Default for now
		})
	}

	snapshot := &pb.MatchStateSnapshot{
		Seats:   matchState.Seats[:],
		OwnerId: matchState.OwnerID,
		Tick:    matchState.Tick,
		Players: playerStates,
	}
	snapshotPayload, err := proto.Marshal(snapshot)
	if err != nil {
		logger.Error("MatchJoin: Failed to marshal match state snapshot: %v", err)
		return matchState
	}

	// Broadcast the current match state to all presences after join.
	if err := dispatcher.BroadcastMessage(int64(pb.OpCode_OP_CODE_PLAYER_JOINED), snapshotPayload, nil, nil, true); err != nil {
		logger.Error("MatchJoin: Failed to broadcast player joined snapshot: %v", err)
	}

	return matchState
}

// MatchLeave is called when one or more players leave the match.
func (mh *matchHandler) MatchLeave(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	matchState, ok := state.(*MatchState)
	if !ok {
		logger.Error("MatchLeave: state not found")
		return state
	}

	for _, p := range presences {
		delete(matchState.Presences, p.GetUserId())

		for i, seatUserId := range matchState.Seats {
			if seatUserId == p.GetUserId() {
				matchState.Seats[i] = ""
				logger.Debug("MatchLeave: User %s left, seat %d freed.", p.GetUserId(), i)

				if matchState.OwnerID == p.GetUserId() {
					matchState.OwnerID = ""
					// Assign new owner
					for _, newOwnerId := range matchState.Seats {
						if newOwnerId != "" {
							matchState.OwnerID = newOwnerId
							logger.Debug("MatchLeave: Owner %s left, new owner is %s.", p.GetUserId(), newOwnerId)
							break
						}
					}
				}
				break
			}
		}
	}

	mh.updateLabel(matchState, dispatcher, logger)

	return matchState
}

func (mh *matchHandler) MatchLoop(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, messages []runtime.MatchData) interface{} {
	matchState, ok := state.(*MatchState)
	if !ok {
		return state
	}

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

	return matchState
}

func (mh *matchHandler) handleStartGame(state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, msg runtime.MatchData) {
	senderID := msg.GetUserId()
	if senderID != state.OwnerID {
		logger.Warn("StartGame: User %s tried to start game but is not owner (%s)", senderID, state.OwnerID)
		return
	}

	activeCount := state.GetOccupiedSeatCount()
	if activeCount < 2 {
		logger.Warn("StartGame: Cannot start with %d players. Need at least 2.", activeCount)
		return
	}

	// Initialize the domain Game via the Service
	game, events, err := state.App.StartGame(state.Seats[:])
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
	events, err := state.App.PlayCards(state.Game, senderID, domainCards)
	if err != nil {
		logger.Warn("handlePlayCards: User %s failed to play cards: %v", senderID, err)
		return
	}

	// Broadcast events
	for _, ev := range events {
		mh.broadcastEvent(state, dispatcher, logger, ev)
	}
}

func (mh *matchHandler) handlePassTurn(state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, msg runtime.MatchData) {
	senderID := msg.GetUserId()
	if state.Game == nil {
		logger.Warn("handlePassTurn: Game not started.")
		return
	}

	// Call app service
	events, err := state.App.PassTurn(state.Game, senderID)
	if err != nil {
		logger.Warn("handlePassTurn: User %s failed to pass turn: %v", senderID, err)
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
		payload = &pb.GameStartedEvent{
			Phase:           pb.GamePhase_PHASE_PLAYING,
			FirstTurnUserId: p.FirstTurnUserID,
		}
	case app.EventHandDealt:
		opCode = int64(pb.OpCode_OP_CODE_HAND_DEALT)
		p := ev.Payload.(app.HandDealtPayload)
		payload = &pb.HandDealtEvent{
			Hand: toProtoCards(p.Hand),
		}
	case app.EventCardPlayed:
		opCode = int64(pb.OpCode_OP_CODE_CARD_PLAYED)
		p := ev.Payload.(app.CardPlayedPayload)
		payload = &pb.CardPlayedEvent{
			UserId:         p.UserID,
			Cards:          toProtoCards(p.Cards),
			NextTurnUserId: p.NextTurnUserID,
		}
	case app.EventTurnPassed:
		opCode = int64(pb.OpCode_OP_CODE_TURN_PASSED)
		p := ev.Payload.(app.TurnPassedPayload)
		payload = &pb.TurnPassedEvent{
			UserId:         p.UserID,
			NextTurnUserId: p.NextTurnUserID,
		}
	case app.EventGameEnded:
		opCode = int64(pb.OpCode_OP_CODE_GAME_ENDED)
		p := ev.Payload.(app.GameEndedPayload)
		payload = &pb.GameEndedEvent{
			FinishOrder: p.FinishOrder,
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
