package domain

// Phase represents the lifecycle stage of a match.
type Phase string

const (
	// PhaseLobby indicates the match is waiting for players.
	PhaseLobby Phase = "lobby"
	// PhasePlaying indicates the match is actively in progress.
	PhasePlaying Phase = "playing"
	// PhaseEnded indicates the match has finished.
	PhaseEnded Phase = "ended"
)

// Card represents a standard playing card in domain terms.
type Card struct {
	Suit string
	Rank int
}

// Player holds the domain state for a player in a match.
type Player struct {
	UserID    string
	Seat      int
	IsOwner   bool
	Hand      []Card
	HasPassed bool
	Finished  bool
}

// MatchState captures the domain state for a single match instance.
type MatchState struct {
	Phase       Phase
	Players     map[string]*Player
	Seats       [4]string
	OwnerUserID string
	FinishOrder []string
}

// LowestAvailableSeat returns the lowest index of an empty seat.
func LowestAvailableSeat(seats *[4]string) int {
	for i, userID := range seats {
		if userID == "" {
			return i
		}
	}
	panic("no available seats")
}

// CountPlayersWithCards returns the number of active players with cards remaining.
func CountPlayersWithCards(state *MatchState) int {
	count := 0
	for _, player := range state.Players {
		if !player.Finished && len(player.Hand) > 0 {
			count++
		}
	}
	return count
}

// RemoveCards removes the specified cards from a hand and returns the updated hand.
func RemoveCards(hand []Card, toRemove []Card) []Card {
	if len(toRemove) == 0 || len(hand) == 0 {
		return hand
	}

	removeCounts := make(map[Card]int, len(toRemove))
	for _, card := range toRemove {
		removeCounts[card]++
	}

	updated := make([]Card, 0, len(hand))
	for _, card := range hand {
		if count, ok := removeCounts[card]; ok && count > 0 {
			removeCounts[card] = count - 1
			continue
		}
		updated = append(updated, card)
	}

	return updated
}
