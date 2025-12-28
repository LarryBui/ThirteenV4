package bot

import (
	"context"
	"encoding/json"
	"fmt"
	"os"
	"sync"

	"github.com/heroiclabs/nakama-common/runtime"
)

type BotIdentity struct {
	DeviceID    string `json:"device_id"`
	UserID      string `json:"user_id"`
	Username    string `json:"username"`
	DisplayName string `json:"display_name"`
	Difficulty  string `json:"difficulty"` // "easy", "medium", "hard"
	AvatarIndex int    `json:"avatar_index"`
}

var (
	botIdentities     []BotIdentity
	botIDMap          map[string]bool
	botUsernameMap    map[string]string
	botDisplayNameMap map[string]string
	botConfigMap      map[string]BotIdentity
	loadOnce          sync.Once
	provisionOnce     sync.Once
	loadErr           error
)

// LoadIdentities loads the bot profiles from the given path.
func LoadIdentities(path string) error {
	loadOnce.Do(func() {
		data, err := os.ReadFile(path)
		if err != nil {
			loadErr = fmt.Errorf("failed to read bot identities: %w", err)
			return
		}

		if err := json.Unmarshal(data, &botIdentities); err != nil {
			loadErr = fmt.Errorf("failed to unmarshal bot identities: %w", err)
			return
		}

		botIDMap = make(map[string]bool)
		botUsernameMap = make(map[string]string)
		botDisplayNameMap = make(map[string]string)
		botConfigMap = make(map[string]BotIdentity)
		for _, identity := range botIdentities {
			if identity.UserID != "" {
				mapIdentity(identity)
			}
		}
	})
	return loadErr
}

func mapIdentity(identity BotIdentity) {
	botIDMap[identity.UserID] = true
	botUsernameMap[identity.UserID] = identity.Username
	botDisplayNameMap[identity.UserID] = identity.DisplayName
	botConfigMap[identity.UserID] = identity
}

// ProvisionBots ensures that bot accounts exist in the Nakama database and have the is_bot metadata.
func ProvisionBots(ctx context.Context, nk runtime.NakamaModule, logger runtime.Logger) error {
	var err error
	provisionOnce.Do(func() {
		for i := range botIdentities {
			identity := &botIdentities[i]
			if identity.DeviceID == "" {
				continue
			}

			// Authenticate/Create bot account using the requested username
			userID, username, _, authErr := nk.AuthenticateDevice(ctx, identity.DeviceID, identity.Username, true)
			if authErr != nil {
				logger.Error("ProvisionBots: Failed to authenticate bot %s: %v", identity.Username, authErr)
				continue
			}

			identity.UserID = userID
			identity.Username = username

			// Update Metadata and Profile
			metadata := map[string]interface{}{
				"is_bot":       true,
				"difficulty":   identity.Difficulty,
				"avatar_index": identity.AvatarIndex,
			}

			// Set both display name and username in the account update
			authErr = nk.AccountUpdateId(ctx, userID, identity.Username, metadata, identity.DisplayName, "", "", "", "")
			if authErr != nil {
				logger.Warn("ProvisionBots: Failed to update bot account %s: %v", userID, authErr)
			}

			// Update local maps for runtime lookup
			mapIdentity(*identity)

			logger.Info("ProvisionBots: Bot %s (%s) is ready. Difficulty: %s", identity.DisplayName, userID, identity.Difficulty)
		}
	})
	return err
}

// GetBotConfig returns the full identity configuration for a given bot ID.
func GetBotConfig(userID string) (BotIdentity, bool) {
	config, ok := botConfigMap[userID]
	return config, ok
}

// GetBotUsername returns the username for a bot ID, or an empty string if not a bot.
func GetBotUsername(userID string) string {
	if botUsernameMap == nil {
		return ""
	}
	return botUsernameMap[userID]
}

// GetBotDisplayName returns the display name for a bot ID, or an empty string if not a bot.
func GetBotDisplayName(userID string) string {
	if botDisplayNameMap == nil {
		return ""
	}
	name := botDisplayNameMap[userID]
	if name == "" {
		return GetBotUsername(userID)
	}
	return name
}

// GetBotIdentity returns an identity for a bot by index (mod pool size).
func GetBotIdentity(index int) BotIdentity {
	if len(botIdentities) == 0 {
		return BotIdentity{
			UserID:      fmt.Sprintf("bot-%d", index),
			DisplayName: fmt.Sprintf("AI Player %d", index),
		}
	}
	return botIdentities[index%len(botIdentities)]
}

// IsBot reports whether the given user ID belongs to the bot pool.
func IsBot(userID string) bool {
	if botIDMap == nil {
		return false
	}
	return botIDMap[userID]
}

// GetAllBotIDs returns all provisioned bot UserIDs.
func GetAllBotIDs() []string {
	ids := make([]string, 0, len(botIDMap))
	for id := range botIDMap {
		ids = append(ids, id)
	}
	return ids
}
