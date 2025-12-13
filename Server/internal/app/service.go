package app

import (
	"errors"
	"math/rand"
	"time"

	"tienlen/internal/domain"
)

// Service contains Tien Len use-cases operating on domain state.
type Service struct {
	rng *rand.Rand
}

// NewService constructs a Service with provided rng or a time-seeded default.
func NewService(rng *rand.Rand) *Service {
	if rng == nil {
		rng = rand.New(rand.NewSource(time.Now().UnixNano()))
	}
	return &Service{rng: rng}
}

var (
	ErrNotOwner       = errors.New("actor is not match owner")
	ErrNotInLobby     = errors.New("match not in lobby")
	ErrNotPlaying     = errors.New("match not in playing phase")
	ErrMatchNotEnded  = errors.New("match not ended")
	ErrTooFewPlayers  = errors.New("not enough players to start")
	ErrUnknownPlayer  = errors.New("player not found")
	ErrPlayerFinished = errors.New("player already finished")
)

// Join adds a player if not already present and returns the resulting events.
func (s *Service) Join(state *domain.MatchState, userID string) ([]Event, error) {
	if _, ok := state.Players[userID]; ok {
		// Rejoin does not emit duplicate events.
		return nil, nil
	}

	seat := domain.LowestAvailableSeat(&state.Seats)
	state.Seats[seat] = userID

	isOwner := false
	if state.OwnerUserID == "" {
		state.OwnerUserID = userID
		isOwner = true
	}

	state.Players[userID] = &domain.Player{
		UserID:  userID,
		Seat:    seat + 1, // 1..4 externally
		IsOwner: isOwner,
	}

	ev := Event{
		Kind: EventPlayerJoined,
		Payload: PlayerJoinedPayload{
			UserID: userID,
			Seat:   seat + 1,
			Owner:  isOwner,
		},
	}
	return []Event{ev}, nil
}

// Leave removes players and reassigns owner if needed.
func (s *Service) Leave(state *domain.MatchState, userID string) ([]Event, error) {
	pl, ok := state.Players[userID]
	if !ok {
		return nil, nil
	}

	state.Seats[pl.Seat-1] = ""
	delete(state.Players, userID)

	// Owner reassignment to first remaining player.
	if state.OwnerUserID == userID {
		state.OwnerUserID = ""
		for otherID, other := range state.Players {
			state.OwnerUserID = otherID
			other.IsOwner = true
			break
		}
	}

	ev := Event{
		Kind:    EventPlayerLeft,
		Payload: PlayerLeftPayload{UserID: userID},
	}
	return []Event{ev}, nil
}

// StartGame starts a match, deals cards, and emits events.
func (s *Service) StartGame(state *domain.MatchState, actorUserID string) ([]Event, error) {
	if state.Phase != domain.PhaseLobby {
		return nil, ErrNotInLobby
	}
	if actorUserID != state.OwnerUserID {
		return nil, ErrNotOwner
	}
	if len(state.Players) < 2 {
		return nil, ErrTooFewPlayers
	}

	deck := domain.NewDeck()
	s.shuffle(deck)

	events := make([]Event, 0, len(state.Players)+1)

	i := 0
	for _, pl := range state.Players {
		pl.Hand = append([]domain.Card{}, deck[i:i+13]...)
		pl.HasPassed = false
		pl.Finished = false
		i += 13

		events = append(events, Event{
			Kind: EventHandDealt,
			Payload: HandDealtPayload{
				UserID: pl.UserID,
				Hand:   pl.Hand,
			},
			Recipients: []string{pl.UserID},
		})
	}

	state.Phase = domain.PhasePlaying
	state.FinishOrder = nil

	events = append(events, Event{
		Kind:    EventGameStarted,
		Payload: GameStartedPayload{Phase: state.Phase},
	})

	return events, nil
}

// PlayCards processes a play action and emits resulting events.
func (s *Service) PlayCards(state *domain.MatchState, actorUserID string, cards []domain.Card) ([]Event, error) {
	if state.Phase != domain.PhasePlaying {
		return nil, ErrNotPlaying
	}
	pl, ok := state.Players[actorUserID]
	if !ok {
		return nil, ErrUnknownPlayer
	}
	if pl.Finished {
		return nil, ErrPlayerFinished
	}
	if len(cards) == 0 {
		return nil, nil
	}

	pl.Hand = domain.RemoveCards(pl.Hand, cards)
	events := []Event{
		{
			Kind: EventCardPlayed,
			Payload: CardPlayedPayload{
				UserID: actorUserID,
				Cards:  cards,
			},
		},
	}

	if len(pl.Hand) == 0 && !pl.Finished {
		pl.Finished = true
		state.FinishOrder = append(state.FinishOrder, actorUserID)
	}

	if domain.CountPlayersWithCards(state) <= 1 {
		state.Phase = domain.PhaseEnded
		events = append(events, Event{
			Kind:    EventGameEnded,
			Payload: GameEndedPayload{FinishOrder: state.FinishOrder},
		})
	}

	return events, nil
}

// PassTurn marks a player's pass action.
func (s *Service) PassTurn(state *domain.MatchState, actorUserID string) ([]Event, error) {
	if state.Phase != domain.PhasePlaying {
		return nil, ErrNotPlaying
	}
	pl, ok := state.Players[actorUserID]
	if !ok {
		return nil, ErrUnknownPlayer
	}
	if pl.Finished {
		return nil, ErrPlayerFinished
	}

	pl.HasPassed = true
	return []Event{
		{
			Kind:    EventTurnPassed,
			Payload: TurnPassedPayload{UserID: actorUserID},
		},
	}, nil
}

// RequestNewGame resets the match back to lobby after it ended.
func (s *Service) RequestNewGame(state *domain.MatchState, actorUserID string) error {
	if state.Phase != domain.PhaseEnded {
		return ErrMatchNotEnded
	}
	if actorUserID != state.OwnerUserID {
		return ErrNotOwner
	}

	for _, pl := range state.Players {
		pl.Hand = nil
		pl.HasPassed = false
		pl.Finished = false
	}
	state.Phase = domain.PhaseLobby
	state.FinishOrder = nil
	return nil
}

func (s *Service) shuffle(deck []domain.Card) {
	s.rng.Shuffle(len(deck), func(i, j int) { deck[i], deck[j] = deck[j], deck[i] })
}
