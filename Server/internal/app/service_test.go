package app

import (
	"math/rand"
	"testing"

	"tienlen/internal/domain"
)

func TestStartGameDealsHands(t *testing.T) {
	rng := rand.New(rand.NewSource(42))
	svc := NewService(rng)
	
	// Pass player IDs directly to StartGame
	game, evs, err := svc.StartGame([]string{"u1", "u2"})
	if err != nil {
		t.Fatalf("start game error: %v", err)
	}
	if game.Phase != domain.PhasePlaying {
		t.Fatalf("phase = %s, want playing", game.Phase)
	}

	handEvents := 0
	for _, ev := range evs {
		if ev.Kind == EventHandDealt {
			handEvents++
			payload := ev.Payload.(HandDealtPayload)
			if len(payload.Hand) != 13 {
				t.Fatalf("hand size = %d, want 13", len(payload.Hand))
			}
		}
	}
	if handEvents != 2 {
		t.Fatalf("hand events = %d, want 2", handEvents)
	}
}

func TestPlayCardsAndEnd(t *testing.T) {
	rng := rand.New(rand.NewSource(99))
	svc := NewService(rng)
	
	game, _, err := svc.StartGame([]string{"u1", "u2"})
	if err != nil {
		t.Fatalf("start game error: %v", err)
	}

	// Force small hands for predictable end.
	game.Players["u1"].Hand = []domain.Card{{Suit: "S", Rank: 0}}
	game.Players["u2"].Hand = []domain.Card{{Suit: "H", Rank: 1}}

	// Make sure it's u1's turn (since StartGame defaults to first player)
	game.CurrentTurn = "u1"

	evs, err := svc.PlayCards(game, "u1", []domain.Card{{Suit: "S", Rank: 0}})
	if err != nil {
		t.Fatalf("play cards error: %v", err)
	}
	if game.Players["u1"].Finished != true {
		t.Fatalf("u1 should be finished")
	}
	// With two players, game ends when only one has cards left.
	if game.Phase != domain.PhaseEnded {
		t.Fatalf("game should have ended when one player remains")
	}
	foundEnd := false
	for _, ev := range evs {
		if ev.Kind == EventGameEnded {
			foundEnd = true
		}
	}
	if !foundEnd {
		t.Fatalf("expected game ended event")
	}
}
