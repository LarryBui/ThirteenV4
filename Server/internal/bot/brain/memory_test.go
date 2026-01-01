package brain

import (
	"testing"
	"tienlen/internal/domain"
)

func TestGameMemory(t *testing.T) {
	m := NewMemory()

	// Initial state
	for i := 0; i < 52; i++ {
		if m.DeckStatus[i] != StatusUnknown {
			t.Errorf("Index %d should be Unknown, got %d", i, m.DeckStatus[i])
		}
	}

	// Mark Mine: 3 of Spades (0,0)
	threeSpades := domain.Card{Rank: 0, Suit: 0}
	m.MarkMine([]domain.Card{threeSpades})
	if m.DeckStatus[0] != StatusMine {
		t.Errorf("3S should be StatusMine")
	}

	// Reset
	m.Reset()
	if m.DeckStatus[0] != StatusUnknown {
		t.Errorf("After reset, 3S should be StatusUnknown")
	}
}

func TestGameMemory_OpponentModeling(t *testing.T) {
	m := NewMemory()

	// 1. Table has a Pair of 10s
	pair10s := []domain.Card{
		{Rank: 7, Suit: 0},
		{Rank: 7, Suit: 1},
	}
	m.UpdateTable(pair10s)
	
	if m.CurrentCombo.Type != domain.Pair {
		t.Errorf("CurrentCombo should be Pair")
	}

	// 2. Seat 1 passes
	m.RecordPass(1)

	profile, ok := m.Opponents[1]
	if !ok {
		t.Fatal("Opponent profile not created")
	}

	if _, exists := profile.Weaknesses[domain.Pair]; !exists {
		t.Errorf("Weakness for Pair should exist")
	}
	
	if profile.CanPossiblyBeat(domain.IdentifyCombination(pair10s)) {
		t.Errorf("Should NOT possibly beat what they just passed on")
	}

	// 3. Table updated to Pair of Queens
	pairQs := []domain.Card{
		{Rank: 9, Suit: 0},
		{Rank: 9, Suit: 1},
	}
	oldValue := profile.Weaknesses[domain.Pair]
	m.UpdateTable(pairQs)
	m.RecordPass(1)

	if profile.Weaknesses[domain.Pair] <= oldValue {
		t.Errorf("Weakness should have increased to Pair of Queens value")
	}
}
