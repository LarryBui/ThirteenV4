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
func (s *Service) StartGame(playerIDs []string, lastWinnerID string) (*domain.Game, []Event, error) {
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

	if len(activePlayers) < MinPlayersToStartGame {
		return nil, nil, ErrTooFewPlayers
	}

	deck := domain.NewDeck()
	deck = domain.ShuffleDeck(deck)

	game := &domain.Game{
		Phase:   domain.PhasePlaying,
		Players: activePlayers,
	}

	// Deal cards
	cardIdx := 0
	for _, userID := range seats { // Iterate in seat order
		pl := activePlayers[userID]
		pl.Hand = append([]domain.Card{}, deck[cardIdx:cardIdx+13]...)
		domain.SortHand(pl.Hand) // Sort hand after dealing
		pl.HasPassed = false
		pl.Finished = false
		cardIdx += 13
	}

	// Determine FirstTurn
	firstTurnUserID := ""

	// If last winner is provided and is in the current game, they go first
	if lastWinnerID != "" {
		if _, ok := activePlayers[lastWinnerID]; ok {
			firstTurnUserID = lastWinnerID
		}
	}

	// Fallback to lowest card if no last winner or last winner left
	if firstTurnUserID == "" {
		// Find the player with the absolute lowest card
		var lowestCardVal int32 = 9999

		for _, pl := range game.Players {
			for _, card := range pl.Hand {
				// Rank 0-12 (3 to 2), Suit 0-3 (Spade to Heart)
				// Value = Rank * 4 + Suit
				val := card.Rank*4 + card.Suit
				if val < lowestCardVal {
					lowestCardVal = val
					firstTurnUserID = pl.UserID
				}
			}
		}

		if firstTurnUserID == "" {
			// Should strictly never happen if there are players with cards
			firstTurnUserID = seats[0]
		}
	}
	game.CurrentTurn = firstTurnUserID

	events := make([]Event, 0, len(activePlayers))

	// Create GameStarted event for EACH player, containing their private hand
	for _, userID := range seats {
		pl := activePlayers[userID]
		events = append(events, Event{
			Kind: EventGameStarted,
			Payload: GameStartedPayload{
				Phase:           game.Phase,
				FirstTurnUserID: game.CurrentTurn,
				Hand:            pl.Hand,
			},
			Recipients: []string{pl.UserID},
		})
	}

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
	newRound := game.LastPlayedCombination.Type == domain.Invalid
	if !newRound { // If there was a previous play
		if !domain.CanBeat(game.LastPlayedCombination.Cards, playedCombo.Cards) {
			return nil, ErrCannotBeat
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
				UserID:   actorUserID,
				Cards:    cards,
				NewRound: newRound,
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
			NewRound:       newRound,
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

	// Check if the round should reset.
	// The round resets if only one active (non-finished) player remains who hasn't passed.
	// This player is the "winner" of the round and starts the new one.
	activeCount := 0
	activeNotPassedCount := 0
	var lastActivePlayerID string

	for _, p := range game.Players {
		if !p.Finished {
			activeCount++
			if !p.HasPassed {
				activeNotPassedCount++
				lastActivePlayerID = p.UserID
			}
		}
	}

	// If everyone passed (activeNotPassedCount == 0), it means the last person to play 
	// (who is now finished) won the round.
	// The turn should go to the next active player after them, and the board clears.
	// OR if only one person hasn't passed, they won.

	newRound := false
	nextTurnID := ""

	if activeNotPassedCount == 0 {
		// This happens if the person who played last just finished, and everyone else passed.
		// The round ends. The turn goes to the next active player after the person who finished.
		// We need to find who is next after LastPlayerToPlay.
		newRound = true
		nextTurnID = s.findNextActivePlayerInOrder(game, game.LastPlayerToPlay)
	} else if activeNotPassedCount == 1 {
		// Standard case: Everyone else passed, leaving one winner.
		newRound = true
		nextTurnID = lastActivePlayerID
	} else {
		// Round continues, find next player
		nextTurnID = s.findNextPlayer(game, actorUserID, game.Players)
	}

	game.CurrentTurn = nextTurnID

	if newRound {
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
			Kind: EventTurnPassed,
			Payload: TurnPassedPayload{
				UserID:         actorUserID,
				NextTurnUserID: game.CurrentTurn,
				NewRound:       newRound,
			},
		},
	}, nil
}

// findNextActivePlayerInOrder finds the next active player seat-wise after the given userID.
func (s *Service) findNextActivePlayerInOrder(game *domain.Game, currentUserID string) string {
	// Get players in seat order
	var orderedPlayers []*domain.Player
	for _, pl := range game.Players {
		orderedPlayers = append(orderedPlayers, pl)
	}
	sort.Slice(orderedPlayers, func(i, j int) bool {
		return orderedPlayers[i].Seat < orderedPlayers[j].Seat
	})

	// Find current index
	startIdx := -1
	for i, pl := range orderedPlayers {
		if pl.UserID == currentUserID {
			startIdx = i
			break
		}
	}

	if startIdx == -1 {
		return ""
	}

	// Loop to find next not finished
	for i := 1; i <= len(orderedPlayers); i++ {
		idx := (startIdx + i) % len(orderedPlayers)
		if !orderedPlayers[idx].Finished {
			return orderedPlayers[idx].UserID
		}
	}
	return ""
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
