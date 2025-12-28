package nakama

import (
	"context"
	"database/sql"
	"encoding/json"
	"math/rand"
	"strconv"
	"time"

	"tienlen/internal/app"
	"tienlen/internal/bot"
	"tienlen/internal/config"
	"tienlen/internal/domain"
	"tienlen/internal/ports"
	pb "tienlen/proto"

	"github.com/heroiclabs/nakama-common/runtime"
	"google.golang.org/protobuf/encoding/protojson"
	"google.golang.org/protobuf/proto"
)

const (
	MatchLabelKey_OpenSeats        = "open" // Key for the open seats in the match label
	gameStartTurnTimerBonusSeconds = 5      // Extra seconds added to the first turn timer to cover card dealing.
	lobbyAutoFillBotMax            = 2      // Max bots to auto-fill when a single human is waiting.
)

// MatchState holds the authoritative runtime state for the Nakama match handler.
type MatchState struct {
	Seats                [4]string                   `json:"seats"`                   // Array of user IDs, empty string means seat is empty
	OwnerSeat            int                         `json:"owner_seat"`              // Seat index of the match owner
	LastWinnerSeat       int                         `json:"last_winner_seat"`        // Seat index of the winner of the last game
	Tick                 int64                       `json:"tick"`                    // Current tick of the match for turn-based logic
	Presences            map[string]runtime.Presence `json:"-"`                       // Map UserId -> Presence for targeted messaging
	App                  *app.Service                `json:"-"`                       // TienLen app service with game logic
	Game                 *domain.Game                `json:"-"`                       // Current active game state (nil if in lobby)
	BotsEnabled          bool                        `json:"bots_enabled"`            // Whether AI players are allowed
	BotMinDelay          int                         `json:"bot_min_delay"`           // Min seconds a bot waits
	BotMaxDelay          int                         `json:"bot_max_delay"`           // Max seconds a bot waits
	BotAutoFillDelay     int                         `json:"bot_auto_fill_delay"`     // Seconds to wait before auto-filling with bots
	BotWaitUntil         int64                       `json:"bot_wait_until"`          // Tick when the bot should act
	LastSinglePlayerTick int64                       `json:"last_single_player_tick"` // Tick when a single player started waiting
	TurnSecondsRemaining int64                       `json:"turn_seconds_remaining"`  // Seconds remaining before the current turn expires
	Bots                 map[string]*bot.Agent       `json:"-"`                       // Active bot agents
	Economy              ports.EconomyPort           `json:"-"`                       // Interface to Nakama wallet
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
	return bot.IsBot(userId)
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

// shouldTerminateNoHumans returns true when there are no humans in the match.
func shouldTerminateNoHumans(seats []string) bool {
	return findFirstHumanSeat(seats) == -1
}

// NewMatch is the factory function registered with Nakama.
func NewMatch(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule) (runtime.Match, error) {
	return &matchHandler{}, nil
}

type matchHandler struct{}

// MatchInit is called when the match is created.
func (mh *matchHandler) MatchInit(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, params map[string]interface{}) (interface{}, int, string) {
	logger.Info("MatchInit: Initializing match handler.")

	// Load game configuration
	if err := config.LoadGameConfig("data/game_config.json"); err != nil {
		logger.Warn("MatchInit: Could not load game config: %v", err)
	}

	state := &MatchState{
		Tick:           time.Now().Unix(),
		Presences:      make(map[string]runtime.Presence),
		App:            app.NewService(nil), // Initialize the app service
		OwnerSeat:      -1,
		LastWinnerSeat: -1,
		Bots:           make(map[string]*bot.Agent),
		Economy:        NewNakamaEconomyAdapter(nk),
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
	defaultAutoFillDelay := 5
	if cfg := config.GetGameConfig(); cfg != nil && cfg.BotAutoFillDelaySeconds > 0 {
		defaultAutoFillDelay = cfg.BotAutoFillDelaySeconds
	}
	if state.BotAutoFillDelay == 0 {
		state.BotAutoFillDelay = defaultAutoFillDelay
	}

	// Initial match label: 4 open seats, lobby state
	label := &pb.MatchLabel{
		Open:  int32(state.GetOpenSeatsCount()),
		State: "lobby",
	}
	labelBytes, err := (&protojson.MarshalOptions{EmitUnpopulated: true}).Marshal(label)
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
				if isBotUserId(seat) {
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

	logger.Info("MatchJoin: %d users joining match.", len(presences))

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
				if isBotUserId(seatUserId) {
					logger.Info("MatchJoin: Replacing bot %s with human %s in seat %d", seatUserId, p.GetUserId(), i)
					delete(matchState.Bots, seatUserId) // Cleanup AI
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
	mh.broadcastMatchState(ctx, matchState, dispatcher, logger)

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

	if shouldTerminateNoHumans(matchState.Seats[:]) {
		logger.Info("MatchLeave: Terminating match with no humans.")
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
		logger.Info("MatchLoop: Received OpCode %d from %s", msg.GetOpCode(), msg.GetUserId())
		switch msg.GetOpCode() {
		case int64(pb.OpCode_OP_CODE_START_GAME):
			mh.handleStartGame(ctx, matchState, dispatcher, logger, msg)
		case int64(pb.OpCode_OP_CODE_PLAY_CARDS):
			mh.handlePlayCards(ctx, matchState, dispatcher, logger, msg)
		case int64(pb.OpCode_OP_CODE_PASS_TURN):
			mh.handlePassTurn(ctx, matchState, dispatcher, logger, msg)
		case int64(pb.OpCode_OP_CODE_IN_GAME_CHAT):
			mh.handleInGameChat(ctx, matchState, dispatcher, logger, msg)
		default:
			logger.Warn("MatchLoop: Unknown opcode received: %d", msg.GetOpCode())
		}
	}

	// Turn Timer Check
	if matchState.Game != nil && matchState.Game.Phase == domain.PhasePlaying {
		if matchState.TurnSecondsRemaining > 0 {
			matchState.TurnSecondsRemaining--
		}

		if matchState.TurnSecondsRemaining <= 0 {
			currentTurn := matchState.Game.CurrentTurn
			logger.Info("MatchLoop: Turn timer expired for seat %d. Processing timeout.", currentTurn)

			// Call App Service to handle the timeout logic (Play Smallest vs Pass)
			events, err := matchState.App.TimeoutTurn(matchState.Game, currentTurn)
			if err != nil {
				logger.Error("MatchLoop: Failed to process timeout for seat %d: %v", currentTurn, err)
			} else {
				mh.resetTurnSecondsRemaining(matchState, logger)
				for _, ev := range events {
					mh.broadcastEvent(ctx, matchState, dispatcher, logger, ev)
				}
			}
		}
	}

	// AI Logic
	if matchState.BotsEnabled {
		mh.processBots(ctx, matchState, dispatcher, logger, nk)
	}

	return matchState
}

func (mh *matchHandler) resetTurnSecondsRemaining(state *MatchState, logger runtime.Logger) {
	mh.resetTurnSecondsRemainingWithBonus(state, logger, 0)
}

// resetTurnSecondsRemainingWithBonus resets the turn timer and applies a bonus to the current turn.
func (mh *matchHandler) resetTurnSecondsRemainingWithBonus(state *MatchState, logger runtime.Logger, bonusSeconds int) {
	if state.Game == nil || state.Game.Phase != domain.PhasePlaying {
		state.TurnSecondsRemaining = 0
		return
	}

	duration := 16 // Default
	if cfg := config.GetGameConfig(); cfg != nil {
		duration = cfg.TurnDurationSeconds
	}

	state.TurnSecondsRemaining = int64(duration + bonusSeconds)
	logger.Debug(
		"Turn timer reset: Seat %d has %d seconds remaining",
		state.Game.CurrentTurn,
		state.TurnSecondsRemaining)
}

func (mh *matchHandler) processBots(ctx context.Context, state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, nk runtime.NakamaModule) {
	// 1. Auto-fill lobby with up to two bots if there's exactly one human player alone after delay
	if state.Game == nil {
		humanCount := state.GetHumanPlayerCount()
		occupiedCount := state.GetOccupiedSeatCount()

		if humanCount == 1 && occupiedCount == 1 {
			if state.LastSinglePlayerTick == 0 {
				state.LastSinglePlayerTick = state.Tick
				logger.Debug("processBots: Single player detected, starting auto-fill timer.")
			}

			if state.Tick-state.LastSinglePlayerTick >= int64(state.BotAutoFillDelay) {
				openSeats := state.GetOpenSeatsCount()
				botsToAdd := lobbyAutoFillBotMax
				if openSeats < botsToAdd {
					botsToAdd = openSeats
				}

				added := 0
				for i, seat := range state.Seats {
					if seat == "" {
						identity := bot.GetBotIdentity(i)
						botID := identity.UserID
						if botID == "" {
							logger.Warn("processBots: Bot identity at index %d has no UserID (not provisioned?).", i)
							continue
						}
						
						// Check Bot Balance and Top-up if needed
						account, err := nk.AccountGetId(ctx, botID)
						if err != nil {
							logger.Warn("processBots: Failed to get bot account %s: %v", botID, err)
						} else {
							var wallet map[string]int64
							if err := json.Unmarshal([]byte(account.Wallet), &wallet); err != nil {
								logger.Warn("processBots: Failed to unmarshal bot wallet %s: %v", botID, err)
							} else {
								gold := wallet["gold"] // Assuming "gold" is the currency key
								if gold < 10000 {
									// Add 10000 gold
									changes := map[string]int64{"gold": 10000}
									metadata := map[string]interface{}{"reason": "bot_topup"}
									if _, _, err := nk.WalletUpdate(ctx, botID, changes, metadata, true); err != nil {
										logger.Warn("processBots: Failed to top-up bot %s: %v", botID, err)
									} else {
										logger.Info("processBots: Replenished bot %s with 10k gold. New balance likely > %d", botID, gold+10000)
									}
								}
							}
						}

						state.Seats[i] = botID

						// Create Bot Agent via Factory
						agent, err := bot.NewAgent(botID)
						if err != nil {
							logger.Error("Failed to create bot agent for %s: %v", botID, err)
						} else {
							state.Bots[botID] = agent
						}

						logger.Info("processBots: Added bot %s (%s) to seat %d", identity.Username, botID, i)
						added++
						if added >= botsToAdd {
							break
						}
					}
				}
				if added > 0 {
					mh.updateLabel(state, dispatcher, logger)
					mh.broadcastMatchState(ctx, state, dispatcher, logger)
				}
				// Reset timer so it doesn't keep "adding"
				state.LastSinglePlayerTick = 0
			}
		} else {
			// Reset timer if 0 or >1 total players
			state.LastSinglePlayerTick = 0
		}
	}

	// 2. Handle bot turns in-game
	if state.Game != nil && state.Game.Phase == domain.PhasePlaying {
		currentTurn := state.Game.CurrentTurn
		currentUserID := state.Seats[currentTurn]

		if isBotUserId(currentUserID) {
			if state.BotWaitUntil == 0 {
				// Initialize random delay
				delay := rand.Intn(state.BotMaxDelay-state.BotMinDelay+1) + state.BotMinDelay
				state.BotWaitUntil = state.Tick + int64(delay)
				logger.Debug("processBots: Bot %s (seat %d) will act at tick %d (current %d)", currentUserID, currentTurn, state.BotWaitUntil, state.Tick)
			}

			if state.Tick >= state.BotWaitUntil {
				state.BotWaitUntil = 0 // Reset for next turn

				agent, exists := state.Bots[currentUserID]
				if !exists {
					// Fallback if agent missing (shouldn't happen for new bots)
					var err error
					agent, err = bot.NewAgent(currentUserID)
					if err != nil {
						logger.Error("processBots: Failed to create fallback agent: %v", err)
						return
					}
					state.Bots[currentUserID] = agent
				}

				move, err := agent.Play(state.Game)
				if err != nil {
					logger.Error("processBots: Bot %s failed to calculate move: %v", currentUserID, err)
					return
				}

				if move.Pass {
					events, err := state.App.PassTurn(state.Game, currentTurn)
					if err == nil {
						for _, ev := range events {
							mh.broadcastEvent(ctx, state, dispatcher, logger, ev)
						}
					}
				} else {
					events, err := state.App.PlayCards(state.Game, currentTurn, move.Cards)
					if err == nil {
						for _, ev := range events {
							mh.broadcastEvent(ctx, state, dispatcher, logger, ev)
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

func (mh *matchHandler) broadcastMatchState(ctx context.Context, state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger) {
	var playerStates []*pb.PlayerState
	for i, userId := range state.Seats {
		if userId == "" {
			continue
		}

		        displayName := userId

				avatarIndex := 0

		

				if p, exists := state.Presences[userId]; exists {

					displayName = p.GetUsername()

					// For humans, we could fetch from account if we wanted, 

					// but for now let's prioritize bots which we have in memory.

				}

		

				if botCfg, isBot := bot.GetBotConfig(userId); isBot {

					displayName = botCfg.DisplayName

					avatarIndex = botCfg.AvatarIndex

				}

		

				cardsRemaining := 0

				if state.Game != nil {

					for _, p := range state.Game.Players {

						if p.Seat == i {

							cardsRemaining = len(p.Hand)

							break

						}

					}

				}

		

				balance := int64(0)

				if !isBotUserId(userId) && state.Economy != nil {

					var err error

					balance, err = state.Economy.GetBalance(ctx, userId)

					if err != nil {

						logger.Warn("broadcastMatchState: Failed to fetch balance for user %s: %v", userId, err)

						balance = 0

					}

				}

		

				playerStates = append(playerStates, &pb.PlayerState{

					UserId:         userId,

					Seat:           int32(i),

					IsOwner:        i == state.OwnerSeat,

					CardsRemaining: int32(cardsRemaining),

					DisplayName:    displayName,

					AvatarIndex:    int32(avatarIndex),

					Balance:        balance,

				})

		
	}

	snapshot := &pb.MatchStateSnapshot{
		Seats:                state.Seats[:],
		OwnerSeat:            int32(state.OwnerSeat),
		Tick:                 state.Tick,
		TurnSecondsRemaining: state.TurnSecondsRemaining,
		Players:              playerStates,
	}
	bytes, _ := proto.Marshal(snapshot)
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_CODE_PLAYER_JOINED), bytes, nil, nil, true)
}

func (mh *matchHandler) handleStartGame(ctx context.Context, state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, msg runtime.MatchData) {
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

	// Resolve BaseBet (Default to "casual" or configured default if params missing)
	// TODO: Get tier from match metadata/params
	baseBet := config.GetBaseBet("") // Uses default tier

	// Initialize the domain Game via the Service
	game, events, err := state.App.StartGame(state.Seats[:], state.LastWinnerSeat, baseBet)
	if err != nil {
		logger.Error("StartGame: Failed to start game: %v", err)
		return
	}

	// Store the authoritative game state
	state.Game = game

	// Update match label to reflect playing state
	mh.updateLabel(state, dispatcher, logger)

	// Start the turn timer with extra buffer for dealing time.
	mh.resetTurnSecondsRemainingWithBonus(state, logger, gameStartTurnTimerBonusSeconds)

	// Broadcast resulting events
	for _, ev := range events {
		mh.broadcastEvent(ctx, state, dispatcher, logger, ev)
	}

	logger.Info("StartGame: Game started with %d players.", activeCount)
}

func (mh *matchHandler) handlePlayCards(ctx context.Context, state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, msg runtime.MatchData) {
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
		var hand []domain.Card
		if state.Game != nil {
			for _, p := range state.Game.Players {
				if p.Seat == senderSeat {
					hand = p.Hand
					break
				}
			}
		}
		logger.Warn("handlePlayCards: User %s (seat %d) failed to play cards: %v. Requested: %+v, Hand: %+v", senderID, senderSeat, err, domainCards, hand)
		mh.sendError(state, dispatcher, logger, senderID, 400, err.Error())
		return
	}

	mh.resetTurnSecondsRemaining(state, logger)

	// Broadcast events
	for _, ev := range events {
		mh.broadcastEvent(ctx, state, dispatcher, logger, ev)
	}
}

func (mh *matchHandler) handlePassTurn(ctx context.Context, state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, msg runtime.MatchData) {
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
		mh.sendError(state, dispatcher, logger, senderID, 400, err.Error())
		return
	}

	mh.resetTurnSecondsRemaining(state, logger)

	// Broadcast events
	for _, ev := range events {
		mh.broadcastEvent(ctx, state, dispatcher, logger, ev)
	}
}

func (mh *matchHandler) handleInGameChat(ctx context.Context, state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, msg runtime.MatchData) {
	senderID := msg.GetUserId()
	senderSeat := -1
	for i, seatUserId := range state.Seats {
		if seatUserId == senderID {
			senderSeat = i
			break
		}
	}

	if senderSeat == -1 {
		logger.Warn("handleInGameChat: User %s not in match.", senderID)
		return
	}

	request := &pb.InGameChatRequest{}
	if err := proto.Unmarshal(msg.GetData(), request); err != nil {
		logger.Error("handleInGameChat: Failed to unmarshal InGameChatRequest: %v", err)
		return
	}

	event := &pb.InGameChatEvent{
		SeatIndex: int32(senderSeat),
		Message:   request.GetMessage(),
	}

	bytes, err := proto.Marshal(event)
	if err != nil {
		logger.Error("handleInGameChat: Failed to marshal InGameChatEvent: %v", err)
		return
	}

	// Broadcast to everyone
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_CODE_IN_GAME_CHAT), bytes, nil, nil, true)
}

// broadcastEvent handles the conversion and dispatching of app events to Nakama.
func (mh *matchHandler) broadcastEvent(ctx context.Context, state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, ev app.Event) {
	var opCode int64
	var payload proto.Message

	switch ev.Kind {
	case app.EventGameStarted:
		opCode = int64(pb.OpCode_OP_CODE_GAME_STARTED)
		p := ev.Payload.(app.GameStartedPayload)
		logger.Debug("Event: game_started (firstTurnSeat=%d, handCount=%d, recipients=%d)", p.FirstTurnSeat, len(p.Hand), len(ev.Recipients))
		// Log the hand being sent to the specific user
		if len(ev.Recipients) > 0 {
			logger.Info("StartGame: Dealing to %s: %+v", ev.Recipients[0], p.Hand)
		}
		payload = &pb.GameStartedEvent{
			Phase:                pb.GamePhase_PHASE_PLAYING,
			FirstTurnSeat:        int32(p.FirstTurnSeat),
			Hand:                 toProtoCards(p.Hand),
			TurnSecondsRemaining: state.TurnSecondsRemaining,
		}
	case app.EventCardPlayed:
		opCode = int64(pb.OpCode_OP_CODE_CARD_PLAYED)
		p := ev.Payload.(app.CardPlayedPayload)
		payload = &pb.CardPlayedEvent{
			Seat:                 int32(p.Seat),
			Cards:                toProtoCards(p.Cards),
			NextTurnSeat:         int32(p.NextTurnSeat),
			NewRound:             p.NewRound,
			TurnSecondsRemaining: state.TurnSecondsRemaining,
		}
	case app.EventPigChopped:
		opCode = int64(pb.OpCode_OP_CODE_PIG_CHOPPED)
		p := ev.Payload.(app.PigChoppedPayload)
		payload = &pb.PigChoppedEvent{
			SourceSeat:     int32(p.SourceSeat),
			TargetSeat:     int32(p.TargetSeat),
			ChopType:       p.ChopType,
			CardsChopped:   toProtoCards(p.CardsChopped),
			CardsChopping:  toProtoCards(p.CardsChopping),
			BalanceChanges: p.BalanceChanges,
		}

		// Apply Immediate Balance Changes
		if state.Economy != nil {
			updates := make([]ports.WalletUpdate, 0, len(p.BalanceChanges))
			for userID, amount := range p.BalanceChanges {
				// Skip bots
				if isBotUserId(userID) {
					continue
				}
				updates = append(updates, ports.WalletUpdate{
					UserID: userID,
					Amount: amount,
					Metadata: map[string]interface{}{
						"match_id": ctx.Value(runtime.RUNTIME_CTX_MATCH_ID),
						"reason":   "pig_chop",
					},
				})
			}
			if err := state.Economy.UpdateBalances(ctx, updates); err != nil {
				logger.Error("Failed to update balances for Pig Chop: %v", err)
			}
		}

	case app.EventTurnPassed:
		opCode = int64(pb.OpCode_OP_CODE_TURN_PASSED)
		p := ev.Payload.(app.TurnPassedPayload)
		payload = &pb.TurnPassedEvent{
			Seat:                 int32(p.Seat),
			NextTurnSeat:         int32(p.NextTurnSeat),
			NewRound:             p.NewRound,
			TurnSecondsRemaining: state.TurnSecondsRemaining,
		}
	case app.EventPlayerFinished:
		opCode = int64(pb.OpCode_OP_CODE_PLAYER_FINISHED)
		p := ev.Payload.(app.PlayerFinishedPayload)
		payload = &pb.PlayerFinishedEvent{
			Seat: int32(p.Seat),
			Rank: int32(p.Rank),
		}
	case app.EventGameEnded:
		opCode = int64(pb.OpCode_OP_CODE_GAME_ENDED)
		p := ev.Payload.(app.GameEndedPayload)
		protoSeats := make([]int32, len(p.FinishOrderSeats))
		for i, seat := range p.FinishOrderSeats {
			protoSeats[i] = int32(seat)
		}

		protoRemainingHands := make(map[int32]*pb.CardList)
		if p.RemainingHands != nil {
			for seat, hand := range p.RemainingHands {
				protoRemainingHands[int32(seat)] = &pb.CardList{
					Cards: toProtoCards(hand),
				}
			}
		}

		payload = &pb.GameEndedEvent{
			FinishOrderSeats: protoSeats,
			BalanceChanges:   p.BalanceChanges,
			RemainingHands:   protoRemainingHands,
		}

		// Apply Balance Changes to Nakama Wallets
		if state.Economy != nil {
			updates := make([]ports.WalletUpdate, 0, len(p.BalanceChanges))
			for userID, amount := range p.BalanceChanges {
				// Skip bots
				if isBotUserId(userID) {
					continue
				}
				updates = append(updates, ports.WalletUpdate{
					UserID: userID,
					Amount: amount,
					Metadata: map[string]interface{}{
						"match_id": ctx.Value(runtime.RUNTIME_CTX_MATCH_ID),
						"reason":   "game_settlement",
					},
				})
			}
			if err := state.Economy.UpdateBalances(ctx, updates); err != nil {
				logger.Error("Failed to update balances: %v", err)
			}
		}

		// Save the winner for the next game
		if len(p.FinishOrderSeats) > 0 {
			state.LastWinnerSeat = p.FinishOrderSeats[0]
		}
		// Game ended, clear game state and update label back to lobby
		state.Game = nil
		mh.updateLabel(state, dispatcher, logger)
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

		// If we had intended recipients but none are connected (e.g. they are bots),
		// we MUST NOT broadcast to everyone else.
		if len(recipients) == 0 {
			return
		}
	}

	dispatcher.BroadcastMessage(opCode, bytes, recipients, nil, true)
}

// sendError sends a GameErrorEvent to a specific user.
func (mh *matchHandler) sendError(state *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, userID string, code int, message string) {
	payload := &pb.GameErrorEvent{
		Code:    int32(code),
		Message: message,
	}
	bytes, err := proto.Marshal(payload)
	if err != nil {
		logger.Error("Failed to marshal GameErrorEvent: %v", err)
		return
	}

	presence, ok := state.Presences[userID]
	if !ok {
		logger.Warn("Cannot send error to %s: Presence not found", userID)
		return
	}

	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_CODE_GAME_ERROR), bytes, []runtime.Presence{presence}, nil, true)
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
	matchState := "lobby"
	if state.Game != nil {
		matchState = "playing"
	}

	label := &pb.MatchLabel{
		Open:  int32(state.GetOpenSeatsCount()),
		State: matchState,
	}
	labelBytes, err := (&protojson.MarshalOptions{EmitUnpopulated: true}).Marshal(label)
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
	matchState, ok := state.(*MatchState)
	if !ok {
		return state, "state not found"
	}

	var signal struct {
		Op   string        `json:"op"`
		Deck []domain.Card `json:"deck"`
	}
	if err := json.Unmarshal([]byte(data), &signal); err != nil {
		logger.Warn("MatchSignal: Failed to unmarshal signal: %v", err)
		return state, "invalid signal"
	}

	if signal.Op == "start_with_deck" {
		logger.Info("MatchSignal: Starting game with rigged deck.")

		// Check standard constraints
		if matchState.GetOccupiedSeatCount() < app.MinPlayersToStartGame {
			return state, "not enough players"
		}

		baseBet := config.GetBaseBet("")

		// Call Service
		game, events, err := matchState.App.StartGameWithDeck(matchState.Seats[:], matchState.LastWinnerSeat, baseBet, signal.Deck)
		if err != nil {
			logger.Error("MatchSignal: Failed to start game: %v", err)
			return state, "failed to start"
		}

		matchState.Game = game
		mh.updateLabel(matchState, dispatcher, logger)
		mh.resetTurnSecondsRemainingWithBonus(matchState, logger, gameStartTurnTimerBonusSeconds)

		for _, ev := range events {
			mh.broadcastEvent(ctx, matchState, dispatcher, logger, ev)
		}

		return state, "game started"
	}

	return state, "unknown signal"
}
