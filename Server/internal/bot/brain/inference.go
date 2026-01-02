package brain

import (
	"tienlen/internal/domain"
)

// Estimator provides probabilistic insights based on memory.
type Estimator struct {
	Memory *GameMemory
}

// NewEstimator creates a new reasoning engine.
func NewEstimator(m *GameMemory) *Estimator {
	return &Estimator{Memory: m}
}

// GetBossCards returns a list of cards in the bot's hand that are currently unbeatable.
func (e *Estimator) GetBossCards(hand []domain.Card) []domain.Card {
	var bossCards []domain.Card
	for _, c := range hand {
		if e.Memory.IsBoss(c) {
			bossCards = append(bossCards, c)
		}
	}
	return bossCards
}

// LeadTurnProbability returns a 0.0 to 1.0 chance that playing this single card 
// will eventually win the turn back (i.e. no one can beat it or we have the boss).
func (e *Estimator) LeadTurnProbability(c domain.Card) float64 {
	if e.Memory.IsBoss(c) {
		return 1.0
	}

	// Count how many cards are higher and where they are
	higherUnknown := 0
	higherTotal := 0
	idx := cardToIndex(c)
	
	for i := idx + 1; i < 52; i++ {
		status := e.Memory.DeckStatus[i]
		if status == StatusUnknown || status == StatusOpponent {
			higherUnknown++
			higherTotal++
		} else if status == StatusPlayed {
			higherTotal++
		}
	}

	if higherUnknown == 0 {
		return 1.0
	}

	// Simple heuristic: inverse of unknown higher cards
	// If 1 unknown higher card exists, chance is roughly 0.5 (if 2 players)
	// This will be refined with opponent modeling later.
	return 1.0 / float64(higherUnknown+1)
}

// CalculateDominance returns a 0.0 to 1.0 score representing the bot's hand strength 
// relative to all unknown cards in the game.
func (e *Estimator) CalculateDominance(hand []domain.Card) float64 {
	if len(hand) == 0 {
		return 0.0
	}

	handPower := 0.0
	for _, c := range hand {
		handPower += float64(cardToIndex(c))
	}
	avgHandPower := handPower / float64(len(hand))

	unknownPower := 0.0
	unknownCount := 0
	for i, status := range e.Memory.DeckStatus {
		if status == StatusUnknown || status == StatusOpponent {
			unknownPower += float64(i)
			unknownCount++
		}
	}

	if unknownCount == 0 {
		return 1.0
	}

	avgUnknownPower := unknownPower / float64(unknownCount)
	return avgHandPower / (avgHandPower + avgUnknownPower)
}

// IsSafeFromNextPlayers returns 1.0 if the next active players are known to be unable 
// to beat the given combo based on their pass history.
func (e *Estimator) IsSafeFromNextPlayers(combo domain.CardCombination, mySeat int) float64 {
	if combo.Type == domain.Invalid {
		return 0.0
	}

	// We check the next seats in order (1, 2, 3 seats away)
	// If the immediate next player is "weak" against this combo, it's safer.
	safety := 0.0
	checked := 0

	for i := 1; i <= 3; i++ {
		nextSeat := (mySeat + i) % 4
		profile, ok := e.Memory.Opponents[nextSeat]
		if !ok {
			continue // No data on this player
		}

		checked++
		if !profile.CanPossiblyBeat(combo) {
			safety += 1.0
		} else {
			// If even one person can possibly beat it, safety drops
			// In a more complex version, we'd use probabilities here.
			break 
		}
	}

	if checked == 0 {
		return 0.0
	}

	return safety / float64(checked)
}

// GetComboLikelihood returns a 0.0 to 1.0 estimate of the probability that 
// an opponent at the specified seat still has a specific combo type.
func (e *Estimator) GetComboLikelihood(seat int, comboType domain.CardCombinationType) float64 {
	p, ok := e.Memory.Opponents[seat]
	if !ok {
		return 0.5 // Unknown
	}

	// 1. Hard Constraints (Physical impossibility)
	switch comboType {
	case domain.Straight:
		if p.CardsRemaining < 3 {
			return 0.0
		}
	case domain.Pair:
		if p.CardsRemaining < 2 {
			return 0.0
		}
	case domain.Triple:
		if p.CardsRemaining < 3 {
			return 0.0
		}
	case domain.Bomb:
		if p.CardsRemaining < 4 {
			return 0.0
		}
	}

	// 2. Exhaustion Heuristics (Typical hand structure limits)
	played := p.PlayedStats[comboType]
	
	switch comboType {
	case domain.Straight:
		if played >= 2 {
			return 0.1 // Rare to have 3 straights in 13 cards
		}
		if played == 1 {
			return 0.3
		}
	case domain.Pair:
		if played >= 4 {
			return 0.1
		}
		if played >= 3 {
			return 0.4
		}
	case domain.Bomb:
		if played >= 1 {
			return 0.05 // Very rare to have 2 bombs
		}
	}

	return 0.7 // Default high likelihood if few have been played
}
