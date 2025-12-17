package nakama

import (
	"context"
	"database/sql"
	"encoding/json"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
	"google.golang.org/protobuf/proto"
	"tienlen/internal/domain"
	pb "tienlen/proto"
)

const (
	MatchLabelKey_OpenSeats = "open" // Key for the open seats in the match label
)

type MatchState struct {
	Seats     [4]string                   `json:"seats"`    // Array of user IDs, empty string means seat is empty
	OwnerID   string                      `json:"owner_id"` // User ID of the match owner
	Tick      int64                       `json:"tick"`     // Current tick of the match for turn-based logic
	Presences map[string]runtime.Presence `json:"-"`        // Map UserId -> Presence for targeted messaging
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
		// Optionally send error back to sender
		return
	}

	// Deal Cards
	deck := domain.NewDeck()
	domain.Shuffle(deck)

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
			if uid != "" && cardIdx < len(deck) {
				hands[uid] = append(hands[uid], deck[cardIdx])
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
