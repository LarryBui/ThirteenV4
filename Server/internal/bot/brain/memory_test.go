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

	// Mark Played: 3 of Spades
	m.MarkPlayed([]domain.Card{threeSpades})
	if m.DeckStatus[0] != StatusPlayed {
		t.Errorf("3S should be StatusPlayed")
	}
	if !m.IsPlayed(threeSpades) {
		t.Errorf("IsPlayed(3S) should be true")
	}

	// Reset
	m.Reset()
	if m.DeckStatus[0] != StatusUnknown {
		t.Errorf("After reset, 3S should be StatusUnknown")
	}
}
