package bot

import (
	"tienlen/internal/domain"
)


// Move represents the decision made by the AI.
type Move struct {
	Pass  bool
	Cards []domain.Card
}

// Brain is the interface that all bot strategies must implement.
type Brain interface {
	CalculateMove(game *domain.Game, player *domain.Player) (Move, error)
	OnEvent(event interface{})
}
