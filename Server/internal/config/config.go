package config

import (
	"encoding/json"
	"fmt"
	"os"
	"sync"
)

type BetTier struct {
	ID      string `json:"id"`
	BaseBet int64  `json:"base_bet"`
}

type GameConfig struct {
	TaxRate             float64   `json:"tax_rate"`
	DefaultTier         string    `json:"default_tier"`
	Tiers               []BetTier `json:"tiers"`
	TurnDurationSeconds int       `json:"turn_duration_seconds"`
	// BotAutoFillDelaySeconds configures how many seconds to wait before adding a bot to a solo human lobby.
	BotAutoFillDelaySeconds int `json:"bot_auto_fill_delay_seconds"`
}

var (
	cfg      *GameConfig
	loadOnce sync.Once
	loadErr  error
)

// LoadGameConfig loads the game configuration from the given path.
func LoadGameConfig(path string) error {
	loadOnce.Do(func() {
		data, err := os.ReadFile(path)
		if err != nil {
			loadErr = fmt.Errorf("failed to read game config: %w", err)
			return
		}

		var c GameConfig
		if err := json.Unmarshal(data, &c); err != nil {
			loadErr = fmt.Errorf("failed to unmarshal game config: %w", err)
			return
		}
		cfg = &c
	})
	return loadErr
}

// GetGameConfig returns the global game configuration.
func GetGameConfig() *GameConfig {
	return cfg
}

// GetBaseBet returns the base bet for a given tier ID, or the default if not found.
func GetBaseBet(tierID string) int64 {
	if cfg == nil {
		return 100 // Safe default
	}

	target := tierID
	if target == "" {
		target = cfg.DefaultTier
	}

	for _, tier := range cfg.Tiers {
		if tier.ID == target {
			return tier.BaseBet
		}
	}

	// Fallback to default tier if specific ID not found
	for _, tier := range cfg.Tiers {
		if tier.ID == cfg.DefaultTier {
			return tier.BaseBet
		}
	}

	return 100
}
