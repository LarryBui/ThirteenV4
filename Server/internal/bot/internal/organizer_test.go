package internal

import (
	"testing"
	"tienlen/internal/domain"
)

func TestOrganizer_ExtractBombs(t *testing.T) {
	// Hand: Quad 4s, 3-Pine (55, 66, 77), and some trash
	hand := []domain.Card{
		{Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 1, Suit: 2}, {Rank: 1, Suit: 3}, // 4s (Quad)
		{Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}, // 5s
		{Rank: 3, Suit: 0}, {Rank: 3, Suit: 1}, // 6s
		{Rank: 4, Suit: 0}, {Rank: 4, Suit: 1}, // 7s (part of 3-pine 5-6-7)
		{Rank: 10, Suit: 0}, // K (Trash)
	}
	domain.SortHand(hand)

	bombs, remaining := ExtractBombs(hand)

	if len(bombs) != 2 {
		t.Errorf("Expected 2 bombs, got %d", len(bombs))
	}

	if len(remaining) != 1 {
		t.Errorf("Expected 1 card remaining, got %d: %v", len(remaining), remaining)
	}
}

func TestOrganizer_ExtractStraights(t *testing.T) {
	// Hand: 3, 4, 5, 6, 7 (Straight), 9, 9 (Pair)
	hand := []domain.Card{
		{Rank: 0, Suit: 0}, // 3
		{Rank: 1, Suit: 1}, // 4
		{Rank: 2, Suit: 2}, // 5
		{Rank: 3, Suit: 3}, // 6
		{Rank: 4, Suit: 0}, // 7
		{Rank: 6, Suit: 0}, // 9
		{Rank: 6, Suit: 1}, // 9
	}
	domain.SortHand(hand)

	straights, remaining := ExtractStraights(hand)

	if len(straights) != 1 {
		t.Errorf("Expected 1 straight, got %d", len(straights))
	}

	if straights[0].Count != 5 {
		t.Errorf("Expected straight length 5, got %d", straights[0].Count)
	}

	if len(remaining) != 2 {
		t.Errorf("Expected 2 cards remaining (9s), got %d", len(remaining))
	}
}

func TestOrganizer_PartitionHand(t *testing.T) {
	// Hand:
	// 3, 3, 3, 3 (Quad) - Rank 0
	// 5, 6, 7 (Straight) - Rank 2, 3, 4
	// 9, 9, 9 (Triple) - Rank 6
	// Jack, Jack (Pair) - Rank 8
	// King (Trash) - Rank 10
	hand := []domain.Card{
		{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 0, Suit: 2}, {Rank: 0, Suit: 3}, // 3s
		{Rank: 2, Suit: 0}, {Rank: 3, Suit: 0}, {Rank: 4, Suit: 0}, // 5,6,7
		{Rank: 6, Suit: 0}, {Rank: 6, Suit: 1}, {Rank: 6, Suit: 2}, // 9s
		{Rank: 8, Suit: 0}, {Rank: 8, Suit: 1}, // Jacks
		{Rank: 10, Suit: 0}, // King
	}
	
	organized := PartitionHand(hand)
	
	if len(organized.Bombs) != 1 {
		t.Errorf("Expected 1 Bomb, got %d", len(organized.Bombs))
	}
	if len(organized.Straights) != 1 {
		t.Errorf("Expected 1 Straight, got %d", len(organized.Straights))
	}
	if len(organized.Triples) != 1 {
		t.Errorf("Expected 1 Triple, got %d", len(organized.Triples))
	}
	if len(organized.Pairs) != 1 {
		t.Errorf("Expected 1 Pair, got %d", len(organized.Pairs))
	}
	if len(organized.Trash) != 1 || organized.Trash[0].Rank != 10 {
		t.Errorf("Expected 1 Trash card (King), got %d: %v", len(organized.Trash), organized.Trash)
	}
}
