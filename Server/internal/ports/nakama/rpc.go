package nakama

import (
	"context"
	"database/sql"
	"encoding/json"
	"fmt"

	"tienlen/internal/domain"

	"github.com/heroiclabs/nakama-common/runtime"
)

// RpcFindMatch searches for an available match with open seats.
// If an available match is found, it returns the Match ID.
// If no match is found, it creates a new match and returns its ID.
//
// Payload: (Optional) Unused for now.
// Returns: String containing the Match ID.
func RpcFindMatch(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userId, _ := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)

	// 1. Search for matches with at least 1 open seat.
	// Query syntax: "+label.open:>=1"
	// +label.open means we are filtering on the "open" key in the JSON label.
	// :>=1 means the value must be greater than or equal to 1.
	limit := 1
	authoritative := true
	labelQuery := fmt.Sprintf("+label.%s:>=1", MatchLabelKey_OpenSeats)
	minSize := 0
	maxSize := 4
	
	matches, err := nk.MatchList(ctx, limit, authoritative, "", &minSize, &maxSize, labelQuery)
	if err != nil {
		logger.Error("RpcFindMatch [User:%s]: Failed to list matches: %v", userId, err)
		return "", err
	}

	// 2. If a match is found, return its ID.
	if len(matches) > 0 {
		matchId := matches[0].MatchId
		logger.Info("RpcFindMatch [User:%s]: Found existing match %s", userId, matchId)
		return fmt.Sprintf("%q", matchId), nil
	}

	// 3. If no match is found, create a new one.
	moduleName := MatchNameTienLen // Must match the name registered in InitModule
	matchId, err := nk.MatchCreate(ctx, moduleName, nil)
	if err != nil {
		logger.Error("RpcFindMatch [User:%s]: Failed to create match: %v", userId, err)
		return "", err
	}

	logger.Info("RpcFindMatch [User:%s]: Created new match %s", userId, matchId)
	return fmt.Sprintf("%q", matchId), nil
}

// RpcCreateMatchTest is for integration testing only. It always creates a fresh match.
func RpcCreateMatchTest(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	moduleName := MatchNameTienLen
	matchId, err := nk.MatchCreate(ctx, moduleName, nil)
	if err != nil {
		return "", err
	}

	return fmt.Sprintf("%q", matchId), nil
}

type riggedHand struct {
	Seat  int           `json:"seat"`
	Cards []domain.Card `json:"cards"`
}

// RpcStartGameTest is for integration testing only.
func RpcStartGameTest(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	type request struct {
		MatchId string       `json:"match_id"`
		Hands   []riggedHand `json:"hands"`
	}
	var req request
	if err := json.Unmarshal([]byte(payload), &req); err != nil {
		logger.Error("RpcStartGameTest: Failed to unmarshal payload: %v", err)
		return "", err
	}
	
	fullDeck := domain.NewDeck()
	usedCards := make(map[domain.Card]bool)
	
	// Mark used cards
	for _, h := range req.Hands {
		for _, c := range h.Cards {
			usedCards[c] = true
		}
	}
	
	finalDeck := make([]domain.Card, 0, 52)
	unusedIdx := 0
	
	for s := 0; s < 4; s++ {
		var userHand []domain.Card
		for _, h := range req.Hands {
			if h.Seat == s {
				userHand = h.Cards
				break
			}
		}
		
		// Add explicit cards
		finalDeck = append(finalDeck, userHand...)
		
		// Fill remaining
		needed := 13 - len(userHand)
		for k := 0; k < needed; k++ {
			for unusedIdx < 52 {
				c := fullDeck[unusedIdx]
				unusedIdx++
				if !usedCards[c] {
					finalDeck = append(finalDeck, c)
					// No need to mark used, we strictly iterate fullDeck once
					break
				}
			}
		}
	}
	
	deckBytes, _ := json.Marshal(finalDeck)
	
	signalPayload := fmt.Sprintf(`{"op": "start_with_deck", "deck": %s}`, string(deckBytes))
	
	// Signal the match
	_, err := nk.MatchSignal(ctx, req.MatchId, signalPayload)
	if err != nil {
		logger.Error("RpcStartGameTest: Failed to signal match: %v", err)
		return "", err
	}
	
	logger.Info("RpcStartGameTest: Signaled match %s to start with rigged deck.", req.MatchId)
	return "{}", nil
}

