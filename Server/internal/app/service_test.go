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
	game, evs, err := svc.StartGame([]string{"u1", "u2"}, "")
	if err != nil {
		t.Fatalf("start game error: %v", err)
	}
	if game.Phase != domain.PhasePlaying {
		t.Fatalf("phase = %s, want playing", game.Phase)
	}

	gameStartedEvents := 0
	for _, ev := range evs {
		if ev.Kind == EventGameStarted {
			gameStartedEvents++
			payload := ev.Payload.(GameStartedPayload)
			if len(payload.Hand) != 13 {
				t.Fatalf("hand size = %d, want 13", len(payload.Hand))
			}
		}
	}
	if gameStartedEvents != 2 {
		t.Fatalf("game started events = %d, want 2", gameStartedEvents)
	}
}

func TestPlayCardsAndEnd(t *testing.T) {
	rng := rand.New(rand.NewSource(99))
	svc := NewService(rng)
	
	game, _, err := svc.StartGame([]string{"u1", "u2"}, "")
	if err != nil {
		t.Fatalf("start game error: %v", err)
	}

	// Force small hands for predictable end.
	// Suit 0=Spades, 1=Clubs, 2=Diamonds, 3=Hearts
	game.Players["u1"].Hand = []domain.Card{{Suit: 0, Rank: 0}}
	game.Players["u2"].Hand = []domain.Card{{Suit: 3, Rank: 1}}

	// Make sure it's u1's turn (since StartGame defaults to first player)
	game.CurrentTurn = "u1"

	evs, err := svc.PlayCards(game, "u1", []domain.Card{{Suit: 0, Rank: 0}})
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

func TestPassAndRoundReset(t *testing.T) {
	svc := NewService(nil)
	players := []string{"u1", "u2", "u3"}
	game, _, _ := svc.StartGame(players, "")

	// Force hands to ensure they don't finish immediately
	for _, p := range game.Players {
		p.Hand = []domain.Card{{Suit: 0, Rank: 5}, {Suit: 1, Rank: 5}}
	}

	game.CurrentTurn = "u1"

	// u1 plays a single
	cards := []domain.Card{{Suit: 0, Rank: 5}}
	_, err := svc.PlayCards(game, "u1", cards)
	if err != nil {
		t.Fatalf("u1 play error: %v", err)
	}

	if game.LastPlayerToPlay != "u1" {
		t.Fatalf("expected last player to be u1, got %s", game.LastPlayerToPlay)
	}

	// u2 passes
	_, err = svc.PassTurn(game, "u2")
	if err != nil {
		t.Fatalf("u2 pass error: %v", err)
	}

	// u3 passes
	_, err = svc.PassTurn(game, "u3")
	if err != nil {
		t.Fatalf("u3 pass error: %v", err)
	}

	// Now it should be u1's turn again, and the round should reset
	if game.CurrentTurn != "u1" {
		t.Fatalf("expected turn to return to u1, got %s", game.CurrentTurn)
	}

	if game.LastPlayedCombination.Type != domain.Invalid {
		t.Fatalf("expected round to reset, but last played combo is still valid: %v", game.LastPlayedCombination.Type)
	}

	// Verify all players' HasPassed is reset
	for id, p := range game.Players {
		if p.HasPassed {
			t.Errorf("player %s should have HasPassed reset to false", id)
		}
	}
}

func TestPlayErrors(t *testing.T) {
	svc := NewService(nil)
	players := []string{"u1", "u2"}
	game, _, _ := svc.StartGame(players, "")

	game.Players["u1"].Hand = []domain.Card{{Suit: 0, Rank: 0}} // 3 Spades
	game.CurrentTurn = "u1"

	// 1. Play out of turn
	_, err := svc.PlayCards(game, "u2", []domain.Card{{Suit: 0, Rank: 0}})
	if err != ErrNotYourTurn {
		t.Errorf("expected ErrNotYourTurn, got %v", err)
	}

	// 2. Play cards not in hand
	_, err = svc.PlayCards(game, "u1", []domain.Card{{Suit: 3, Rank: 12}}) // 2 Hearts
	if err != ErrCardsNotInHand {
		t.Errorf("expected ErrCardsNotInHand, got %v", err)
	}

	// 3. Play valid cards but cannot beat previous
	game.LastPlayedCombination = domain.CardCombination{
		Type:  domain.Single,
		Cards: []domain.Card{{Suit: 3, Rank: 0}}, // 3 Hearts
	}
	_, err = svc.PlayCards(game, "u1", []domain.Card{{Suit: 0, Rank: 0}}) // 3 Spades
	if err != ErrCannotBeat {
		t.Errorf("expected ErrCannotBeat, got %v", err)
	}
}

func TestRoundResetsWhenLastPlayerFinishes(t *testing.T) {
	svc := NewService(nil)
	players := []string{"u1", "u2", "u3"}
	game, _, _ := svc.StartGame(players, "")

	// Force u1 to have only one card
	game.Players["u1"].Hand = []domain.Card{{Suit: 3, Rank: 12}} // 2 Hearts
	game.CurrentTurn = "u1"

	// u1 plays their last card and finishes
	_, err := svc.PlayCards(game, "u1", []domain.Card{{Suit: 3, Rank: 12}})
	if err != nil {
		t.Fatalf("u1 play error: %v", err)
	}

	if !game.Players["u1"].Finished {
		t.Fatal("u1 should be finished")
	}

	// u2 passes
	_, err = svc.PassTurn(game, "u2")
	if err != nil {
		t.Fatalf("u2 pass error: %v", err)
	}

	// u3 passes
	_, err = svc.PassTurn(game, "u3")
	if err != nil {
		t.Fatalf("u3 pass error: %v", err)
	}

	// Now it should be u2's turn (since u1 is finished), and the round should reset
	if game.CurrentTurn != "u2" {
		t.Fatalf("expected turn to go to u2, got %s", game.CurrentTurn)
	}

	if game.LastPlayedCombination.Type != domain.Invalid {
		t.Fatalf("expected round to reset after everyone passed following a finished player")
	}
}
