package internal

import (
	"tienlen/internal/domain"
)

// BossStats provides insights into the hand relative to the game state.
type BossStats struct {
	UnseenCards   []domain.Card
	BossSingles   []domain.Card // Singles in hand that cannot be beaten
	BossPairs     [][]domain.Card
	Dominance     float64       // 0 to 1, how much "control" the hand has
}

// AnalyzeHand performs card counting and identifies "boss" combinations.
func AnalyzeHand(hand []domain.Card, discards []domain.Card) BossStats {
	// 1. Calculate Unseen
	allCards := domain.NewDeck()
	unseen := removeSubset(allCards, discards)
	unseen = removeSubset(unseen, hand)

	stats := BossStats{
		UnseenCards: unseen,
	}

	if len(unseen) == 0 {
		stats.Dominance = 1.0
		// Every card is a boss
		stats.BossSingles = hand
		return stats
	}

	// 2. Identify Boss Singles
	// A single is a boss if its power > highest power in unseen.
	highestUnseenPower := getHighestPower(unseen)
	for _, c := range hand {
		if cardPower(c) > highestUnseenPower {
			stats.BossSingles = append(stats.BossSingles, c)
		}
	}

	// 3. Dominance calculation (Heuristic)
	// Ratio of hand power vs unseen power
	handPower := 0
	for _, c := range hand {
		handPower += int(cardPower(c))
	}
	avgHandPower := float64(handPower) / float64(len(hand))

	unseenPower := 0
	for _, c := range unseen {
		unseenPower += int(cardPower(c))
	}
	avgUnseenPower := float64(unseenPower) / float64(len(unseen))

	stats.Dominance = avgHandPower / (avgHandPower + avgUnseenPower)

	return stats
}

func getHighestPower(cards []domain.Card) int32 {
	var max int32 = -1
	for _, c := range cards {
		p := cardPower(c)
		if p > max {
			max = p
		}
	}
	return max
}

func cardPower(c domain.Card) int32 {
	return c.Rank*4 + c.Suit
}
