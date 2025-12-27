package nakama

import (
	"context"
	"database/sql"

	"github.com/heroiclabs/nakama-common/runtime"
)

const MatchNameTienLen = "tienlen_match"

// InitModule wires RPCs and match handlers for Nakama runtime.
func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	if err := initializer.RegisterRpc("find_match", RpcFindMatch); err != nil {
		return err
	}

	// Register test-only RPCs if test mode is enabled
	env := ctx.Value(runtime.RUNTIME_CTX_ENV).(map[string]string)
	if val, ok := env["tienlen_test_mode"]; ok && val == "true" {
		if err := initializer.RegisterRpc("test_create_match", RpcCreateMatchTest); err != nil {
			return err
		}
		if err := initializer.RegisterRpc("test_start_game", RpcStartGameTest); err != nil {
			return err
		}
		logger.Info("Test RPCs registered.")
	}

	if err := initializer.RegisterAfterAuthenticateDevice(AfterAuthenticateDevice); err != nil {
		return err
	}

	if err := initializer.RegisterMatch(MatchNameTienLen, NewMatch); err != nil {
		return err
	}

	logger.Info("TienLen Go module loaded.")
	return nil
}
