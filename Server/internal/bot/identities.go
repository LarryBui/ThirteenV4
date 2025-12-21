package bot

import (
	"encoding/json"
	"fmt"
	"os"
	"sync"
)

type BotIdentity struct {
	UserID     string `json:"user_id"`
	Username   string `json:"username"`
	Difficulty string `json:"difficulty"` // "good", "smart", "god"
}

var (
	botIdentities  []BotIdentity
	botIDMap       map[string]bool
	botUsernameMap map[string]string
	botConfigMap   map[string]BotIdentity
	loadOnce       sync.Once
	loadErr        error
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
		botConfigMap = make(map[string]BotIdentity)
		for _, identity := range botIdentities {
			botIDMap[identity.UserID] = true
			botUsernameMap[identity.UserID] = identity.Username
			botConfigMap[identity.UserID] = identity
		}
	})
	return loadErr
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

// GetBotIdentity returns an identity for a bot by index (mod pool size).
func GetBotIdentity(index int) BotIdentity {
	if len(botIdentities) == 0 {
		return BotIdentity{
			UserID:   fmt.Sprintf("bot-%d", index),
			Username: fmt.Sprintf("AI Player %d", index),
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
