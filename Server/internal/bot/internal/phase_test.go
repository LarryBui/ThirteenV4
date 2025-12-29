package internal

import (
	"testing"
	"tienlen/internal/domain"
)

func TestDetectPhase_Opening(t *testing.T) {
	game := &domain.Game{
		Players: map[string]*domain.Player{
			"p1": {Seat: 0, Hand: make([]domain.Card, 13)},
			"p2": {Seat: 1, Hand: make([]domain.Card, 13)},
		},
	}

	if got := DetectPhase(game); got != PhaseOpening {
		t.Fatalf("DetectPhase = %v, want %v", got, PhaseOpening)
	}
}

func TestDetectPhase_Mid(t *testing.T) {
	game := &domain.Game{
		Players: map[string]*domain.Player{
			"p1": {Seat: 0, Hand: make([]domain.Card, 8)},
			"p2": {Seat: 1, Hand: make([]domain.Card, 7)},
		},
	}

	if got := DetectPhase(game); got != PhaseMid {
		t.Fatalf("DetectPhase = %v, want %v", got, PhaseMid)
	}
}

func TestDetectPhase_End(t *testing.T) {
	game := &domain.Game{
		Players: map[string]*domain.Player{
			"p1": {Seat: 0, Hand: make([]domain.Card, 5)},
			"p2": {Seat: 1, Hand: make([]domain.Card, 9)},
		},
	}

	if got := DetectPhase(game); got != PhaseEnd {
		t.Fatalf("DetectPhase = %v, want %v", got, PhaseEnd)
	}
}
