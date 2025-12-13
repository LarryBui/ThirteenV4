package nakama

import (
	"context"
	"database/sql"
	"encoding/json"

	"github.com/heroiclabs/nakama-common/runtime"
)

// QuickMatchResponse is the payload returned to clients when requesting a lobby-capable match.
type QuickMatchResponse struct {
	MatchID string `json:"match_id"`
	IsNew   bool   `json:"is_new"`
}

// RegisterRPCs registers Nakama RPC endpoints.
func RegisterRPCs(initializer runtime.Initializer) error {
	return initializer.RegisterRpc(RpcQuickMatch, rpcQuickMatch)
}

func rpcQuickMatch(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	// Find any match that is open and is our game.
	query := "+label.open:T label.game:tienlen label.phase:lobby"

	limit := 10
	authoritative := true

	minSize := 1
	maxSize := 3 // ensure < 4 players

	matches, err := nk.MatchList(ctx, limit, authoritative, "", &minSize, &maxSize, query)
	if err != nil {
		logger.Error("MatchList error: %v", err)
		return "", err
	}

	if len(matches) > 0 {
		resp := QuickMatchResponse{MatchID: matches[0].MatchId, IsNew: false}
		b, _ := json.Marshal(resp)
		return string(b), nil
	}

	// Create new match; seat/owner assignment happens in MatchJoin (server-authoritative).
	matchID, err := nk.MatchCreate(ctx, MatchNameTienLen, map[string]interface{}{})
	if err != nil {
		logger.Error("MatchCreate error: %v", err)
		return "", err
	}

	resp := QuickMatchResponse{MatchID: matchID, IsNew: true}
	b, _ := json.Marshal(resp)
	return string(b), nil
}
