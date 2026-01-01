package bot

import (
	"tienlen/internal/bot/brain"
)

// NewAgent creates a new autonomous bot agent with the configured identity.
func NewAgent(botID string) (*Agent, error) {
	config, ok := GetBotConfig(botID)
	
	name := "Unknown Bot"
	if ok {
		name = config.Username
	}

	mem := brain.NewMemory()
	return &Agent{
		ID:       botID,
		Name:     name,
		Strategy: &StandardBot{
			Memory:    mem,
			Estimator: brain.NewEstimator(mem),
		},
	}, nil
}