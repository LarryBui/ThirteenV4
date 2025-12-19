package bot

import (
	"tienlen/internal/domain"
)

// Move represents the decision made by the AI.
type Move struct {
	Pass  bool
	Cards []domain.Card
}

// CalculateMove determines the best move for a bot given the current game state.
func CalculateMove(game *domain.Game, seat int) (Move, error) {
	// Find player in game
	var player *domain.Player
	for _, p := range game.Players {
		if p.Seat-1 == seat {
			player = p
			break
		}
	}

	if player == nil || len(player.Hand) == 0 {
		return Move{Pass: true}, nil
	}

	// If it's a new round, start with the lowest card.
	if game.LastPlayedCombination.Type == domain.Invalid {
		return Move{
			Cards: []domain.Card{player.Hand[0]},
		}, nil
	}

	// Try to beat the current combination with the same number of cards.
	prevCards := game.LastPlayedCombination.Cards
	hand := player.Hand

	// Simple AI: Try to find a single card that beats the previous play
	if len(prevCards) == 1 {
		for _, card := range hand {
			if domain.CanBeat(prevCards, []domain.Card{card}) {
				return Move{Cards: []domain.Card{card}}, nil
			}
		}
	}

	// Simple AI: Try to find a pair that beats the previous play
	if len(prevCards) == 2 {
		for i := 0; i < len(hand)-1; i++ {
			for j := i + 1; j < len(hand); j++ {
				pair := []domain.Card{hand[i], hand[j]}
				if domain.IsValidSet(pair) && domain.CanBeat(prevCards, pair) {
					return Move{Cards: pair}, nil
				}
			}
		}
	}
    
    // Simple AI: Try to find a triple that beats the previous play
	if len(prevCards) == 3 {
		for i := 0; i < len(hand)-2; i++ {
			for j := i + 1; j < len(hand)-1; j++ {
                for k := j + 1; k < len(hand); k++ {
                    triple := []domain.Card{hand[i], hand[j], hand[k]}
                    if domain.IsValidSet(triple) && domain.CanBeat(prevCards, triple) {
                        return Move{Cards: triple}, nil
                    }
                }
			}
		}
	}

	// If no simple move found, pass.
	return Move{Pass: true}, nil
}
