package nakama

import (
	"context"
	"database/sql"

	"github.com/heroiclabs/nakama-common/runtime"
)

// InitModule wires RPCs and match handlers for Nakama runtime.
func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	if err := RegisterRPCs(initializer); err != nil {
		return err
	}

	if err := initializer.RegisterMatch(MatchNameTienLen, func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule) (runtime.Match, error) {
		return newMatchHandler(), nil
	}); err != nil {
		return err
	}

	logger.Info("TienLen Go module loaded.")
	return nil
}
