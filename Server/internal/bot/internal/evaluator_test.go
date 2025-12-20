package internal

import (
	"testing"
	"tienlen/internal/domain"
)

func TestEvaluateHand(t *testing.T) {
	// Case 1: Pure Trash (3S, 5S, 7S)
	trashHand := []domain.Card{
		{Rank: 0, Suit: 0}, // 3S
		{Rank: 2, Suit: 0}, // 5S
		{Rank: 4, Suit: 0}, // 7S
	}
	scoreTrash := EvaluateHand(trashHand)
	// Expected: 3 * -2.0 = -6.0
	
	// Case 2: Pure Straight (3S, 4S, 5S)
	straightHand := []domain.Card{
		{Rank: 0, Suit: 0},
		{Rank: 1, Suit: 0},
		{Rank: 2, Suit: 0},
	}
	scoreStraight := EvaluateHand(straightHand)
	// Expected: 3 cards * 5.0 = 15.0
	
	if scoreStraight <= scoreTrash {
		t.Errorf("Straight (%.2f) should be worth more than Trash (%.2f)", scoreStraight, scoreTrash)
	}
	
	// Case 3: Pair vs Two Singles
	pairHand := []domain.Card{
		{Rank: 0, Suit: 0},
		{Rank: 0, Suit: 1},
	}
	scorePair := EvaluateHand(pairHand) 
	// Expected: 1 pair * 5.0 = 5.0
	
	twoSinglesHand := []domain.Card{
		{Rank: 0, Suit: 0},
		{Rank: 1, Suit: 0},
	}
	scoreTwoSingles := EvaluateHand(twoSinglesHand)
	// Expected: 2 * -2.0 = -4.0
	
	if scorePair <= scoreTwoSingles {
		t.Errorf("Pair (%.2f) should be worth more than 2 Singles (%.2f)", scorePair, scoreTwoSingles)
	}
	
	// Case 4: Pig (2S)
	pigHand := []domain.Card{
		{Rank: 12, Suit: 0},
	}
	scorePig := EvaluateHand(pigHand)
	// Expected: 20.0
	
	if scorePig < 15.0 {
		t.Errorf("Pig (%.2f) should be very valuable", scorePig)
	}
}
