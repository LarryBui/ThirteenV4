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

	// Hand: Ace of Hearts (Rank 11, Suit 3)
	aceHearts := domain.Card{Rank: 11, Suit: 3}
	hand = []domain.Card{aceHearts}
	
	// Initially not a boss because 2s are unknown
	if len(e.GetBossCards(hand)) != 0 {
		t.Errorf("AH should not be a boss if 2s are unknown")
	}

	// Mark all 2s as played
	m.MarkPlayed([]domain.Card{
		{Rank: 12, Suit: 0},
		{Rank: 12, Suit: 1},
		{Rank: 12, Suit: 2},
		{Rank: 12, Suit: 3},
	})

	if len(e.GetBossCards(hand)) != 1 {
		t.Errorf("AH should be a boss if all 2s are played")
	}
}

func TestEstimator_LeadProbability(t *testing.T) {
	m := NewMemory()
	e := NewEstimator(m)

	// Highest card
	twoHearts := domain.Card{Rank: 12, Suit: 3}
	if e.LeadTurnProbability(twoHearts) != 1.0 {
		t.Errorf("2H probability should be 1.0")
	}

	// 3 of Spades with many unknown higher cards
	threeSpades := domain.Card{Rank: 0, Suit: 0}
	prob := e.LeadTurnProbability(threeSpades)
	if prob >= 0.5 {
		t.Errorf("3S probability should be very low, got %f", prob)
	}
}

func TestEstimator_CalculateDominance(t *testing.T) {
	m := NewMemory()
	e := NewEstimator(m)

	// Hand: All 2s
	hand := []domain.Card{
		{Rank: 12, Suit: 0},
		{Rank: 12, Suit: 1},
		{Rank: 12, Suit: 2},
		{Rank: 12, Suit: 3},
	}
	m.MarkMine(hand)

	dom := e.CalculateDominance(hand)
	if dom <= 0.5 {
		t.Errorf("Hand with all 2s should have high dominance, got %f", dom)
	}

	// Hand: All 3s
	handLow := []domain.Card{
		{Rank: 0, Suit: 0},
		{Rank: 0, Suit: 1},
		{Rank: 0, Suit: 2},
		{Rank: 0, Suit: 3},
	}
	m.Reset()
	m.MarkMine(handLow)
	domLow := e.CalculateDominance(handLow)
	if domLow >= 0.5 {
		t.Errorf("Hand with all 3s should have low dominance, got %f", domLow)
	}
}