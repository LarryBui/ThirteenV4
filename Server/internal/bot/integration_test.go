package bot

import (
	"testing"
	"tienlen/internal/app"
	"tienlen/internal/bot/brain"
	"tienlen/internal/domain"
)

func TestStandardBot_TacticalRestraint_ProtectsBomb(t *testing.T) {
	// Hand: Quad 3s (Bomb).
	// Opponent plays a 3 of Spades.
	// To beat it, the bot must use one of its 3s (e.g. 3H).
	// This would break the Quad. The bot should choose to Pass instead.
	
	hand := []domain.Card{
		{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 0, Suit: 2}, {Rank: 0, Suit: 3}, // Quad 3s
	}
	
	player := &domain.Player{
		Seat: 0,
		Hand: hand,
	}
	
	// Opponent played 3 of Spades (Rank 0, Suit 0)
	threeSpades := domain.Card{Rank: 0, Suit: 0}
	game := &domain.Game{
		Players: map[string]*domain.Player{
			"bot": player,
		},
		LastPlayedCombination: domain.CardCombination{
			Type:  domain.Single,
			Cards: []domain.Card{threeSpades},
			Value: domain.CardPower(threeSpades),
		},
	}
	
	bot := &StandardBot{
		Memory: brain.NewMemory(),
	}
	bot.Memory.UpdateHand(hand)
	
	move, err := bot.CalculateMove(game, player)
	if err != nil {
		t.Fatalf("CalculateMove failed: %v", err)
	}
	
	if !move.Pass {
		t.Errorf("Bot broke a Bomb to beat a single! It should have passed. Played: %v", move.Cards)
	}
}

func TestStandardBot_MemoryDrivenLead(t *testing.T) {
	// Scenario:
	// 1. Bot has lead.
	// 2. Bot knows Opponent (Seat 1) passed on a Single King earlier.
	// 3. Bot should favor leading with a Single to exploit that weakness.
	
	hand := []domain.Card{
		{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, // Pair of 3s
		{Rank: 9, Suit: 0},                   // Single Queen
	}
	
	player := &domain.Player{
		Seat: 0,
		Hand: hand,
	}
	
	bot := &StandardBot{
		Memory: brain.NewMemory(),
	}
	bot.Memory.UpdateHand(hand)
	
	// Simulate Opponent passing on a King (Rank 10)
	kingCombo := domain.CardCombination{
		Type:  domain.Single,
		Value: 40, // King
	}
	bot.Memory.CurrentCombo = kingCombo
	bot.Memory.RecordPass(1) // Seat 1 passed on a King
	
	game := &domain.Game{
		Players: map[string]*domain.Player{
			"bot": player,
		},
		LastPlayedCombination: domain.CardCombination{Type: domain.Invalid}, // Free lead
	}
	
	move, _ := bot.CalculateMove(game, player)
	
	// Because seat 1 is weak to singles, bot should lead with the Single Queen 
	// rather than the Pair of 3s.
	if len(move.Cards) != 1 {
		t.Errorf("Bot should have exploited single weakness, but led with %v", move.Cards)
	}
}

func TestStandardBot_EventSynchronization(t *testing.T) {
	bot := &StandardBot{
		Memory: brain.NewMemory(),
	}
	
	// 1. Simulate game start
	bot.OnEvent(app.GameStartedPayload{
		Hand: []domain.Card{{Rank: 0, Suit: 0}},
	}, true)
	
	// 2. Simulate someone playing a card
	playedCards := []domain.Card{{Rank: 5, Suit: 0}}
	bot.OnEvent(app.CardPlayedPayload{
		Seat:  1,
		Cards: playedCards,
	}, false)
	
	if bot.Memory.CurrentCombo.Type != domain.Single {
		t.Errorf("Memory failed to update CurrentCombo from CardPlayed event")
	}
	
	if !bot.Memory.IsPlayed(playedCards[0]) {
		t.Errorf("Memory failed to mark cards as Played")
	}
}