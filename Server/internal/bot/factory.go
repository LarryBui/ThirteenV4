package bot

import (
	"fmt"
)

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
