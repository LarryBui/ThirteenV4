package bot

import (
	"testing"
	"tienlen/internal/domain"
)

func TestGoodBot_CalculateMove_Lead(t *testing.T) {
	// Hand: 3S (0,0), 4S (1,0), 2S (12,0)
	hand := []domain.Card{
		{Rank: 0, Suit: 0},
		{Rank: 1, Suit: 0},
		{Rank: 12, Suit: 0},
	}
	
	player := &domain.Player{
		Seat: 1,
		Hand: hand,
	}
	
	game := &domain.Game{
		Players: map[string]*domain.Player{
			"bot": player,
		},
		LastPlayedCombination: domain.CardCombination{Type: domain.Invalid},
	}
	
	bot := &GoodBot{}
	move, err := bot.CalculateMove(game, 0) // Seat 0 (0-based) matches Seat 1 (1-based)
	if err != nil {
		t.Fatalf("CalculateMove failed: %v", err)
	}
	
	if move.Pass {
		t.Fatal("Bot passed on a free turn")
	}
	
	// Should play 3S (Rank 0)
	if len(move.Cards) != 1 || move.Cards[0].Rank != 0 {
		t.Errorf("Bot should have played 3S (Rank 0), played %+v", move.Cards)
	}
}

func TestGoodBot_CalculateMove_Respond(t *testing.T) {
	// Hand: 4S (1,0), 6S (3,0), 2S (12,0)
	hand := []domain.Card{
		{Rank: 1, Suit: 0},
		{Rank: 3, Suit: 0},
		{Rank: 12, Suit: 0},
	}
	
	player := &domain.Player{
		Seat: 1,
		Hand: hand,
	}
	
	// Prev: 5S (Rank 2)
	game := &domain.Game{
		Players: map[string]*domain.Player{
			"bot": player,
		},
		LastPlayedCombination: domain.CardCombination{
			Type:  domain.Single,
			Cards: []domain.Card{{Rank: 2, Suit: 0}},
			Value: 8, // 2*4 + 0
		},
	}
	
	bot := &GoodBot{}
	move, err := bot.CalculateMove(game, 0)
	if err != nil {
		t.Fatalf("CalculateMove failed: %v", err)
	}
	
	// Should play 6S (Rank 3). 4S is too low. 2S is too high (Good bot is conservative).
	if len(move.Cards) != 1 || move.Cards[0].Rank != 3 {
		t.Errorf("Bot should have played 6S (Rank 3), played %+v", move.Cards)
	}
}
