package domain

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
	Suit string // "S","H","D","C"
	Rank int    // 0..12 (3=0, A=11, 2=12)
}

// Player holds state for a participant in the match.
type Player struct {
	UserID    string
	Seat      int  // 1-based seat number
	IsOwner   bool // true if match owner
	Hand      []Card
	HasPassed bool
	Finished  bool
}

// MatchState holds authoritative state for a Tien Len match instance.
type MatchState struct {
	Phase Phase

	Players map[string]*Player // userId -> player
	Seats   [4]string          // index 0..3 => userId or ""

	OwnerUserID string

	// Turn / Round tracking (placeholders for future logic)
	CurrentTurnSeat int
	RoundLeaderSeat int
	LastPlaySeat    int

	// Finish order
	FinishOrder []string // userIds in order they went out
}
