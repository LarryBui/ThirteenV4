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

// StartGame initializes a new Game domain object with the provided players.
// It expects a list of userIDs representing the players in seat order (empty strings for empty seats).
func (s *Service) StartGame(playerIDs []string) (*domain.Game, []Event, error) {
	activePlayers := make(map[string]*domain.Player)
	var seats []string

	// Filter empty seats and create players
	for i, userID := range playerIDs {
		if userID == "" {
			continue
		}
		activePlayers[userID] = &domain.Player{
			UserID: userID,
			Seat:   i + 1, // 1-based seat index for domain
		}
		seats = append(seats, userID)
	}

	if len(activePlayers) < 2 {
		return nil, nil, ErrTooFewPlayers
	}

	deck := domain.NewDeck()
	s.shuffle(deck)

	game := &domain.Game{
		Phase:   domain.PhasePlaying,
		Players: activePlayers,
	}

	events := make([]Event, 0, len(activePlayers)+1)

	// Deal cards
	cardIdx := 0
	for _, userID := range seats { // Iterate in seat order
		pl := activePlayers[userID]
		pl.Hand = append([]domain.Card{}, deck[cardIdx:cardIdx+13]...)
		pl.HasPassed = false
		pl.Finished = false
		cardIdx += 13

		events = append(events, Event{
			Kind: EventHandDealt,
			Payload: HandDealtPayload{
				UserID: pl.UserID,
				Hand:   pl.Hand,
			},
			Recipients: []string{pl.UserID},
		})
	}

	// TODO: Determine FirstTurn logic (e.g. 3 of Spades or winner of previous)
	// For now, simple round-robin starting at first seat
	game.CurrentTurn = seats[0]

	events = append(events, Event{
		Kind:    EventGameStarted,
		Payload: GameStartedPayload{Phase: game.Phase},
	})

	return game, events, nil
}

// PlayCards processes a play action and emits resulting events.
func (s *Service) PlayCards(game *domain.Game, actorUserID string, cards []domain.Card) ([]Event, error) {
	if game.Phase != domain.PhasePlaying {
		return nil, ErrNotPlaying
	}
	pl, ok := game.Players[actorUserID]
	if !ok {
		return nil, ErrUnknownPlayer
	}
	if game.CurrentTurn != actorUserID {
		// return nil, errors.New("not your turn") // TODO: Add specific error
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
		game.FinishOrder = append(game.FinishOrder, actorUserID)
	}

	if domain.CountPlayersWithCards(game) <= 1 {
		game.Phase = domain.PhaseEnded
		events = append(events, Event{
			Kind:    EventGameEnded,
			Payload: GameEndedPayload{FinishOrder: game.FinishOrder},
		})
	}

	// TODO: Update game.CurrentTurn

	return events, nil
}

// PassTurn marks a player's pass action.
func (s *Service) PassTurn(game *domain.Game, actorUserID string) ([]Event, error) {
	if game.Phase != domain.PhasePlaying {
		return nil, ErrNotPlaying
	}
	pl, ok := game.Players[actorUserID]
	if !ok {
		return nil, ErrUnknownPlayer
	}
	if game.CurrentTurn != actorUserID {
		// return nil, errors.New("not your turn")
	}
	if pl.Finished {
		return nil, ErrPlayerFinished
	}

	pl.HasPassed = true
	
	// TODO: Update game.CurrentTurn

	return []Event{
		{
			Kind:    EventTurnPassed,
			Payload: TurnPassedPayload{UserID: actorUserID},
		},
	}, nil
}

func (s *Service) shuffle(deck []domain.Card) {
	s.rng.Shuffle(len(deck), func(i, j int) { deck[i], deck[j] = deck[j], deck[i] })
}
