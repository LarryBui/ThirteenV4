package internal

import (
	"testing"
	"tienlen/internal/domain"
)

func TestProfileHand_CountsCombos(t *testing.T) {
	hand := []domain.Card{
		{Rank: 0, Suit: 0},  // 3S
		{Rank: 1, Suit: 0},  // 4S
		{Rank: 2, Suit: 0},  // 5S
		{Rank: 5, Suit: 0},  // 8S
		{Rank: 5, Suit: 1},  // 8D
		{Rank: 6, Suit: 0},  // 9S
		{Rank: 8, Suit: 0},  // JS
		{Rank: 8, Suit: 1},  // JD
		{Rank: 8, Suit: 2},  // JH
		{Rank: 11, Suit: 0}, // AS
		{Rank: 11, Suit: 1}, // AD
		{Rank: 11, Suit: 2}, // AH
		{Rank: 11, Suit: 3}, // AC
	}

	profile := ProfileHand(hand)

	if profile.Pairs != 1 {
		t.Fatalf("Pairs = %d, want 1", profile.Pairs)
	}
	if profile.Triples != 1 {
		t.Fatalf("Triples = %d, want 1", profile.Triples)
	}
	if profile.Quads != 1 {
		t.Fatalf("Quads = %d, want 1", profile.Quads)
	}
	if profile.Straights != 1 || profile.StraightCards != 3 {
		t.Fatalf("Straights = %d (cards %d), want 1 straight of 3", profile.Straights, profile.StraightCards)
	}
	if profile.Singles != 1 {
		t.Fatalf("Singles = %d, want 1", profile.Singles)
	}
	if profile.Twos != 0 {
		t.Fatalf("Twos = %d, want 0", profile.Twos)
	}
}

func TestProfileHand_DetectsPines(t *testing.T) {
	hand := []domain.Card{
		{Rank: 0, Suit: 0}, // 3S
		{Rank: 0, Suit: 1}, // 3D
		{Rank: 1, Suit: 0}, // 4S
		{Rank: 1, Suit: 1}, // 4D
		{Rank: 2, Suit: 0}, // 5S
		{Rank: 2, Suit: 1}, // 5D
		{Rank: 3, Suit: 0}, // 6S
		{Rank: 3, Suit: 1}, // 6D
		{Rank: 6, Suit: 0}, // 9S
		{Rank: 12, Suit: 0}, // 2S
	}

	profile := ProfileHand(hand)

	if profile.Pines != 1 || profile.PineCards != 8 || profile.MaxPinePairs != 4 {
		t.Fatalf("Pines = %d (cards %d, max %d), want 1 pine of 4 pairs", profile.Pines, profile.PineCards, profile.MaxPinePairs)
	}
	if profile.Singles != 2 {
		t.Fatalf("Singles = %d, want 2", profile.Singles)
	}
	if profile.Twos != 1 {
		t.Fatalf("Twos = %d, want 1", profile.Twos)
	}
}
