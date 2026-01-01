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

	if !e.Memory.IsBoss(twoHearts) {
		// All other cards are StatusUnknown, but there are NO cards higher than 2H.
		// Wait, rank 12 suit 3 is the absolute max. cardToIndex(2H) = 12*4 + 3 = 51.
		// The loop i := idx + 1; i < 52 will not run.
	}

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
