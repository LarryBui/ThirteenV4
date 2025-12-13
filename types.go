package main

import "github.com/heroiclabs/nakama-common/runtime"

// Phase represents the lifecycle stage of a Tien Len match.
type Phase string

const (
	// PhaseLobby is the pre-game state where players can join.
	PhaseLobby Phase = "lobby"
	// PhasePlaying is the active game state where cards are played.
	PhasePlaying Phase = "playing"
	// PhaseEnded is the state after a game concludes.
	PhaseEnded Phase = "ended"
)

// Card is a single playing card in the Tien Len deck.
type Card struct {
	Suit string `json:"suit"` // "S","H","D","C"
	Rank int    `json:"rank"` // 0..12 (3=0, A=11, 2=12)
}

// PlayerState tracks server-side state for a participant in the match.
type PlayerState struct {
	UserID   string
	Presence runtime.Presence
	Seat     int
	IsOwner  bool

	Hand      []Card
	HasPassed bool
	Finished  bool
}

// Label is the match label advertised for quick-match queries.
type Label struct {
	Open  bool   `json:"open"`
	Game  string `json:"game"`
	Phase string `json:"phase"`
}
