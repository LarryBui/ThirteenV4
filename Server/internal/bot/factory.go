package bot

// NewAgent creates a new autonomous bot agent with the configured identity.
func NewAgent(botID string) (*Agent, error) {
	config, ok := GetBotConfig(botID)
	
	name := "Unknown Bot"
	if ok {
		name = config.Username
	}

	return &Agent{
		ID:       botID,
		Name:     name,
		Strategy: &StandardBot{},
	}, nil
}