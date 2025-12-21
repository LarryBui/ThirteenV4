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
	Suit int32
	Rank int32
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

// Game captures the pure domain state for a single game instance (playing phase).
type Game struct {
	Phase                 Phase
	Players               map[string]*Player
	FinishOrderSeats      []int // Seat indices (0-based)
	CurrentTurn           int   // Seat index (0-based)
	LastPlayedCombination CardCombination
	LastPlayerToPlaySeat  int // Seat index (0-based)
	BaseBet               int64
	Discards              []Card // All cards played in this game so far
}

// Settlement represents the net gold change for each player.
type Settlement struct {
	BalanceChanges map[string]int64 // UserID -> Gold (+/-)
}

// CalculateSettlement computes the payouts based on finishing rank.
// Payout Matrix (Multiplier * BaseBet):
// 4 Players: 1st(+2), 2nd(+1), 3rd(-1), 4th(-2)
// 3 Players: 1st(+3), 2nd(-1), 3rd(-2)
// 2 Players: 1st(+1), 2nd(-1)
func (g *Game) CalculateSettlement() Settlement {
	changes := make(map[string]int64)
	playerCount := len(g.Players)
	
	// Create a map of seat index to user ID for easy lookup
	seatToUser := make(map[int]string)
	for uid, p := range g.Players {
		seatToUser[p.Seat-1] = uid
	}

	// Determine multipliers based on player count
	var multipliers []int64
	switch playerCount {
	case 4:
		multipliers = []int64{2, 1, -1, -2}
	case 3:
		multipliers = []int64{3, -1, -2}
	case 2:
		multipliers = []int64{1, -1}
	default:
		// Fallback for 1 player (testing) or >4 (error)
		multipliers = make([]int64, playerCount)
	}

	// Apply payouts based on rank order
	// FinishOrderSeats contains seat indices of players in order of finishing (1st, 2nd...)
	// Note: Players who haven't finished yet are implicitly last.
	// We need to construct a full ordered list including those still playing (if forced end).
	
	fullRankOrder := make([]int, 0, playerCount)
	fullRankOrder = append(fullRankOrder, g.FinishOrderSeats...)
	
	// Add remaining players who haven't finished (shouldn't happen in normal flow as game ends when 1 left)
	// But for safety, add them.
	present := make(map[int]bool)
	for _, seat := range g.FinishOrderSeats {
		present[seat] = true
	}
	for i := 0; i < 4; i++ {
		if _, ok := seatToUser[i]; ok { // If seat is occupied
			if !present[i] {
				// We append implicitly based on seat order if they are tied for "last"
				fullRankOrder = append(fullRankOrder, i)
			}
		}
	}

	// Calculate changes
	for rank, seat := range fullRankOrder {
		if rank >= len(multipliers) {
			break
		}
		
		uid := seatToUser[seat]
		amount := multipliers[rank] * g.BaseBet
		changes[uid] = amount
	}

	return Settlement{BalanceChanges: changes}
}

// CountPlayersWithCards returns the number of active players with cards remaining.
func CountPlayersWithCards(game *Game) int {
	count := 0
	for _, player := range game.Players {
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
