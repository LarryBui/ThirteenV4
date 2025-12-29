package bot

import (
	"sort"
	"tienlen/internal/bot/internal"
	"tienlen/internal/domain"
)

type GoodBot struct{}

func (b *GoodBot) CalculateMove(game *domain.Game, seat int) (Move, error) {
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

	// 2. Generate moves
	validMoves := internal.GetValidMoves(player.Hand, game.LastPlayedCombination)

	if len(validMoves) == 0 {
		return Move{Pass: true}, nil
	}

	// 3. Phase-aware scoring with conservative weights.
	phase := internal.DetectPhase(game)
	weights := goodBotTuning.ForPhase(phase)
	threat := internal.DetectThreat(game, seat, goodBotTuning.ThreatThreshold)
	scored := internal.BuildScoredMoves(player.Hand, validMoves, weights, threat)

	sort.Slice(scored, func(i, j int) bool {
		if scored[i].Score != scored[j].Score {
			return scored[i].Score > scored[j].Score
		}
		// Prefer the lowest-value combo when scores tie.
		return scored[i].Combo.Value < scored[j].Combo.Value
	})

	return Move{Cards: scored[0].Move.Cards}, nil
}
