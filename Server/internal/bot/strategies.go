package bot

import (
	"sort"
	"tienlen/internal/app"
	"tienlen/internal/bot/brain"
	"tienlen/internal/bot/internal"
	"tienlen/internal/domain"
)

type StandardBot struct {
	Memory    *brain.GameMemory
	Estimator *brain.Estimator
}

func (b *StandardBot) OnEvent(event interface{}) {
	if b.Memory == nil {
		return
	}

	// Initialize Estimator if missing
	if b.Estimator == nil {
		b.Estimator = brain.NewEstimator(b.Memory)
	}

	switch e := event.(type) {
	case app.CardPlayedPayload:
		b.Memory.MarkPlayed(e.Cards)
	case app.GameStartedPayload:
		b.Memory.Reset()
	case app.GameEndedPayload:
		b.Memory.Reset()
	}
}

func (b *StandardBot) CalculateMove(game *domain.Game, player *domain.Player) (Move, error) {
	// 1. Identify Context
	if player == nil || len(player.Hand) == 0 {
		return Move{Pass: true}, nil
	}

	// Sync Memory with current hand
	if b.Memory != nil {
		b.Memory.UpdateHand(player.Hand)
	}
	if b.Estimator == nil && b.Memory != nil {
		b.Estimator = brain.NewEstimator(b.Memory)
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
		for i := 0; i < 4; i++ {
			if i == player.Seat {
				continue
			}
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

	// 4. Apply State-Aware reasoning (Boss Bonus / Lead Chance)
	for i := range scored {
		if b.Estimator != nil {
			// Bonus for Boss cards
			if len(scored[i].Move.Cards) == 1 && b.Memory.IsBoss(scored[i].Move.Cards[0]) {
				scored[i].Score += 50.0 // Significant boost for unbeatable singles
			}

			// Adjust score based on lead-turning probability for singles
			if len(scored[i].Move.Cards) == 1 {
				prob := b.Estimator.LeadTurnProbability(scored[i].Move.Cards[0])
				scored[i].Score += prob * 10.0
			}
		}
	}

	sort.Slice(scored, func(i, j int) bool {
		if scored[i].Score != scored[j].Score {
			return scored[i].Score > scored[j].Score
		}
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

// DumpState returns a debug summary of the bot's internal state.
func (b *StandardBot) DumpState() string {
	if b.Memory == nil || b.Estimator == nil {
		return "Brain not initialized"
	}
	
	playedCount := 0
	mineCount := 0
	for _, status := range b.Memory.DeckStatus {
		if status == brain.StatusPlayed {
			playedCount++
		} else if status == brain.StatusMine {
			mineCount++
		}
	}
	
	return "State: Played=" + string(rune('0'+playedCount/10)) + string(rune('0'+playedCount%10)) + 
		   ", Hand=" + string(rune('0'+mineCount/10)) + string(rune('0'+mineCount%10))
}
