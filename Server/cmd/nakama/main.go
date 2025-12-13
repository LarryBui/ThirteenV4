package main

import (
	"context"
	"database/sql"

	"tienlen/internal/ports/nakama"

	"github.com/heroiclabs/nakama-common/runtime"
)

// InitModule proxies Nakama initialization to the nakama adapter package.
func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	return nakama.InitModule(ctx, logger, db, nk, initializer)
}
