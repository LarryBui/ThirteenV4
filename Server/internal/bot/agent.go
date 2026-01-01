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
	
	move, err := a.Strategy.CalculateMove(game, player)
	if err != nil {
		return Move{Pass: true}, err
	}
	return move, nil
}

// PlayAtSeat is a safer version if the Agent doesn't know its seat index automatically.
func (a *Agent) PlayAtSeat(game *domain.Game, seat int) (Move, error) {
	var player *domain.Player
	for _, p := range game.Players {
		if p.Seat == seat {
			player = p
			break
		}
	}
	if player == nil {
		return Move{Pass: true}, nil
	}
	return a.Strategy.CalculateMove(game, player)
}

// OnGameEvent notifies the agent of a game event.
func (a *Agent) OnGameEvent(event interface{}) {
	a.Strategy.OnEvent(event)
}
