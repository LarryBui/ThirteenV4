package bot

import (
	"sort"
	"tienlen/internal/bot/internal"
	"tienlen/internal/domain"
)

type StandardBot struct{}

func (b *StandardBot) CalculateMove(game *domain.Game, player *domain.Player) (Move, error) {
	// 1. Identify Context
	if player == nil || len(player.Hand) == 0 {
		return Move{Pass: true}, nil
	}

	// 2. Generate all valid moves
	lastCombo := game.LastPlayedCombination
	validMoves := internal.GetValidMoves(player.Hand, lastCombo)

	if len(validMoves) == 0 {
		return Move{Pass: true}, nil
	}

	// 3. Phase-aware scoring with pass logic.
	phase := internal.DetectPhase(game)
	weights := DefaultTuning.ForPhase(phase)
	
	// Deterministic threat detection
	threat := false
	if game != nil && DefaultTuning.ThreatThreshold > 0 {
		// Iterate seats 0-3 deterministically
		for i := 0; i < 4; i++ {
			if i == player.Seat {
				continue
			}
			
			// Find player at this seat
			var opponent *domain.Player
			for _, p := range game.Players {
				if p.Seat == i {
					opponent = p
					break
				}
			}
			
			if opponent != nil && !opponent.Finished && len(opponent.Hand) > 0 && len(opponent.Hand) <= DefaultTuning.ThreatThreshold {
				threat = true
				break
			}
		}
	}

	scored := internal.BuildScoredMoves(player.Hand, validMoves, weights, threat)

	sort.Slice(scored, func(i, j int) bool {
		if scored[i].Score != scored[j].Score {
			return scored[i].Score > scored[j].Score
		}
		// Save higher cards when scores are equal.
		return scored[i].Combo.Value < scored[j].Combo.Value
	})

	if lastCombo.Type != domain.Invalid {
		currentScore := internal.ScoreHand(player.Hand, weights)
		if scored[0].Score < currentScore+DefaultTuning.PassThreshold {
			return Move{Pass: true}, nil
		}
	}

	return Move{Cards: scored[0].Move.Cards}, nil
}
