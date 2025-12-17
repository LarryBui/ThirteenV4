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

// MatchRuntimeState holds the authoritative runtime state for the Nakama match handler.
type MatchRuntimeState struct {
	Seats     [4]string                   `json:"seats"`    // Array of user IDs, empty string means seat is empty
	OwnerID   string                      `json:"owner_id"` // User ID of the match owner
	Tick      int64                       `json:"tick"`     // Current tick of the match for turn-based logic
	Presences map[string]runtime.Presence `json:"-"`        // Map UserId -> Presence for targeted messaging
	App       *app.Service                `json:"-"`        // TienLen app service with game logic
}

func (ms *MatchRuntimeState) GetOpenSeatsCount() int {
	count := 0
	for _, seat := range ms.Seats {
		if seat == "" {
			count++
		}
	}
	return count
}

func (ms *MatchRuntimeState) GetOccupiedSeatCount() int {
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

	state := &MatchRuntimeState{
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
	matchState, ok := state.(*MatchRuntimeState)
	if !ok {
		return state, false, "state not found"
	}

	if matchState.GetOpenSeatsCount() <= 0 {
		return state, false, "Match full"
	}

	return state, true, ""
}

func (mh *matchHandler) MatchJoin(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	matchState, ok := state.(*MatchRuntimeState)
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

	snapshot := &pb.MatchStateSnapshot{
		Seats:   matchState.Seats[:],
		OwnerId: matchState.OwnerID,
		Tick:    matchState.Tick,
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
	matchState, ok := state.(*MatchRuntimeState)
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
	matchState, ok := state.(*MatchRuntimeState)
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

func (mh *matchHandler) handleStartGame(state *MatchRuntimeState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, msg runtime.MatchData) {
	senderID := msg.GetUserId()
	if senderID != state.OwnerID {
		logger.Warn("StartGame: User %s tried to start game but is not owner (%s)", senderID, state.OwnerID)
		return
	}

	activeCount := state.GetOccupiedSeatCount()
	if activeCount < 2 {
		logger.Warn("StartGame: Cannot start with %d players. Need at least 2.", activeCount)
		// Optionally send error back to sender
		return
	}

	// Deal Cards
	deck := domain.NewDeck()
	domain.Shuffle(deck)
	pbDeck := make([]*pb.Card, len(deck))
	for i, card := range deck {
		pbDeck[i] = toProtoCard(card)
	}

	hands := make(map[string][]*pb.Card)
	// Initialize hands for active seats
	for _, uid := range state.Seats {
		if uid != "" {
			hands[uid] = make([]*pb.Card, 0)
		}
	}

	// Distribute 13 cards (or as many as possible if deck < 13*players, which shouldn't happen in standard game)
	// Standard Tien Len: 52 cards. 4 players = 13 each. 2 players = 13 each (26 left over).
	cardIdx := 0
	cardsPerPlayer := 13

	for i := 0; i < cardsPerPlayer; i++ {
		for _, uid := range state.Seats {
			if uid != "" && cardIdx < len(pbDeck) {
				hands[uid] = append(hands[uid], pbDeck[cardIdx])
				cardIdx++
			}
		}
	}

	// 1. Broadcast Game Started Event
	startEvent := &pb.GameStartedEvent{
		FirstTurnUserId: "", // TODO: Determine first turn based on 3 of Spades
		Phase:           pb.GamePhase_PHASE_PLAYING,
	}
	startPayload, _ := proto.Marshal(startEvent)
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_CODE_GAME_STARTED), startPayload, nil, nil, true)

	// 2. Send unique HandDealtEvent to each player
	for uid, hand := range hands {
		presence, exists := state.Presences[uid]
		if !exists {
			logger.Warn("StartGame: User %s has hand but no presence.", uid)
			continue
		}

		event := &pb.HandDealtEvent{
			Hand: hand,
		}

		payload, err := proto.Marshal(event)
		if err != nil {
			logger.Error("StartGame: Failed to marshal HandDealtEvent for %s: %v", uid, err)
			continue
		}

		dispatcher.BroadcastMessage(int64(pb.OpCode_OP_CODE_HAND_DEALT), payload, []runtime.Presence{presence}, nil, true)
	}

	logger.Info("StartGame: Game started with %d players.", activeCount)
}

func (mh *matchHandler) handlePlayCards(state *MatchRuntimeState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, msg runtime.MatchData) {
	senderID := msg.GetUserId()
	logger.Debug("handlePlayCards: User %s wants to play cards.", senderID)

	// Convert Nakama's MatchRuntimeState into app's domain.MatchState
	// For now, only phase and players are needed for app.Service
	appMatchState := &domain.MatchState{
		Phase:   domain.Phase(state.Tick), // TODO: Use actual game phase from MatchRuntimeState
		Players: make(map[string]*domain.Player),
	}
	for _, presence := range state.Presences {
		// This mapping is incomplete. You need to properly map
		// Nakama's MatchRuntimeState player data to domain.Player
		// Including hand, hasPassed, etc. which are currently in app.Service's state.
		appMatchState.Players[presence.GetUserId()] = &domain.Player{
			UserID: presence.GetUserId(),
			// Populate other fields from a more comprehensive MatchRuntimeState if they exist
		}
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
			Suit: card.GetSuit().String(), // Assuming string conversion is consistent
			Rank: int(card.GetRank()),
		}
	}

	// Call app service
	// IMPORTANT: This currently uses a placeholder appMatchState.
	// The full domain.MatchState needs to be reconstructed from MatchRuntimeState
	// or MatchRuntimeState needs to fully contain domain.MatchState.
	events, err := state.App.PlayCards(appMatchState, senderID, domainCards)
	if err != nil {
		logger.Warn("handlePlayCards: User %s failed to play cards: %v", senderID, err)
		// TODO: Send error back to client
		return
	}

	// Broadcast events
	for _, ev := range events {
		payload, err := proto.Marshal(toProtoEvent(ev))
		if err != nil {
			logger.Error("handlePlayCards: Failed to marshal event: %v", err)
			continue
		}
		// TODO: Determine recipients based on event type and app.Service events
		dispatcher.BroadcastMessage(int64(pb.OpCode_OP_CODE_CARD_PLAYED), payload, nil, nil, true)
	}
}

func (mh *matchHandler) handlePassTurn(state *MatchRuntimeState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, msg runtime.MatchData) {
	senderID := msg.GetUserId()
	logger.Debug("handlePassTurn: User %s wants to pass turn.", senderID)

	// Convert Nakama's MatchRuntimeState into app's domain.MatchState
	appMatchState := &domain.MatchState{
		Phase:   domain.Phase(state.Tick), // TODO: Use actual game phase from MatchRuntimeState
		Players: make(map[string]*domain.Player),
	}
	for _, presence := range state.Presences {
		// This mapping is incomplete. You need to properly map
		// Nakama's MatchRuntimeState player data to domain.Player
		// Including hand, hasPassed, etc. which are currently in app.Service's state.
		appMatchState.Players[presence.GetUserId()] = &domain.Player{
			UserID: presence.GetUserId(),
			// Populate other fields from a more comprehensive MatchRuntimeState if they exist
		}
	}

	// Unmarshal client request (PassTurnRequest might be empty or contain context)
	// For now, assuming no specific payload for PassTurnRequest
	// If a payload exists, unmarshal it here.

	// Call app service
	// IMPORTANT: This currently uses a placeholder appMatchState.
	// The full domain.MatchState needs to be reconstructed from MatchRuntimeState
	// or MatchRuntimeState needs to fully contain domain.MatchState.
	events, err := state.App.PassTurn(appMatchState, senderID)
	if err != nil {
		logger.Warn("handlePassTurn: User %s failed to pass turn: %v", senderID, err)
		// TODO: Send error back to client
		return
	}

	// Broadcast events
	for _, ev := range events {
		payload, err := proto.Marshal(toProtoEvent(ev))
		if err != nil {
			logger.Error("handlePassTurn: Failed to marshal event: %v", err)
			continue
		}
		// TODO: Determine recipients based on event type and app.Service events
		dispatcher.BroadcastMessage(int64(pb.OpCode_OP_CODE_TURN_PASSED), payload, nil, nil, true)
	}
}

// TODO: This is a placeholder. You need to implement a proper conversion
// from app.Event to a protobuf event that clients understand.
func toProtoEvent(ev app.Event) proto.Message {
	switch ev.Kind {
	case app.EventCardPlayed:
		// Example conversion, adapt to your proto definition
		payload := ev.Payload.(app.CardPlayedPayload)
		return &pb.CardPlayedEvent{
			UserId: payload.UserID,
			Cards:  toProtoCards(payload.Cards),
		}
	case app.EventTurnPassed:
		// Example conversion
		payload := ev.Payload.(app.TurnPassedPayload)
		return &pb.TurnPassedEvent{
			UserId: payload.UserID,
		}
	default:
		return nil
	}
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
	suit := pb.Suit_SUIT_SPADES
	switch card.Suit {
	case "C":
		suit = pb.Suit_SUIT_CLUBS
	case "D":
		suit = pb.Suit_SUIT_DIAMONDS
	case "H":
		suit = pb.Suit_SUIT_HEARTS
	}

	return &pb.Card{
		Suit: suit,
		Rank: pb.Rank(card.Rank),
	}
}

func (mh *matchHandler) updateLabel(state *MatchRuntimeState, dispatcher runtime.MatchDispatcher, logger runtime.Logger) {
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
