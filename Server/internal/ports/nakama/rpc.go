package nakama

import (
	"context"
	"database/sql"
	"fmt"

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
		return matchId, nil
	}

	// 3. If no match is found, create a new one.
	moduleName := MatchNameTienLen // Must match the name registered in InitModule
	matchId, err := nk.MatchCreate(ctx, moduleName, nil)
	if err != nil {
		logger.Error("RpcFindMatch [User:%s]: Failed to create match: %v", userId, err)
		return "", err
	}

	logger.Info("RpcFindMatch [User:%s]: Created new match %s", userId, matchId)
	return matchId, nil
}
