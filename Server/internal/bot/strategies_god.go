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

	// 2. Card Counting & Analysis
	stats := internal.AnalyzeHand(player.Hand, game.Discards)

	// 3. Determine if we are in Blocker Mode
	nextPlayerLowCards := false
	for _, p := range game.Players {
		if !p.Finished && !p.HasPassed && p.Seat-1 != seat {
			if len(p.Hand) <= 3 {
				nextPlayerLowCards = true
				break
			}
		}
	}

	// 4. Generate & Score Moves
	validMoves := internal.GetValidMoves(player.Hand, game.LastPlayedCombination)
	if len(validMoves) == 0 {
		return Move{Pass: true}, nil
	}

	currentScore := internal.EvaluateHand(player.Hand)
	
	type scoredMove struct {
		move  internal.ValidMove
		delta float64
		power int32
		isBoss bool
	}

	var candidates []scoredMove
	for _, m := range validMoves {
		remaining := domain.RemoveCards(player.Hand, m.Cards)
		newScore := internal.EvaluateHand(remaining)
		
		delta := newScore - currentScore
		combo := domain.IdentifyCombination(m.Cards)
		
		// Boss Check
		isBoss := false
		if combo.Type == domain.Single {
			for _, bc := range stats.BossSingles {
				if bc == m.Cards[0] {
					isBoss = true
					break
				}
			}
		}

		// --- God Logic: Killer Instinct ---
		if len(remaining) == 0 {
			delta += 10000.0
		}

		// --- God Logic: Strategic Boss Usage ---
		// If we have dominance, prefer playing non-boss cards to save power, 
		// UNLESS we are blocking or finishing.
		if isBoss {
			if stats.Dominance > 0.6 && !nextPlayerLowCards && len(player.Hand) > 3 {
				delta -= 10.0 // Save the boss card for later
			} else {
				delta += 20.0 // Seize control
			}
		}

		// --- God Logic: Blocker Mode ---
		if nextPlayerLowCards {
			if combo.Type == domain.Single && combo.Value < 40 {
				delta -= 100.0 // Heavy penalty for feeding low cards
			}
			if isBoss {
				delta += 50.0 // Prefer playing boss to block
			}
		}

		candidates = append(candidates, scoredMove{
			move:   m,
			delta:  delta,
			power:  combo.Value,
			isBoss: isBoss,
		})
	}

	// 5. Selection
	sort.Slice(candidates, func(i, j int) bool {
		if candidates[i].delta != candidates[j].delta {
			return candidates[i].delta > candidates[j].delta
		}
		if nextPlayerLowCards || candidates[i].isBoss {
			return candidates[i].power > candidates[j].power
		}
		return candidates[i].power < candidates[j].power
	})

	// Pass Logic: Don't pass if we have a "Boss" card that can win the round
	if game.LastPlayedCombination.Type != domain.Invalid {
		if candidates[0].delta < -15.0 && !candidates[0].isBoss {
			return Move{Pass: true}, nil
		}
	}

	return Move{Cards: candidates[0].move.Cards}, nil
}
