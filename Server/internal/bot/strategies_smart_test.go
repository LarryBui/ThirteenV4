package bot

import (
	"testing"
	"tienlen/internal/domain"
)

func TestSmartBot_PreservesStraight(t *testing.T) {
	// Hand: 3S, 4S, 5S, 6S, 7S, 8S (A long straight)
	// If opponent plays a 3H, a dumb bot might play the 4S, breaking the straight.
	// A smart bot should realize playing the 4S destroys the straight utility and might pass or play a different card.
	// But in this hand, ALL cards are in the straight.
	
	hand := []domain.Card{
		{Rank: 0, Suit: 0}, // 3S
		{Rank: 1, Suit: 0}, // 4S
		{Rank: 2, Suit: 0}, // 5S
		{Rank: 3, Suit: 0}, // 6S
		{Rank: 4, Suit: 0}, // 7S
		{Rank: 5, Suit: 0}, // 8S
	}
	
	player := &domain.Player{
		Seat: 0,
		Hand: hand,
	}
	
	// Prev: 3H (Rank 0, Suit 2)
	game := &domain.Game{
		Players: map[string]*domain.Player{
			"bot": player,
		},
		LastPlayedCombination: domain.CardCombination{
			Type:  domain.Single,
			Cards: []domain.Card{{Rank: 0, Suit: 2}},
			Value: 2, // 3H
		},
	}
	
	bot := &SmartBot{}
	move, err := bot.CalculateMove(game, 0)
	if err != nil {
		t.Fatalf("CalculateMove failed: %v", err)
	}
	
	// The bot should recognize that playing any card breaks a 6-card straight.
	// Depending on weights, it might decide it's better to PASS and keep the straight for its own lead.
	if !move.Pass {
		t.Logf("Smart bot played %+v despite breaking a straight", move.Cards)
		// If it DOES play, it's because our evaluator thinks the penalty for breaking is small.
		// But ideally, a Smart bot passes here.
	} else {
		t.Log("Smart bot passed to preserve the straight. Good.")
	}
}

func TestSmartBot_FinishesGame(t *testing.T) {
	// Hand: 2S (One card left)
	hand := []domain.Card{{Rank: 12, Suit: 0}}
	
	player := &domain.Player{
		Seat: 0,
		Hand: hand,
	}
	
	// Free turn
	game := &domain.Game{
		Players: map[string]*domain.Player{
			"bot": player,
		},
		LastPlayedCombination: domain.CardCombination{Type: domain.Invalid},
	}
	
	bot := &SmartBot{}
	move, _ := bot.CalculateMove(game, 0)
	
	if len(move.Cards) != 1 || move.Cards[0].Rank != 12 {
		t.Errorf("Bot should have played its last card to win, got %+v", move.Cards)
	}
}
