package nakama

import (
	"context"
	"database/sql"
	"os"

	"tienlen/internal/app"
	"tienlen/internal/bot"

	"github.com/heroiclabs/nakama-common/runtime"
)

const MatchNameTienLen = "tienlen_match"

// InitModule wires RPCs and match handlers for Nakama runtime.
func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	env := ctx.Value(runtime.RUNTIME_CTX_ENV).(map[string]string)
	vivoxSecret := envOrOs(env, "VIVOX_SECRET")
	vivoxIssuer := envOrOs(env, "VIVOX_ISSUER")
	vivoxDomain := envOrOs(env, "VIVOX_DOMAIN")

	// Initialize the Vivox service
	vivoxService = app.NewVivoxService(vivoxSecret, vivoxIssuer, vivoxDomain)

	if err := initializer.RegisterRpc("find_match", RpcFindMatch); err != nil {
		return err
	}
	if err := initializer.RegisterRpc("get_vivox_token", RpcGetVivoxToken); err != nil {
		return err
	}

	if err := initializer.RegisterRpc("set_vip", RpcSetVip); err != nil {
		return err
	}

	if err := initializer.RegisterRpc("test_create_match", RpcCreateMatchTest); err != nil {
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

	// Initialize Bots
	if err := bot.LoadIdentities("data/bot_identities.json"); err != nil {
		logger.Warn("InitModule: Could not load bot identities: %v", err)
	} else {
		if err := bot.ProvisionBots(ctx, nk, logger); err != nil {
			logger.Warn("InitModule: Failed to provision bots: %v", err)
		}
	}

	logger.Info("TienLen Go module loaded.")
	return nil
}

func envOrOs(env map[string]string, key string) string {
	if value, ok := env[key]; ok && value != "" {
		return value
	}
	return os.Getenv(key)
}
