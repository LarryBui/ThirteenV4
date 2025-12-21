package bot

import (
	"testing"
	"tienlen/internal/domain"
)

func TestGodBot_BlockerMode(t *testing.T) {
	// Hand: 3S (Low), 2S (High)
	// Situation: Free Turn.
	// Opponent has 1 card left.
	// Normal bot might play 3S to dump low card.
	// God bot should play 2S (or highest single) to prevent opponent from winning easily?
	// Wait, if it's a free turn, playing 2S gives the turn to opponent if they have a bigger 2 or nothing.
	// Actually, if we play 3S, opponent (next) might beat it with a 4 and win.
	// If we play 2S, opponent likely can't beat it (unless they have bigger 2).
	// So God Bot should play High.
	
	hand := []domain.Card{
		{Rank: 0, Suit: 0}, // 3S
		{Rank: 12, Suit: 0}, // 2S
	}
	
	myself := &domain.Player{ Seat: 0, Hand: hand }
	enemy := &domain.Player{ Seat: 2, Hand: []domain.Card{{Rank: 5, Suit:0}} } // Enemy has 1 card
	
	game := &domain.Game{
		CurrentTurn: 0, // Seat 1 (index 0)
		Players: map[string]*domain.Player{
			"me": myself,
			"enemy": enemy,
		},
		LastPlayedCombination: domain.CardCombination{Type: domain.Invalid},
	}
	
	bot := &GodBot{}
	move, _ := bot.CalculateMove(game, 0)
	
	if move.Pass {
		t.Fatal("Bot passed on free turn")
	}
	
	// Should play the 2S (Rank 12) to block/control, NOT the 3S.
	if move.Cards[0].Rank != 12 {
		t.Errorf("God Bot failed to block. Played rank %d instead of 2S (12) when enemy had 1 card.", move.Cards[0].Rank)
	}
}
