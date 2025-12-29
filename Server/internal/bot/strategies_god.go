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
		if p.Seat == seat {
			player = p
			break
		}
	}
	if player == nil || len(player.Hand) == 0 {
		return Move{Pass: true}, nil
	}

	// 2. Card Counting & Analysis
	stats := internal.AnalyzeHand(player.Hand, game.Discards)

	// 3. Determine if we are in Blocker Mode (tighter threshold than endgame).
	blockerMode := internal.DetectThreat(game, seat, godBotTuning.ThreatThreshold)

	// 4. Generate & Score Moves
	validMoves := internal.GetValidMoves(player.Hand, game.LastPlayedCombination)
	if len(validMoves) == 0 {
		return Move{Pass: true}, nil
	}

	// 5. Phase-aware scoring with god-level adjustments.
	phase := internal.DetectPhase(game)
	weights := godBotTuning.ForPhase(phase)
	scored := internal.BuildScoredMoves(player.Hand, validMoves, weights, blockerMode)
	currentScore := internal.ScoreHand(player.Hand, weights)

	type scoredMove struct {
		base   internal.ScoredMove
		isBoss bool
	}

	candidates := make([]scoredMove, 0, len(scored))
	for _, m := range scored {
		isBoss := false
		if m.Combo.Type == domain.Single && len(m.Move.Cards) == 1 {
			for _, bc := range stats.BossSingles {
				if bc == m.Move.Cards[0] {
					isBoss = true
					break
				}
			}
		}

		if len(m.Remaining) == 0 {
			m.Score += 10000.0
		}

		if isBoss {
			if stats.Dominance > 0.6 && !blockerMode && len(player.Hand) > 3 {
				m.Score -= 10.0
			} else {
				m.Score += 20.0
			}
		}

		if blockerMode {
			if m.Combo.Type == domain.Single && m.Combo.Value < 40 {
				m.Score -= 100.0
			}
			if isBoss {
				m.Score += 50.0
			}
		}

		candidates = append(candidates, scoredMove{
			base:   m,
			isBoss: isBoss,
		})
	}

	sort.Slice(candidates, func(i, j int) bool {
		if candidates[i].base.Score != candidates[j].base.Score {
			return candidates[i].base.Score > candidates[j].base.Score
		}
		if blockerMode || candidates[i].isBoss {
			return candidates[i].base.Combo.Value > candidates[j].base.Combo.Value
		}
		return candidates[i].base.Combo.Value < candidates[j].base.Combo.Value
	})

	if game.LastPlayedCombination.Type != domain.Invalid {
		if !blockerMode && candidates[0].base.Score < currentScore+godBotTuning.PassThreshold && !candidates[0].isBoss {
			return Move{Pass: true}, nil
		}
	}

	return Move{Cards: candidates[0].base.Move.Cards}, nil
}
