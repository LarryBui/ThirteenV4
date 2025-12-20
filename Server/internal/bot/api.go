package bot

import (
	"tienlen/internal/domain"
)

// BotLevel defines the intelligence level of the AI.
type BotLevel int

const (
	BotLevelGood BotLevel = iota
	BotLevelSmart
	BotLevelGod
)

// Move represents the decision made by the AI.
type Move struct {
	Pass  bool
	Cards []domain.Card
}

// Brain is the interface that all bot strategies must implement.
type Brain interface {
	CalculateMove(game *domain.Game, seat int) (Move, error)
}
