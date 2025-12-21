package bot

import (
	"fmt"
)

// NewAgent creates a new autonomous bot agent with the configured identity and difficulty.
func NewAgent(botID string) (*Agent, error) {
	config, ok := GetBotConfig(botID)
	if !ok {
		// Fallback for unknown bots (e.g. testing)
		return &Agent{
			ID:       botID,
			Name:     "Unknown Bot",
			Strategy: &SmartBot{}, // Default to Smart
		}, nil
	}

	// Map string difficulty to BotLevel enum
	var level BotLevel
	switch config.Difficulty {
	case "god":
		level = BotLevelGod
	case "good":
		level = BotLevelGood
	case "smart":
		level = BotLevelSmart
	default:
		level = BotLevelSmart
	}

	brain, err := NewBrain(level)
	if err != nil {
		return nil, err
	}

	return &Agent{
		ID:       botID,
		Name:     config.Username,
		Strategy: brain,
	}, nil
}

// NewBrain creates a new AI brain based on the specified level.
func NewBrain(level BotLevel) (Brain, error) {
	switch level {
	case BotLevelGood:
		return &GoodBot{}, nil
	case BotLevelSmart:
		return &SmartBot{}, nil
	case BotLevelGod:
		return &GodBot{}, nil
	default:
		return nil, fmt.Errorf("unknown bot level: %d", level)
	}
}
