package app

import (
	"errors"
	"math/rand"
	"sort"
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
	ErrNotOwner         = errors.New("actor is not match owner")
	ErrNotPlaying       = errors.New("match not in playing phase")
	ErrTooFewPlayers    = errors.New("not enough players to start")
	ErrUnknownPlayer    = errors.New("player not found")
	ErrPlayerFinished   = errors.New("player already finished")
	ErrNotYourTurn      = errors.New("not your turn")
	ErrInvalidPlay      = errors.New("invalid card play")
	ErrCardsNotInHand   = errors.New("cards not in hand")
	ErrCannotBeat       = errors.New("cannot beat previous play")
	ErrGameNotEnded     = errors.New("game not ended")
	ErrGameAlreadyEnded = errors.New("game already ended")
)

// StartGame initializes a new Game domain object with the provided players.
// It expects a list of userIDs representing the players in seat order (empty strings for empty seats).
func (s *Service) StartGame(playerIDs []string) (*domain.Game, []Event, error) {
	activePlayers := make(map[string]*domain.Player)
	var seats []string // Active players' UIDs in seat order

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
		domain.SortCards(pl.Hand) // Sort hand after dealing
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

	// Determine FirstTurn: Player with 3 of Spades
	firstTurnUserID := ""
	for _, pl := range game.Players {
		for _, card := range pl.Hand {
			if card.Suit == "S" && card.Rank == 0 { // 3 of Spades
				firstTurnUserID = pl.UserID
				break
			}
		}
		if firstTurnUserID != "" {
			break
		}
	}
	if firstTurnUserID == "" {
		// Fallback if 3 of Spades somehow not found (shouldn't happen with full deck)
		firstTurnUserID = seats[0]
	}
	game.CurrentTurn = firstTurnUserID

	events = append(events, Event{
		Kind:    EventGameStarted,
		Payload: GameStartedPayload{Phase: game.Phase, FirstTurnUserID: game.CurrentTurn},
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
		return nil, ErrNotYourTurn
	}
	if pl.Finished {
		return nil, ErrPlayerFinished
	}
	if len(cards) == 0 {
		return nil, ErrInvalidPlay // Must play at least one card
	}

	// 1. Verify player has the cards
	if !playerHasCards(pl.Hand, cards) {
		return nil, ErrCardsNotInHand
	}

	// 2. Identify the combination of played cards
	playedCombo := domain.IdentifyCombination(cards)
	if playedCombo.Type == domain.Invalid {
		return nil, ErrInvalidPlay
	}

	// 3. Validate against previous play
	if game.LastPlayedCombination.Type != domain.Invalid { // If there was a previous play
		if !domain.CanBeat(playedCombo, game.LastPlayedCombination) {
			return nil, ErrCannotBeat
		}
	} else {
		// First play of the game must be 3 of Spades
		has3Spades := false
		for _, c := range pl.Hand {
			if c.Suit == "S" && c.Rank == 0 {
				has3Spades = true
				break
			}
		}

		if has3Spades {
			played3Spades := false
			for _, c := range cards {
				if c.Suit == "S" && c.Rank == 0 {
					played3Spades = true
					break
				}
			}
			if !played3Spades {
				return nil, errors.New("must play 3 of Spades in first turn")
			}
		}
	}

	// If valid, update game state
	pl.Hand = domain.RemoveCards(pl.Hand, cards)
	game.LastPlayedCombination = playedCombo
	game.LastPlayerToPlay = actorUserID
	
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

	// Check if game ended
	if domain.CountPlayersWithCards(game) <= 1 {
		game.Phase = domain.PhaseEnded
		events = append(events, Event{
			Kind:    EventGameEnded,
			Payload: GameEndedPayload{FinishOrder: game.FinishOrder},
		})
	} else {
		// Advance turn
		game.CurrentTurn = s.findNextPlayer(game, actorUserID, game.Players)
		
		// Note: If findNextPlayer returns actorUserID, it means everyone else is finished/passed.
		// But actorUserID just played, so they can't be passed.
		// However, if actorUserID just played, they initiated a new round or continued.
		// The logic for clearing the board happens in PassTurn.
		
		events[0].Payload = CardPlayedPayload{ // Update payload to include next turn
			UserID:         actorUserID,
			Cards:          cards,
			NextTurnUserID: game.CurrentTurn,
		}
	}

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
		return nil, ErrNotYourTurn
	}
	if pl.Finished {
		return nil, ErrPlayerFinished
	}

	pl.HasPassed = true
	
	// Advance turn
	nextPlayerID := s.findNextPlayer(game, actorUserID, game.Players)
	game.CurrentTurn = nextPlayerID

	// If the turn comes back to the player who played the last combo, 
	// they start a new round.
	if game.CurrentTurn == game.LastPlayerToPlay {
		// Reset LastPlayedCombination for new round
		game.LastPlayedCombination = domain.CardCombination{Type: domain.Invalid}
		// Reset pass status for all active players
		for _, p := range game.Players {
			if !p.Finished {
				p.HasPassed = false
			}
		}
	}

	return []Event{
		{
			Kind:    EventTurnPassed,
			Payload: TurnPassedPayload{UserID: actorUserID, NextTurnUserID: game.CurrentTurn},
		},
	}, nil
}

// playerHasCards checks if a player's hand contains all cards in 'toCheck'.
func playerHasCards(hand []domain.Card, toCheck []domain.Card) bool {
	handCounts := make(map[domain.Card]int)
	for _, card := range hand {
		handCounts[card]++
	}

	for _, card := range toCheck {
		if count, ok := handCounts[card]; !ok || count == 0 {
			return false // Player does not have this card or not enough of it
		}
		handCounts[card]--
	}
	return true
}

// findNextPlayer determines whose turn it is next.
func (s *Service) findNextPlayer(game *domain.Game, currentPlayerID string, allPlayers map[string]*domain.Player) string {
	// Get players in seat order
	var orderedPlayers []*domain.Player
	for _, pl := range allPlayers {
		orderedPlayers = append(orderedPlayers, pl)
	}
	sort.Slice(orderedPlayers, func(i, j int) bool {
		return orderedPlayers[i].Seat < orderedPlayers[j].Seat
	})

	// Find current player's index
	currentIndex := -1
	for i, pl := range orderedPlayers {
		if pl.UserID == currentPlayerID {
			currentIndex = i
			break
		}
	}

	if currentIndex == -1 {
		return "" // Should not happen
	}

	// Iterate through players to find the next active one
	for i := 1; i <= len(orderedPlayers); i++ {
		nextIndex := (currentIndex + i) % len(orderedPlayers)
		nextPlayer := orderedPlayers[nextIndex]

		if !nextPlayer.Finished && !nextPlayer.HasPassed {
			return nextPlayer.UserID
		}
	}

	return currentPlayerID // Fallback
}

func (s *Service) shuffle(deck []domain.Card) {
	s.rng.Shuffle(len(deck), func(i, j int) { deck[i], deck[j] = deck[j], deck[i] })
}