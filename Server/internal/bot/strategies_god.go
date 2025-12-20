package bot

import (
	"sort"
	"tienlen/internal/bot/internal"
	"tienlen/internal/domain"
)

type GodBot struct {
	SmartBot // Inherit basic smart logic for composition if helpful, or just reuse helpers
}

func (b *GodBot) CalculateMove(game *domain.Game, seat int) (Move, error) {
	// 1. Identify Context
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

	// 2. Determine if we are in Blocker Mode
	nextPlayerLowCards := false
	for _, p := range game.Players {
		if !p.Finished && !p.HasPassed && p.Seat-1 != seat {
			// Is this the next player? (Simplified: check if their hand is small)
			if len(p.Hand) <= 3 {
				nextPlayerLowCards = true
				break
			}
		}
	}

	// 2. Generate Moves
	validMoves := internal.GetValidMoves(player.Hand, game.LastPlayedCombination)
	if len(validMoves) == 0 {
		return Move{Pass: true}, nil
	}

	// 3. Score Moves (Reuse Smart Logic components)
	currentScore := internal.EvaluateHand(player.Hand)
	
	type scoredMove struct {
		move  internal.ValidMove
		delta float64
		power int32
	}

	var candidates []scoredMove
	for _, m := range validMoves {
		remaining := domain.RemoveCards(player.Hand, m.Cards)
		newScore := internal.EvaluateHand(remaining)
		
		delta := newScore - currentScore
		
		// --- God Logic: Killer Instinct ---
		// If move finishes game, infinite score.
		if len(remaining) == 0 {
			delta += 10000.0
		}

		// --- God Logic: Blocker Mode ---
		// If opponents are low on cards, PENALIZE playing low cards.
		// We want to force high cards to block them.
		combo := domain.IdentifyCombination(m.Cards)
		if nextPlayerLowCards {
			// If we play a low card (Rank < 10 i.e. < King), penalty
			// Unless it's a combo (pair/triple), which is harder to beat.
			// Mostly applies to Singles.
			if combo.Type == domain.Single && combo.Value < 40 { // Roughly Rank < 10
				delta -= 50.0 // Big penalty for feeding low cards
			}
		}

		candidates = append(candidates, scoredMove{
			move:  m,
			delta: delta,
			power: combo.Value,
		})
	}

	// 4. Selection
	sort.Slice(candidates, func(i, j int) bool {
		if candidates[i].delta != candidates[j].delta {
			return candidates[i].delta > candidates[j].delta
		}
		// Tie-breaker:
		// If in Blocker Mode, prefer HIGHER power (keep pressure).
		// Otherwise, prefer LOWER power (save cards).
		if nextPlayerLowCards {
			return candidates[i].power > candidates[j].power
		}
		return candidates[i].power < candidates[j].power
	})

	// Pass Logic (similar to Smart)
	if game.LastPlayedCombination.Type != domain.Invalid {
		if candidates[0].delta < -15.0 {
			return Move{Pass: true}, nil
		}
	}

	return Move{Cards: candidates[0].move.Cards}, nil
}
