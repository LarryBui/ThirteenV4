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
