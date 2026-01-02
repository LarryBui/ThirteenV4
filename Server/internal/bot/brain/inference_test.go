package brain

import (
	"testing"
	"tienlen/internal/domain"
)

func TestEstimator_BossCards(t *testing.T) {
	m := NewMemory()
	e := NewEstimator(m)

	// Hand: 2 of Hearts (Highest card in game)
	twoHearts := domain.Card{Rank: 12, Suit: 3}
	hand := []domain.Card{twoHearts}

	if len(e.GetBossCards(hand)) != 1 {
		t.Errorf("2H should be a boss card")
	}
}

func TestEstimator_GetComboLikelihood(t *testing.T) {
	m := NewMemory()
	e := NewEstimator(m)

	// Setup opponent at seat 1
	p := NewOpponentProfile(1)
	p.CardsRemaining = 13
	m.Opponents[1] = p

	// Initially likelihood should be default high
	if e.GetComboLikelihood(1, domain.Straight) < 0.5 {
		t.Errorf("Initially should have high likelihood for straight")
	}

	// 1. Hard constraint check
	p.CardsRemaining = 2
	if e.GetComboLikelihood(1, domain.Straight) != 0.0 {
		t.Errorf("With 2 cards, likelihood of a straight must be 0")
	}

	// 2. Exhaustion check
	p.CardsRemaining = 10
	p.RecordPlay(domain.CardCombination{Type: domain.Straight})
	p.RecordPlay(domain.CardCombination{Type: domain.Straight})
	
	if e.GetComboLikelihood(1, domain.Straight) > 0.2 {
		t.Errorf("After 2 straights, likelihood should be very low, got %f", e.GetComboLikelihood(1, domain.Straight))
	}
}
