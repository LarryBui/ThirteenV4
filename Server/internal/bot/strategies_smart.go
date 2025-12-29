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

	// 3. Phase-aware scoring with pass logic.
	phase := internal.DetectPhase(game)
	weights := smartBotTuning.ForPhase(phase)
	threat := internal.DetectThreat(game, seat, smartBotTuning.ThreatThreshold)
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
		if scored[0].Score < currentScore+smartBotTuning.PassThreshold {
			return Move{Pass: true}, nil
		}
	}

	return Move{Cards: scored[0].Move.Cards}, nil
}
