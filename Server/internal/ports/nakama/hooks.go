package nakama

import (
	"context"
	"database/sql"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"strings"

	"tienlen/internal/app/onboarding"

	"github.com/heroiclabs/nakama-common/api"
	"github.com/heroiclabs/nakama-common/runtime"
)

// AfterAuthenticateDevice is triggered after an account is authenticated.
// It initializes the wallet for new accounts.
func AfterAuthenticateDevice(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, out *api.Session, in *api.AuthenticateDeviceRequest) error {
	// Check if the account was just created
	if out.Created {
		userID := ""
		if ctxUserID, ok := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string); ok {
			userID = ctxUserID
		}
		if userID == "" {
			// Resolve User ID from the session token by parsing the JWT payload manually.
			resolvedID, err := extractUserIDFromToken(out.Token)
			if err != nil {
				logger.Error("AfterAuthenticateDevice: Failed to extract user ID from token: %v", err)
				return err
			}
			userID = resolvedID
		}

		logger.Info("Onboarding new user %s", userID)

		service := onboarding.NewService(NewNakamaAccountAdapter(nk), NewNakamaWelcomeBonusAdapter(nk), nil)
		result, err := service.OnboardNewUser(ctx, userID)
		if result.ProfileUpdateErr != nil {
			logger.Warn("AfterAuthenticateDevice: Failed to update profile for user %s: %v", userID, result.ProfileUpdateErr)
		}
		if !result.WelcomeBonusGranted {
			logger.Info("AfterAuthenticateDevice: Welcome bonus already granted for user %s", userID)
		}
		if err != nil {
			logger.Error("AfterAuthenticateDevice: Onboarding failed for user %s: %v", userID, err)
			return err
		}
	}
	return nil
}

func extractUserIDFromToken(token string) (string, error) {
	parts := strings.Split(token, ".")
	if len(parts) != 3 {
		return "", fmt.Errorf("invalid token format")
	}

	payload := parts[1]
	// JWT base64 is RawUrlEncoding (no padding)
	data, err := base64.RawURLEncoding.DecodeString(payload)
	if err != nil {
		return "", fmt.Errorf("failed to decode token payload: %w", err)
	}

	var claims map[string]interface{}
	if err := json.Unmarshal(data, &claims); err != nil {
		return "", fmt.Errorf("failed to unmarshal token claims: %w", err)
	}

	uid, ok := claims["uid"].(string)
	if !ok {
		return "", fmt.Errorf("token claims missing uid")
	}

	return uid, nil
}
