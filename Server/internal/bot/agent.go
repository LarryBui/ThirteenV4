package bot

import (
	"tienlen/internal/domain"
)

// Agent represents an autonomous bot player.
type Agent struct {
	ID       string
	Name     string
	Strategy Brain
}

// Play asks the agent to calculate its move based on the current game state.
func (a *Agent) Play(game *domain.Game) (Move, error) {
	// Find the seat index for this agent using direct map lookup
	player, ok := game.Players[a.ID]
	if !ok {
		// Agent is not part of this game
		return Move{Pass: true}, nil
	}
	
	return a.Strategy.CalculateMove(game, player.Seat - 1) // Domain seats are 1-based usually, check MatchState
}

// PlayAtSeat is a safer version if the Agent doesn't know its seat index automatically.
func (a *Agent) PlayAtSeat(game *domain.Game, seat int) (Move, error) {
	return a.Strategy.CalculateMove(game, seat)
}
