package domain

// LowestAvailableSeat returns the first free seat index (0-based). If full, returns 0.
func LowestAvailableSeat(seats *[4]string) int {
	for i := 0; i < len(seats); i++ {
		if seats[i] == "" {
			return i
		}
	}
	return 0
}

// BuildLabelPayload produces the values needed for match label advertisement.
type LabelPayload struct {
	Open  bool   `json:"open"`
	Game  string `json:"game"`
	Phase string `json:"phase"`
}

// ComputeLabel derives the advertised label from match state.
func ComputeLabel(s *MatchState) LabelPayload {
	open := s.Phase == PhaseLobby && len(s.Players) < 4
	return LabelPayload{Open: open, Game: "tienlen", Phase: string(s.Phase)}
}

// NewDeck produces an ordered 52-card deck.
func NewDeck() []Card {
	suits := []string{"S", "H", "D", "C"}
	var deck []Card
	for _, s := range suits {
		for r := 0; r <= 12; r++ {
			deck = append(deck, Card{Suit: s, Rank: r})
		}
	}
	return deck
}

// RemoveCards removes the provided cards from a hand (O(n*m) prototype).
func RemoveCards(hand []Card, played []Card) []Card {
	out := append([]Card{}, hand...)
	for _, pc := range played {
		for i := 0; i < len(out); i++ {
			if out[i].Suit == pc.Suit && out[i].Rank == pc.Rank {
				out = append(out[:i], out[i+1:]...)
				break
			}
		}
	}
	return out
}

// CountPlayersWithCards returns active players that still hold cards.
func CountPlayersWithCards(s *MatchState) int {
	n := 0
	for _, pl := range s.Players {
		if !pl.Finished && len(pl.Hand) > 0 {
			n++
		}
	}
	return n
}
