package internal

import (
	"testing"
	"tienlen/internal/domain"
)

func TestGetValidMoves_Lead(t *testing.T) {
	// Hand: 3S, 3H, 4S, 5S, 6S (S=0, H=2, D=1, C=3) - Wait, in deck.go: S=0, D=1, H=2, C=3? 
	// Let's check cardPower: Rank*4 + Suit.
	
	hand := []domain.Card{
		{Rank: 0, Suit: 0}, // 3S
		{Rank: 0, Suit: 2}, // 3H
		{Rank: 1, Suit: 0}, // 4S
		{Rank: 2, Suit: 0}, // 5S
		{Rank: 3, Suit: 0}, // 6S
	}
	
	moves := GetValidMoves(hand, domain.CardCombination{Type: domain.Invalid})
	
	// Should find:
	// 5 singles
	// 1 pair (3S, 3H)
	// Straights: (3S, 4S, 5S), (4S, 5S, 6S), (3S, 4S, 5S, 6S)
	// (Note: generator might pick 3H for some straights, but we implemented it to pick first available)
	
	singlesCount := 0
	pairsCount := 0
	straightsCount := 0
	
	for _, m := range moves {
		combo := domain.IdentifyCombination(m.Cards)
		switch combo.Type {
		case domain.Single:
			singlesCount++
		case domain.Pair:
			pairsCount++
		case domain.Straight:
			straightsCount++
		}
	}
	
	if singlesCount != 5 {
		t.Errorf("Expected 5 singles, got %d", singlesCount)
	}
	if pairsCount != 1 {
		t.Errorf("Expected 1 pair, got %d", pairsCount)
	}
	if straightsCount != 3 {
		t.Errorf("Expected 3 straights, got %d", straightsCount)
	}
}

func TestGetValidMoves_BeatingSingle(t *testing.T) {
	hand := []domain.Card{
		{Rank: 0, Suit: 0}, // 3S
		{Rank: 5, Suit: 0}, // 8S
		{Rank: 12, Suit: 0}, // 2S
	}
	
	// Prev: 5S (Rank 2)
	prev := domain.CardCombination{
		Type:  domain.Single,
		Cards: []domain.Card{{Rank: 2, Suit: 0}},
	}
	
	moves := GetValidMoves(hand, prev)
	
	// Should beat 5S with 8S and 2S. 3S is too low.
	if len(moves) != 2 {
		t.Errorf("Expected 2 valid moves, got %d", len(moves))
	}
	
	for _, m := range moves {
		if m.Cards[0].Rank == 0 {
			t.Errorf("3S should not be able to beat 5S")
		}
	}
}

func TestGetValidMoves_Chop(t *testing.T) {
	// Hand has a Quad 4s
	hand := []domain.Card{
		{Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 1, Suit: 2}, {Rank: 1, Suit: 3},
	}
	
	// Prev: Single 2S
	prev := domain.CardCombination{
		Type:  domain.Single,
		Cards: []domain.Card{{Rank: 12, Suit: 0}},
	}
	
	moves := GetValidMoves(hand, prev)
	
	// Should find the Quad as a valid move (chopping)
	foundQuad := false
	for _, m := range moves {
		if len(m.Cards) == 4 {
			foundQuad = true
			break
		}
	}
	
	if !foundQuad {
		t.Errorf("Bot failed to find chopping move (Quad) against 2S")
	}
}
