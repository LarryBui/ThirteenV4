package bot

import (
	"sort"
	"tienlen/internal/bot/internal"
	"tienlen/internal/domain"
)

type SmartBot struct{}

func (b *SmartBot) CalculateMove(game *domain.Game, seat int) (Move, error) {
	// 1. Identify Context
	var player *domain.Player
	for _, p := range game.Players {
		if p.Seat == seat {
			player = p
			break
		}
	}
	if player == nil || len(player.Hand) == 0 {
		return Move{Pass: true}, nil
	}

	// 2. Generate all valid moves
	lastCombo := game.LastPlayedCombination
	validMoves := internal.GetValidMoves(player.Hand, lastCombo)

	if len(validMoves) == 0 {
		return Move{Pass: true}, nil
	}

	// 3. Evaluation
	currentScore := internal.EvaluateHand(player.Hand)
	
	type scoredMove struct {
		move  internal.ValidMove
		delta float64
		power int32 // Highest card power of the move (for tie-breaking)
	}

	var candidates []scoredMove
	for _, m := range validMoves {
		remaining := domain.RemoveCards(player.Hand, m.Cards)
		newScore := internal.EvaluateHand(remaining)
		
		// If the move finishes the hand, it gets a massive bonus
		if len(remaining) == 0 {
			newScore += 1000.0
		}

		candidates = append(candidates, scoredMove{
			move:  m,
			delta: newScore - currentScore,
			power: domain.IdentifyCombination(m.Cards).Value,
		})
	}

	// 4. Selection
	sort.Slice(candidates, func(i, j int) bool {
		// Primary: Highest delta (best resulting hand)
		if candidates[i].delta != candidates[j].delta {
			return candidates[i].delta > candidates[j].delta
		}
		// Secondary: Lowest card power (save high cards if hand quality is equal)
		return candidates[i].power < candidates[j].power
	})

	// Optional: Heuristic to decide if Passing is better than playing (only when responding)
	// If the best move has a very negative delta (e.g. breaking a bomb to beat a single),
	// the smart bot might prefer to Pass.
	if lastCombo.Type != domain.Invalid {
		bestDelta := candidates[0].delta
		// If playing our best card actually makes our hand much worse (e.g. breaking a straight or wasting a 2 on a 3),
		// we consider passing.
		// -15.0 is roughly the cost of a pig or a straight.
		if bestDelta < -15.0 {
			return Move{Pass: true}, nil
		}
	}

	return Move{Cards: candidates[0].move.Cards}, nil
}
