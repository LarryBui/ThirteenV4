package app

import "tienlen/internal/domain"

// EventKind identifies emitted domain events for Nakama dispatch.
type EventKind string

const (
	EventPlayerJoined EventKind = "player_joined"
	EventPlayerLeft   EventKind = "player_left"
	EventGameStarted  EventKind = "game_started"
	EventHandDealt    EventKind = "hand_dealt"
	EventCardPlayed   EventKind = "card_played"
	EventTurnPassed   EventKind = "turn_passed"
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
	Phase           domain.Phase
	FirstTurnUserID string
}

type HandDealtPayload struct {
	UserID string
	Hand   []domain.Card
}

type CardPlayedPayload struct {
	UserID         string
	Cards          []domain.Card
	NextTurnUserID string
}

type TurnPassedPayload struct {
	UserID         string
	NextTurnUserID string
}

type GameEndedPayload struct {
	FinishOrder []string
}