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

func (b *StandardBot) OnEvent(event interface{}, isRecipient bool) {
	if b.Memory == nil {
		return
	}

	// Initialize Estimator if missing
	if b.Estimator == nil {
		b.Estimator = brain.NewEstimator(b.Memory)
	}

	switch e := event.(type) {
	case app.CardPlayedPayload:
		b.Memory.UpdateTable(e.Cards)
		b.Memory.RecordPlay(e.Seat, e.Cards)
	case app.TurnPassedPayload:
		b.Memory.RecordPass(e.Seat)
		if e.NewRound {
			b.Memory.UpdateTable(nil)
		}
	case app.GameStartedPayload:
		b.Memory.Reset()
		if isRecipient {
			b.Memory.MarkMine(e.Hand)
		}
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

	// Tactical Hand Organization (Multi-Strategy)
	options := internal.GetTacticalOptions(player.Hand)

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

	// 4. Apply State-Aware reasoning (Boss Bonus / Lead Chance / Opponent Safety / Tactical Protection)
	for i := range scored {
		// Penalty for breaking tactical structures
		// We use the MINIMUM penalty across all tactical options. 
		// If a move fits ANY valid strategy, it shouldn't be penalized.
		minPenalty := 100000.0
		
		for _, opt := range options {
			currentPenalty := 0.0
			if isBreakingBomb(scored[i].Move.Cards, opt.Bombs) {
				currentPenalty += 1000.0 // Protect Nukes
			}
			if isBreakingStraight(scored[i].Move.Cards, opt.Straights) {
				currentPenalty += 50.0 // Protect fragile straights
			}
			if currentPenalty < minPenalty {
				minPenalty = currentPenalty
			}
		}
		scored[i].Score -= minPenalty

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

			// Opponent Modeling: Is this move safe from next players?
			safety := b.Estimator.IsSafeFromNextPlayers(scored[i].Combo, player.Seat)
			scored[i].Score += safety * 25.0 // Reward safe plays

			// Dominance: Are we capitalizing on a known ceiling?
			dominance := b.Estimator.GetDominanceScore(scored[i].Combo, player.Seat)
			scored[i].Score += dominance * 30.0

			// Strategic Leading: Favor types the NEXT player is likely exhausted of
			nextSeat := (player.Seat + 1) % 4
			likelihood := b.Estimator.GetComboLikelihood(nextSeat, scored[i].Combo.Type)
			scored[i].Score += (1.0 - likelihood) * 10.0 // Reward "blocking" plays
		}

		// Reward Chopping High Value Targets (Pigs/Bombs)
		// We use domain.DetectChop to see if this move qualifies as a special capture.
		if isChop, _ := domain.DetectChop(lastCombo.Cards, scored[i].Move.Cards); isChop {
			scored[i].Score += 500.0 // Massive priority to chop pigs/bombs
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

func isBreakingBomb(moveCards []domain.Card, bombs []domain.CardCombination) bool {
	for _, b := range bombs {
		contains := 0
		for _, mc := range moveCards {
			for _, bc := range b.Cards {
				if mc == bc {
					contains++
					break
				}
			}
		}
		// If move uses SOME cards of the bomb but not ALL, it breaks the bomb.
		if contains > 0 && contains < len(b.Cards) {
			return true
		}
	}
	return false
}

func isBreakingStraight(moveCards []domain.Card, straights []domain.CardCombination) bool {
	for _, s := range straights {
		contains := 0
		for _, mc := range moveCards {
			for _, sc := range s.Cards {
				if mc == sc {
					contains++
					break
				}
			}
		}
		if contains > 0 && contains < len(s.Cards) {
			return true
		}
	}
	return false
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
