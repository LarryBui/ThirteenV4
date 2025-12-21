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

type BetConfig struct {
	TaxRate     float64   `json:"tax_rate"`
	DefaultTier string    `json:"default_tier"`
	Tiers       []BetTier `json:"tiers"`
}

var (
	cfg      *BetConfig
	loadOnce sync.Once
	loadErr  error
)

// LoadBetConfig loads the betting configuration from the given path.
func LoadBetConfig(path string) error {
	loadOnce.Do(func() {
		data, err := os.ReadFile(path)
		if err != nil {
			loadErr = fmt.Errorf("failed to read bet config: %w", err)
			return
		}

		var c BetConfig
		if err := json.Unmarshal(data, &c); err != nil {
			loadErr = fmt.Errorf("failed to unmarshal bet config: %w", err)
			return
		}
		cfg = &c
	})
	return loadErr
}

// GetBetConfig returns the global betting configuration.
func GetBetConfig() *BetConfig {
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
