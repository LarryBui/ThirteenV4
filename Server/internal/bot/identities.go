package bot

import (
	"encoding/json"
	"fmt"
	"os"
	"sync"
)

type BotIdentity struct {
	UserID   string `json:"user_id"`
	Username string `json:"username"`
}

var (
	botIdentities []BotIdentity
	botIDMap      map[string]bool
	loadOnce      sync.Once
	loadErr       error
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
		for _, identity := range botIdentities {
			botIDMap[identity.UserID] = true
		}
	})
	return loadErr
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
