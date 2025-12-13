package main

import (
	"context"
	"database/sql"

	"github.com/heroiclabs/nakama-common/runtime"
)

// RpcQuickMatch is the Nakama RPC id clients call to find or create a lobby-capable match.
const RpcQuickMatch = "quick_match"

// MatchNameTienLen is the authoritative match handler name registered with Nakama.
const MatchNameTienLen = "tienlen_match"

// InitModule wires the Nakama Go runtime module, registering RPCs and match handlers.
func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	if err := initializer.RegisterRpc(RpcQuickMatch, rpcQuickMatch); err != nil {
		return err
	}

	if err := initializer.RegisterMatch(MatchNameTienLen, func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule) (runtime.Match, error) {
		return &TienLenMatch{}, nil
	}); err != nil {
		return err
	}

	logger.Info("TienLen Go module loaded.")
	return nil
}
