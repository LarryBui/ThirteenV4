package internal

import "tienlen/internal/domain"

// HandProfile summarizes a hand's strategic structure for phase-aware scoring.
type HandProfile struct {
	TotalCards     int
	Singles        int
	Pairs          int
	Triples        int
	Quads          int
	Straights      int
	StraightCards  int
	MaxStraightLen int
	Pines          int
	PineCards      int
	MaxPinePairs   int
	Twos           int
}

// ProfileHand analyzes a hand and extracts combo counts using a greedy structure pass.
func ProfileHand(hand []domain.Card) HandProfile {
	profile := HandProfile{TotalCards: len(hand)}
	if len(hand) == 0 {
		return profile
	}

	cards := make([]domain.Card, len(hand))
	copy(cards, hand)
	domain.SortHand(cards)

	for _, c := range cards {
		if c.Rank == 12 {
			profile.Twos++
		}
	}

	var pStats pineStats
	cards, pStats = extractPines(cards)
	profile.Pines = pStats.Count
	profile.PineCards = pStats.Cards
	profile.MaxPinePairs = pStats.MaxPairs

	var sStats straightStats
	cards, sStats = extractStraights(cards)
	profile.Straights = sStats.Count
	profile.StraightCards = sStats.Cards
	profile.MaxStraightLen = sStats.MaxLen

	rankCounts := make(map[int32]int)
	for _, c := range cards {
		rankCounts[c.Rank]++
	}
	for _, count := range rankCounts {
		switch count {
		case 4:
			profile.Quads++
		case 3:
			profile.Triples++
		case 2:
			profile.Pairs++
		case 1:
			profile.Singles++
		}
	}

	return profile
}
