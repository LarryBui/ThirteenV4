package domain

import (
	"testing"
)

func TestIdentifyCombination(t *testing.T) {
	tests := []struct {
		name     string
		cards    []Card
		expected CardCombinationType
	}{
		{
			name:     "Single",
			cards:    []Card{{Rank: 0, Suit: 0}},
			expected: Single,
		},
		{
			name:     "Pair",
			cards:    []Card{{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}},
			expected: Pair,
		},
		{
			name:     "Triple",
			cards:    []Card{{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 0, Suit: 2}},
			expected: Triple,
		},
		{
			name:     "Quad",
			cards:    []Card{{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 0, Suit: 2}, {Rank: 0, Suit: 3}},
			expected: Bomb,
		},
		{
			name:     "Straight 3",
			cards:    []Card{{Rank: 0, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 2, Suit: 2}},
			expected: Straight,
		},
		{
			name:     "3 Consecutive Pairs",
			cards:    []Card{{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}},
			expected: Bomb,
		},
		{
			name:     "Invalid: Single 2 in Straight",
			cards:    []Card{{Rank: 10, Suit: 0}, {Rank: 11, Suit: 1}, {Rank: 12, Suit: 2}},
			expected: Invalid,
		},
		{
			name:     "Invalid: Consecutive Pairs with 2",
			cards:    []Card{{Rank: 11, Suit: 0}, {Rank: 11, Suit: 1}, {Rank: 12, Suit: 0}, {Rank: 12, Suit: 1}, {Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}},
			expected: Invalid,
		},
		{
			name:     "Invalid: Non-consecutive Pairs",
			cards:    []Card{{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}, {Rank: 3, Suit: 0}, {Rank: 3, Suit: 1}},
			expected: Invalid,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			combo := IdentifyCombination(tt.cards)
			if combo.Type != tt.expected {
				t.Errorf("expected %v, got %v", tt.expected, combo.Type)
			}
		})
	}
}

func TestCanBeat(t *testing.T) {
	tests := []struct {
		name     string
		prev     []Card
		new      []Card
		expected bool
	}{
		{
			name:     "Higher Single beats Lower Single",
			prev:     []Card{{Rank: 0, Suit: 0}},
			new:      []Card{{Rank: 0, Suit: 1}},
			expected: true,
		},
		{
			name:     "Higher Suit in Pair",
			prev:     []Card{{Rank: 5, Suit: 0}, {Rank: 5, Suit: 1}},
			new:      []Card{{Rank: 5, Suit: 2}, {Rank: 5, Suit: 3}},
			expected: true,
		},
		{
			name:     "3-Pine chops Single 2",
			prev:     []Card{{Rank: 12, Suit: 0}},
			new:      []Card{{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}},
			expected: true,
		},
		{
			name:     "Quad chops Single 2",
			prev:     []Card{{Rank: 12, Suit: 3}},
			new:      []Card{{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 0, Suit: 2}, {Rank: 0, Suit: 3}},
			expected: true,
		},
		{
			name:     "Quad chops Pair 2",
			prev:     []Card{{Rank: 12, Suit: 0}, {Rank: 12, Suit: 1}},
			new:      []Card{{Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 1, Suit: 2}, {Rank: 1, Suit: 3}},
			expected: true,
		},
		{
			name:     "Quad chops 3-Pine",
			prev:     []Card{{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}},
			new:      []Card{{Rank: 3, Suit: 0}, {Rank: 3, Suit: 1}, {Rank: 3, Suit: 2}, {Rank: 3, Suit: 3}},
			expected: true,
		},
		{
			name:     "4-Pine chops Single 2",
			prev:     []Card{{Rank: 12, Suit: 3}},
			new:      []Card{{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}, {Rank: 3, Suit: 0}, {Rank: 3, Suit: 1}},
			expected: true,
		},
		{
			name:     "4-Pine chops Quad",
			prev:     []Card{{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 0, Suit: 2}, {Rank: 0, Suit: 3}},
			new:      []Card{{Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}, {Rank: 3, Suit: 0}, {Rank: 3, Suit: 1}, {Rank: 4, Suit: 0}, {Rank: 4, Suit: 1}},
			expected: true,
		},
		{
			name:     "5-Pine chops 4-Pine",
			prev:     []Card{{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}, {Rank: 3, Suit: 0}, {Rank: 3, Suit: 1}},
			new:      []Card{{Rank: 4, Suit: 0}, {Rank: 4, Suit: 1}, {Rank: 5, Suit: 0}, {Rank: 5, Suit: 1}, {Rank: 6, Suit: 0}, {Rank: 6, Suit: 1}, {Rank: 7, Suit: 0}, {Rank: 7, Suit: 1}, {Rank: 8, Suit: 0}, {Rank: 8, Suit: 1}},
			expected: true,
		},
		{
			name:     "Higher 3-Pine beats Lower 3-Pine",
			prev:     []Card{{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}},
			new:      []Card{{Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}, {Rank: 3, Suit: 0}, {Rank: 3, Suit: 1}},
			expected: true,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := CanBeat(tt.prev, tt.new); got != tt.expected {
				t.Errorf("CanBeat() = %v, want %v", got, tt.expected)
			}
		})
	}
}
