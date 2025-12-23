package app

import "tienlen/internal/domain"

// EventKind identifies emitted domain events for Nakama dispatch.
type EventKind string

const (
	EventPlayerJoined EventKind = "player_joined"
	EventPlayerLeft   EventKind = "player_left"
	EventGameStarted  EventKind = "game_started"
	EventCardPlayed   EventKind = "card_played"
	EventPigChopped   EventKind = "pig_chopped"
	EventTurnPassed   EventKind = "turn_passed"
	EventPlayerFinished EventKind = "player_finished"
	EventGameEnded    EventKind = "game_ended"
)

// Event is a domain/app event with optional targeted recipients.
type Event struct {
	Kind       EventKind
	Payload    any
	Recipients []string // user IDs; empty means broadcast
}

type PlayerJoinedPayload struct {
	UserID string
	Seat   int
	Owner  bool
}

type PlayerLeftPayload struct {
	UserID string
}

type GameStartedPayload struct {
	Phase         domain.Phase
	FirstTurnSeat int
	Hand          []domain.Card
}

type PigChoppedPayload struct {
	SourceSeat     int
	TargetSeat     int
	ChopType       string
	CardsChopped   []domain.Card
	CardsChopping  []domain.Card
	BalanceChanges map[string]int64
}

type PlayerFinishedPayload struct {
	Seat int
	Rank int
}

type CardPlayedPayload struct {
	Seat int

	Cards []domain.Card

	NextTurnSeat int

	NewRound bool
}

type TurnPassedPayload struct {
	Seat int

	NextTurnSeat int

	NewRound bool
}

type GameEndedPayload struct {
	FinishOrderSeats []int

	BalanceChanges map[string]int64 // UserID -> Gold (+/-)

	RemainingHands map[int][]domain.Card // Seat -> Cards
}
