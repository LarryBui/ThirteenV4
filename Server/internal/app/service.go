package app

import (
	"errors"
	"math/rand"
	"sort"
	"time"

	"tienlen/internal/config"
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
func (s *Service) StartGame(playerIDs []string, lastWinnerSeat int, baseBet int64) (*domain.Game, []Event, error) {
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
		Phase:                domain.PhasePlaying,
		Players:              activePlayers,
		LastPlayerToPlaySeat: -1, // Initialize to -1 as no one has played yet
		BaseBet:              baseBet,
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
	firstTurnSeat := 0

	// If last winner seat is valid (0-3), check if that seat is occupied
	if lastWinnerSeat >= 0 && lastWinnerSeat < 4 {
		winnerID := playerIDs[lastWinnerSeat]
		if winnerID != "" {
			if _, ok := activePlayers[winnerID]; ok {
				firstTurnSeat = lastWinnerSeat
			}
		}
	}

	if lastWinnerSeat < 0 || playerIDs[firstTurnSeat] == "" {
		// Fallback to lowest card if no last winner or last winner left
		var lowestCardVal int32 = 9999
		lowestSeat := 0

		for _, pl := range game.Players {
			for _, card := range pl.Hand {
				// Rank 0-12 (3 to 2), Suit 0-3 (Spade to Heart)
				// Value = Rank * 4 + Suit
				val := card.Rank*4 + card.Suit
				if val < lowestCardVal {
					lowestCardVal = val
					lowestSeat = pl.Seat - 1 // Convert 1-based domain seat to 0-based
				}
			}
		}
		firstTurnSeat = lowestSeat
	}
	game.CurrentTurn = firstTurnSeat

	events := make([]Event, 0, len(activePlayers))

	// Create GameStarted event for EACH player, containing their private hand
	for _, userID := range seats {
		pl := activePlayers[userID]
		events = append(events, Event{
			Kind: EventGameStarted,
			Payload: GameStartedPayload{
				Phase:         game.Phase,
				FirstTurnSeat: game.CurrentTurn,
				Hand:          pl.Hand,
			},
			Recipients: []string{pl.UserID},
		})
	}

	return game, events, nil
}

// PlayCards processes a play action and emits resulting events.

func (s *Service) PlayCards(game *domain.Game, actorSeat int, cards []domain.Card) ([]Event, error) {

	if game.Phase != domain.PhasePlaying {

		return nil, ErrNotPlaying

	}

	// Find player by seat

	var pl *domain.Player

	for _, p := range game.Players {

		if p.Seat-1 == actorSeat {

			pl = p

			break

		}

	}

	if pl == nil {

		return nil, ErrUnknownPlayer

	}

	if game.CurrentTurn != actorSeat {

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

	game.LastPlayerToPlaySeat = actorSeat

	// Add to discards

	game.Discards = append(game.Discards, cards...)

	events := []Event{

		{

			Kind: EventCardPlayed,

			Payload: CardPlayedPayload{

				Seat: actorSeat,

				Cards: cards,

				NewRound: newRound,
			},
		},
	}

	if len(pl.Hand) == 0 && !pl.Finished {

		pl.Finished = true

		game.FinishOrderSeats = append(game.FinishOrderSeats, actorSeat)

	}

	// Check if game ended

	if domain.CountPlayersWithCards(game) <= 1 {

		game.Phase = domain.PhaseEnded

		settlement := game.CalculateSettlement()

		// Apply Tax to positive winnings

		cfg := config.GetGameConfig()

		taxRate := 0.05 // Default

		if cfg != nil {

			taxRate = cfg.TaxRate

		}

		finalChanges := make(map[string]int64)

		for uid, amount := range settlement.BalanceChanges {

			if amount > 0 {

				afterTax := float64(amount) * (1.0 - taxRate)

				finalChanges[uid] = int64(afterTax)

			} else {

				finalChanges[uid] = amount

			}

		}

		events = append(events, Event{

			Kind: EventGameEnded,

			Payload: GameEndedPayload{

				FinishOrderSeats: game.FinishOrderSeats,

				BalanceChanges: finalChanges,
			},
		})

	} else {

		// Advance turn

		game.CurrentTurn = s.findNextPlayer(game, actorSeat, game.Players)

		// Note: If findNextPlayer returns actorSeat, it means everyone else is finished/passed.

		// But actorSeat just played, so they can't be passed.

		// However, if actorSeat just played, they initiated a new round or continued.

		// The logic for clearing the board happens in PassTurn.

		events[0].Payload = CardPlayedPayload{ // Update payload to include next turn

			Seat: actorSeat,

			Cards: cards,

			NextTurnSeat: game.CurrentTurn,

			NewRound: newRound,
		}

	}

	return events, nil

}

// PassTurn marks a player's pass action.
func (s *Service) PassTurn(game *domain.Game, actorSeat int) ([]Event, error) {
	if game.Phase != domain.PhasePlaying {
		return nil, ErrNotPlaying
	}

	// Find player by seat
	var pl *domain.Player
	for _, p := range game.Players {
		if p.Seat-1 == actorSeat {
			pl = p
			break
		}
	}

	if pl == nil {
		return nil, ErrUnknownPlayer
	}
	if game.CurrentTurn != actorSeat {
		return nil, ErrNotYourTurn
	}
	if pl.Finished {
		return nil, ErrPlayerFinished
	}

	pl.HasPassed = true

	// Check if the round should reset.
	// The round resets if only one active (non-finished) player remains who hasn't passed.
	// This player is the "winner" of the round and starts the new one.
	activeNotPassedCount := 0
	var lastActivePlayerSeat int

	for _, p := range game.Players {
		if !p.Finished {
			if !p.HasPassed {
				activeNotPassedCount++
				lastActivePlayerSeat = p.Seat - 1
			}
		}
	}

	newRound := false
	nextTurnSeat := 0

	if activeNotPassedCount == 0 {
		// This happens if the person who played last just finished, and everyone else passed.
		// The round ends. The turn goes to the next active player after the person who finished.
		newRound = true
		nextTurnSeat = s.findNextActivePlayerInOrder(game, game.LastPlayerToPlaySeat)
	} else if activeNotPassedCount == 1 {
		// Standard case: Everyone else passed, leaving one winner.
		newRound = true
		nextTurnSeat = lastActivePlayerSeat
	} else {
		// Round continues, find next player
		nextTurnSeat = s.findNextPlayer(game, actorSeat, game.Players)
	}

	game.CurrentTurn = nextTurnSeat

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
				Seat:         actorSeat,
				NextTurnSeat: game.CurrentTurn,
				NewRound:     newRound,
			},
		},
	}, nil
}

// findNextActivePlayerInOrder finds the next active player seat-wise after the given seat.
func (s *Service) findNextActivePlayerInOrder(game *domain.Game, currentSeat int) int {
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
		if pl.Seat-1 == currentSeat {
			startIdx = i
			break
		}
	}

	if startIdx == -1 {
		return 0
	}

	// Loop to find next not finished
	for i := 1; i <= len(orderedPlayers); i++ {
		idx := (startIdx + i) % len(orderedPlayers)
		if !orderedPlayers[idx].Finished {
			return orderedPlayers[idx].Seat - 1
		}
	}
	return 0
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
func (s *Service) findNextPlayer(game *domain.Game, currentSeat int, allPlayers map[string]*domain.Player) int {
	// Get players in seat order
	var orderedPlayers []*domain.Player
	for _, pl := range allPlayers {
		orderedPlayers = append(orderedPlayers, pl)
	}
	sort.Slice(orderedPlayers, func(i, j int) bool {
		return orderedPlayers[i].Seat < orderedPlayers[j].Seat
	})

	// Iterate through players to find the next active one
	// Note: currentSeat is 0-based, orderedPlayers uses 1-based logic usually but here we just need relative order

	// Convert 0-based seat to index in ordered array (which is sorted 0..3 effectively if seats are 1..4)
	// Actually, safer to find index by Seat value
	currentIndex := -1
	for i, pl := range orderedPlayers {
		if pl.Seat-1 == currentSeat {
			currentIndex = i
			break
		}
	}

	if currentIndex == -1 {
		return 0 // Should not happen
	}

	// Iterate through players to find the next active one
	for i := 1; i <= len(orderedPlayers); i++ {
		nextIndex := (currentIndex + i) % len(orderedPlayers)
		nextPlayer := orderedPlayers[nextIndex]

		if !nextPlayer.Finished && !nextPlayer.HasPassed {
			return nextPlayer.Seat - 1
		}
	}

	return currentSeat // Fallback
}

// TimeoutTurn handles the logic when a player's turn timer expires.
func (s *Service) TimeoutTurn(game *domain.Game, actorSeat int) ([]Event, error) {
	if game.Phase != domain.PhasePlaying {
		return nil, ErrNotPlaying
	}

	// 1. Identify if it's a new round (Leader)
	isNewRound := game.LastPlayedCombination.Type == domain.Invalid

	if isNewRound {
		// Must play a card (cannot pass on new round)
		// Find player's hand
		var player *domain.Player
		for _, p := range game.Players {
			if p.Seat-1 == actorSeat {
				player = p
				break
			}
		}

		if player == nil || len(player.Hand) == 0 {
			return nil, ErrUnknownPlayer
		}

		// Find smallest card (Rank * 4 + Suit)
		smallestIdx := 0
		minVal := int32(9999)
		for i, c := range player.Hand {
			val := c.Rank*4 + c.Suit
			if val < minVal {
				minVal = val
				smallestIdx = i
			}
		}

		// Force play the smallest single card
		return s.PlayCards(game, actorSeat, []domain.Card{player.Hand[smallestIdx]})
	}

	// 2. Mid-round: Force Pass
	return s.PassTurn(game, actorSeat)
}
