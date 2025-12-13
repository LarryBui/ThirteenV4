package domain

import (
	"encoding/json"
	"fmt"
	"reflect"
	"testing"
)

func TestLowestAvailableSeat(t *testing.T) {
	tests := []struct {
		name  string
		seats [4]string
		want  int
	}{
		{name: "all empty", seats: [4]string{"", "", "", ""}, want: 0},
		{name: "first taken", seats: [4]string{"u1", "", "", ""}, want: 1},
		{name: "first two taken", seats: [4]string{"u1", "u2", "", ""}, want: 2},
		{name: "full returns zero", seats: [4]string{"u1", "u2", "u3", "u4"}, want: 0},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := LowestAvailableSeat(&tt.seats); got != tt.want {
				t.Fatalf("LowestAvailableSeat() = %d, want %d", got, tt.want)
			}
		})
	}
}

func TestComputeLabel(t *testing.T) {
	state := &MatchState{Phase: PhaseLobby, Players: map[string]*Player{"a": {}, "b": {}}}
	label := ComputeLabel(state)
	if !label.Open || label.Game != "tienlen" || label.Phase != string(PhaseLobby) {
		t.Fatalf("unexpected label: %+v", label)
	}

	// full lobby
	state.Players["c"] = &Player{}
	state.Players["d"] = &Player{}
	label = ComputeLabel(state)
	if label.Open {
		t.Fatalf("expected label.Open=false for full lobby")
	}

	if _, err := json.Marshal(label); err != nil {
		t.Fatalf("label should marshal: %v", err)
	}
}

func TestNewDeck(t *testing.T) {
	deck := NewDeck()
	if len(deck) != 52 {
		t.Fatalf("deck size = %d, want 52", len(deck))
	}

	seen := make(map[string]bool)
	for _, c := range deck {
		key := fmt.Sprintf("%s-%d", c.Suit, c.Rank)
		if seen[key] {
			t.Fatalf("duplicate card found: %s", key)
		}
		seen[key] = true
		if c.Rank < 0 || c.Rank > 12 {
			t.Fatalf("rank out of range: %d", c.Rank)
		}
		switch c.Suit {
		case "S", "H", "D", "C":
		default:
			t.Fatalf("unexpected suit: %s", c.Suit)
		}
	}
}

func TestRemoveCards(t *testing.T) {
	hand := []Card{
		{Suit: "S", Rank: 0},
		{Suit: "H", Rank: 1},
		{Suit: "D", Rank: 2},
		{Suit: "S", Rank: 3},
	}
	played := []Card{
		{Suit: "H", Rank: 1},
		{Suit: "S", Rank: 3},
	}

	got := RemoveCards(hand, played)
	want := []Card{{Suit: "S", Rank: 0}, {Suit: "D", Rank: 2}}

	if !reflect.DeepEqual(got, want) {
		t.Fatalf("RemoveCards() = %v, want %v", got, want)
	}
}

func TestCountPlayersWithCards(t *testing.T) {
	state := &MatchState{
		Players: map[string]*Player{
			"a": {Hand: []Card{{Suit: "S", Rank: 0}}},
			"b": {Hand: []Card{}, Finished: false},
			"c": {Hand: []Card{{Suit: "H", Rank: 1}}, Finished: true},
		},
	}

	if got := CountPlayersWithCards(state); got != 1 {
		t.Fatalf("CountPlayersWithCards() = %d, want 1", got)
	}
}
