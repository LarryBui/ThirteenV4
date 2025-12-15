package nakama

import (
	"context"
	"database/sql"
	"encoding/json"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

const (
	MatchLabelKey_OpenSeats = "open" // Key for the open seats in the match label
)

type MatchState struct {
	Seats   [4]string `json:"seats"`    // Array of user IDs, empty string means seat is empty
	OwnerID string    `json:"owner_id"` // User ID of the match owner
	Tick    int64     `json:"tick"`     // Current tick of the match for turn-based logic
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

// NewMatch is the factory function registered with Nakama.
func NewMatch(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule) (runtime.Match, error) {
	return &matchHandler{}, nil
}

type matchHandler struct{}

// MatchInit is called when the match is created.
func (mh *matchHandler) MatchInit(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, params map[string]interface{}) (interface{}, int, string) {
	logger.Debug("MatchInit: Initializing match handler.")

	state := &MatchState{
		Tick: time.Now().Unix(),
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
	label := map[string]int{
		MatchLabelKey_OpenSeats: matchState.GetOpenSeatsCount(),
	}
	labelBytes, err := json.Marshal(label)
	if err != nil {
		logger.Error("MatchJoin: Failed to marshal label: %v", err)
	} else {
		if err := dispatcher.MatchLabelUpdate(string(labelBytes)); err != nil {
			logger.Error("MatchJoin: Failed to update match label: %v", err)
		}
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
		for i, seatUserId := range matchState.Seats {
			if seatUserId == p.GetUserId() {
				matchState.Seats[i] = ""
				logger.Debug("MatchLeave: User %s left, seat %d freed.", p.GetUserId(), i)

				if matchState.OwnerID == p.GetUserId() {
					matchState.OwnerID = ""
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

	label := map[string]int{
		MatchLabelKey_OpenSeats: matchState.GetOpenSeatsCount(),
	}
	labelBytes, err := json.Marshal(label)
	if err != nil {
		logger.Error("MatchLeave: Failed to marshal label: %v", err)
	} else {
		if err := dispatcher.MatchLabelUpdate(string(labelBytes)); err != nil {
			logger.Error("MatchLeave: Failed to update match label: %v", err)
		}
	}

	return matchState
}

func (mh *matchHandler) MatchLoop(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, messages []runtime.MatchData) interface{} {
	return state
}

func (mh *matchHandler) MatchTerminate(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, reason int) interface{} {
	logger.Debug("MatchTerminate: Match terminated for reason %d", reason)
	return state
}

func (mh *matchHandler) MatchSignal(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, data string) (interface{}, string) {
	return state, ""
}