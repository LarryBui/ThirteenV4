package brain

import (
	"testing"
	"tienlen/internal/domain"
)

func TestEstimator_LeadTurnProbability(t *testing.T) {
	mem := NewMemory()
	// Setup: 2H is the boss (Rank 12, Suit 3).
	// We hold 2S (Rank 12, Suit 0).
	// 2C (12,1) and 2D (12,2) are already played.
	
	twoS := domain.Card{Rank: 12, Suit: 0}
	twoC := domain.Card{Rank: 12, Suit: 1}
	twoD := domain.Card{Rank: 12, Suit: 2}
	twoH := domain.Card{Rank: 12, Suit: 3}

	mem.MarkMine([]domain.Card{twoS})
	mem.MarkPlayed([]domain.Card{twoC, twoD})
	
	est := NewEstimator(mem)

	// Probability for 2S: Only 2H (unknown) beats it.
	// Formula: 1.0 / (higherUnknown + 1)
	prob := est.LeadTurnProbability(twoS)
	
	if prob < 0.49 || prob > 0.51 {
		t.Errorf("Expected prob ~0.5 for 2S when only 2H is unknown, got %f", prob)
	}

	// Now mark 2H as played
	mem.MarkPlayed([]domain.Card{twoH})
	prob = est.LeadTurnProbability(twoS)
	if prob != 1.0 {
		t.Errorf("Expected prob 1.0 for 2S when 2H is played, got %f", prob)
	}
}

func TestEstimator_GetComboLikelihood_PhysicalConstraints(t *testing.T) {
	mem := NewMemory()
	// Opponent at Seat 1 has only 2 cards left.
	mem.Opponents[1] = NewOpponentProfile(1)
	mem.Opponents[1].CardsRemaining = 2
	
	est := NewEstimator(mem)
	
	// Likelihood of Straight (min 3 cards required)
	prob := est.GetComboLikelihood(1, domain.Straight)
	if prob != 0.0 {
		t.Errorf("Expected 0.0 likelihood for Straight when opponent has 2 cards, got %f", prob)
	}
	
	// Likelihood of Pair (2 cards required) -> Should be > 0.
	prob = est.GetComboLikelihood(1, domain.Pair)
	if prob == 0.0 {
		t.Errorf("Expected >0.0 likelihood for Pair when opponent has 2 cards, got %f", prob)
	}
}

func TestEstimator_IsSafeFromNextPlayers(t *testing.T) {
	mem := NewMemory()
	// Seat 0 is the Bot.
	// Seat 1 passed on a Pair of 5s (Value 11).
	// Seat 2 passed on a Pair of 9s (Value 27).
	
	mem.Opponents[1] = NewOpponentProfile(1)
	mem.Opponents[1].RecordFailure(domain.CardCombination{
		Type: domain.Pair, Value: 11,
	})
	
	mem.Opponents[2] = NewOpponentProfile(2)
	mem.Opponents[2].RecordFailure(domain.CardCombination{
		Type: domain.Pair, Value: 27,
	})

	est := NewEstimator(mem)

	// Bot wants to play a Pair of 7s (Value 19).
	// Value 19 > 11 (Safe against Seat 1)
	// Value 19 < 27 (Not safe against Seat 2, as they only failed to beat 27)
	pair7 := domain.CardCombination{
		Type: domain.Pair,
		Value: 19,
	}

	safety := est.IsSafeFromNextPlayers(pair7, 0)
	
	// Expectation: 
	// Seat 1: Safe (1.0)
	// Seat 2: Unsafe (0.0) - Because logic says if combo.Value > maxFailed, return true (can beat).
	// Total: 1.0 / 2 = 0.5
	
	if safety < 0.4 || safety > 0.6 {
		t.Errorf("Expected safety ~0.5 (Safe against Seat 1, Unsafe against Seat 2), got %f", safety)
	}
}
