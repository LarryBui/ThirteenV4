package app

import (
	"math/rand"
	"testing"

	"tienlen/internal/domain"
)

func TestJoinAndLeave(t *testing.T) {
	svc := NewService(rand.New(rand.NewSource(1)))
	state := &domain.MatchState{Phase: domain.PhaseLobby, Players: map[string]*domain.Player{}}

	evs, err := svc.Join(state, "u1")
	if err != nil || len(evs) != 1 {
		t.Fatalf("join err=%v events=%d", err, len(evs))
	}
	if state.OwnerUserID != "u1" {
		t.Fatalf("expected owner u1")
	}

	evs, err = svc.Join(state, "u2")
	if err != nil || len(evs) != 1 {
		t.Fatalf("join second err=%v events=%d", err, len(evs))
	}

	// Leave owner should reassign.
	evs, err = svc.Leave(state, "u1")
	if err != nil || len(evs) != 1 {
		t.Fatalf("leave err=%v events=%d", err, len(evs))
	}
	if state.OwnerUserID != "u2" {
		t.Fatalf("expected owner u2 after leave, got %s", state.OwnerUserID)
	}
}

func TestStartGameDealsHands(t *testing.T) {
	rng := rand.New(rand.NewSource(42))
	svc := NewService(rng)
	state := &domain.MatchState{Phase: domain.PhaseLobby, Players: map[string]*domain.Player{}}
	_, _ = svc.Join(state, "u1")
	_, _ = svc.Join(state, "u2")

	evs, err := svc.StartGame(state, "u1")
	if err != nil {
		t.Fatalf("start game error: %v", err)
	}
	if state.Phase != domain.PhasePlaying {
		t.Fatalf("phase = %s, want playing", state.Phase)
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
	state := &domain.MatchState{Phase: domain.PhaseLobby, Players: map[string]*domain.Player{}}
	_, _ = svc.Join(state, "u1")
	_, _ = svc.Join(state, "u2")
	_, _ = svc.StartGame(state, "u1")

	// Force small hands for predictable end.
	state.Players["u1"].Hand = []domain.Card{{Suit: "S", Rank: 0}}
	state.Players["u2"].Hand = []domain.Card{{Suit: "H", Rank: 1}}

	evs, err := svc.PlayCards(state, "u1", []domain.Card{{Suit: "S", Rank: 0}})
	if err != nil {
		t.Fatalf("play cards error: %v", err)
	}
	if state.Players["u1"].Finished != true {
		t.Fatalf("u1 should be finished")
	}
	// With two players, game ends when only one has cards left.
	if state.Phase != domain.PhaseEnded {
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
