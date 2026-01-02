package bot

import (
	"testing"
	"tienlen/internal/bot/brain"
	"tienlen/internal/domain"
)

func TestStandardBot_ProtectsBomb(t *testing.T) {
	// Hand: 3S, 3C, 3D, 3H (Bomb 3s), 4S (Single)
	// Table: Single 2S (Rank 12, Suit 0).
	// Goal: The bot should recognize it can chop the 2S with the Bomb.
	// This test verifies that the bot generates the Bomb move and selects it.
	
	hand := []domain.Card{
		{Rank: 0, Suit: 0}, 
		{Rank: 0, Suit: 1}, 
		{Rank: 0, Suit: 2}, 
		{Rank: 0, Suit: 3}, 
		{Rank: 1, Suit: 0}, // 4S
	}
	
	player := &domain.Player{Seat: 0, Hand: hand}
	
	game := &domain.Game{
		Players: map[string]*domain.Player{"bot": player},
		LastPlayedCombination: domain.CardCombination{
			Type: domain.Single, 
			Value: 48, // 2S (Rank 12*4 + 0)
			Cards: []domain.Card{{Rank: 12, Suit: 0}},
			Count: 1,
		},
	}
	
	bot := &StandardBot{Memory: brain.NewMemory()}
	move, err := bot.CalculateMove(game, player)
	if err != nil {
		t.Fatalf("CalculateMove failed: %v", err)
	}
	
	// Expect move to be the Bomb
	if move.Pass {
		t.Errorf("Bot passed when it could have chopped the 2S with a Bomb")
	} else if len(move.Cards) != 4 {
		t.Errorf("Bot played %d cards, expected 4 (Bomb)", len(move.Cards))
	}
}

func TestStandardBot_RefusesToBreakBomb(t *testing.T) {
	// Hand: 5555 (Bomb), No other cards (for simplicity, or minimal other cards).
	// Actually if it has ONLY a bomb, it can't play a single 5 anyway (invalid move).
	// Hand: 5S, 5C, 5D, 5H (Bomb), 6S.
	// Table: Single 4S.
	// Valid moves: 5S (beats 4S), 6S (beats 4S).
	// Strategy: Bot should play 6S. If it plays 5S, it breaks the bomb.
	// If 6S wasn't there, it should PASS rather than break the bomb for a cheap 4S.
	
	hand := []domain.Card{
		{Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}, {Rank: 2, Suit: 2}, {Rank: 2, Suit: 3}, // Bomb 5s
	}
	
	player := &domain.Player{Seat: 0, Hand: hand}
	
	game := &domain.Game{
		Players: map[string]*domain.Player{"bot": player},
		LastPlayedCombination: domain.CardCombination{
			Type: domain.Single, 
			Value: 4, // 4S (Rank 1*4)
			Cards: []domain.Card{{Rank: 1, Suit: 0}},
			Count: 1,
		},
	}
	
	bot := &StandardBot{Memory: brain.NewMemory()}
	move, err := bot.CalculateMove(game, player)
	if err != nil {
		t.Fatalf("CalculateMove failed: %v", err)
	}
	
	if !move.Pass {
		// Playing a single 5 from a bomb is technically legal if you break it,
		// but strategic suicide.
		t.Logf("Bot played: %+v", move.Cards)
		t.Error("Bot should have passed to preserve the Bomb 5s instead of playing a single 5")
	}
}

func TestStandardBot_PlaysBossOnFreeTurn(t *testing.T) {
	// Hand: 2H (Boss), 3S (Trash).
	// Free turn.
	// The bot SHOULD play the Boss (2H) because it guarantees keeping the lead
	// and allows it to dictate the next turn, rather than playing weak trash 
	// that might be beaten.
	
	hand := []domain.Card{
		{Rank: 12, Suit: 3}, // 2H
		{Rank: 0, Suit: 0},  // 3S
	}
	
	player := &domain.Player{Seat: 0, Hand: hand}
	
	// Mark 2H as Boss (nothing higher unknown)
	mem := brain.NewMemory()
	mem.MarkMine(hand)
	
	game := &domain.Game{
		Players: map[string]*domain.Player{"bot": player},
		LastPlayedCombination: domain.CardCombination{Type: domain.Invalid},
	}
	
	bot := &StandardBot{Memory: mem}
	move, _ := bot.CalculateMove(game, player)
	
	if len(move.Cards) == 1 && move.Cards[0].Rank == 12 && move.Cards[0].Suit == 3 {
		t.Log("Bot played Boss 2H. Confirming behavior.")
	} else {
		t.Errorf("Bot played %+v. Expected Boss 2H to maintain control.", move.Cards)
	}
}
