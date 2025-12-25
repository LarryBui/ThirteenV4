package nakama

import (
	"context"
	"database/sql"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"math/rand"
	"strings"
	"time"

	"github.com/heroiclabs/nakama-common/api"
	"github.com/heroiclabs/nakama-common/runtime"
	"google.golang.org/protobuf/types/known/wrapperspb"
	"github.com/google/uuid"
)

// BeforeAuthenticateDevice intercepts device authentication to force creation of a new user every time.
func BeforeAuthenticateDevice(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, in *api.AuthenticateDeviceRequest) (*api.AuthenticateDeviceRequest, error) {
	// Generate a new random UUID for the device ID
	newDeviceID := uuid.New().String()

	logger.Info("Intercepting Device Auth. Replacing original Device ID '%s' with new random ID '%s' to force new user creation.", in.Account.Id, newDeviceID)

	// Replace the Device ID with the new random one
	in.Account.Id = newDeviceID

	// Force 'Create' to true so the account is created
	in.Create = &wrapperspb.BoolValue{Value: true}

	return in, nil
}

// AfterAuthenticateDevice is triggered after an account is authenticated.
// It initializes the wallet for new accounts.
func AfterAuthenticateDevice(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, out *api.Session, in *api.AuthenticateDeviceRequest) error {
	// Check if the account was just created
	if out.Created {
		// Resolve User ID from the session token by parsing the JWT payload manually
		userID, err := extractUserIDFromToken(out.Token)
		if err != nil {
			logger.Error("AfterAuthenticateDevice: Failed to extract user ID from token: %v", err)
			return err
		}

		logger.Info("Initializing wallet for new user %s", userID)

		// Generate and set a friendly display name
		displayName := generateFriendlyName()
		// Signature: ctx, userID, username, metadata, displayName, timezone, location, langTag, avatarUrl
		if err := nk.AccountUpdateId(ctx, userID, displayName, nil, displayName, "", "", "", ""); err != nil {
			logger.Error("AfterAuthenticateDevice: Failed to update account for user %s: %v", userID, err)
			// Don't return error here, wallet update is more important
		} else {
			logger.Info("Assigned friendly name '%s' to user %s", displayName, userID)
		}

		// Grant 10,000 Gold
		changes := map[string]int64{
			"gold": 10000,
		}
		metadata := map[string]interface{}{
			"reason": "welcome_bonus",
		}
		
		if _, _, err := nk.WalletUpdate(ctx, userID, changes, metadata, true); err != nil {
			logger.Error("AfterAuthenticateDevice: Failed to update wallet for user %s: %v", userID, err)
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

	

	func generateFriendlyName() string {

		adjectives := []string{"Happy", "Shiny", "Brave", "Clever", "Swift", "Calm", "Mighty", "Witty", "Sly", "Wild"}

		nouns := []string{"Panda", "Tiger", "Eagle", "Dolphin", "Wolf", "Otter", "Falcon", "Bear", "Fox", "Lion"}

	

		rand.Seed(time.Now().UnixNano())

		adj := adjectives[rand.Intn(len(adjectives))]

		noun := nouns[rand.Intn(len(nouns))]

		num := rand.Intn(9000) + 1000

	

		return fmt.Sprintf("%s%s%d", adj, noun, num)

	}

	