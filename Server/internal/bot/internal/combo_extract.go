package internal

import (
	"sort"
	"tienlen/internal/domain"
)

type straightStats struct {
	Count  int
	Cards  int
	MaxLen int
}

type pineStats struct {
	Count    int
	Cards    int
	MaxPairs int
}

// removeSubset removes the specified cards from a source slice using multiset semantics.
func removeSubset(source []domain.Card, subset []domain.Card) []domain.Card {
	// Inefficient O(N*M) but N is small (<= 13).
	rem := make([]domain.Card, 0, len(source))

	counts := make(map[domain.Card]int)
	for _, c := range subset {
		counts[c]++
	}

	for _, c := range source {
		if counts[c] > 0 {
			counts[c]--
		} else {
			rem = append(rem, c)
		}
	}
	return rem
}

// extractStraights removes the longest straight sequences repeatedly and returns stats.
func extractStraights(cards []domain.Card) ([]domain.Card, straightStats) {
	stats := straightStats{}

	// Greedy straight finding: find the longest possible straight starting from the lowest card.
	for {
		found := false

		rankMap := make(map[int32][]domain.Card)
		var ranks []int
		for _, c := range cards {
			if c.Rank == 12 { // 2s cannot be in a straight
				continue
			}
			if _, ok := rankMap[c.Rank]; !ok {
				ranks = append(ranks, int(c.Rank))
			}
			rankMap[c.Rank] = append(rankMap[c.Rank], c)
		}
		sort.Ints(ranks)

		bestStart := -1
		bestLen := 0
		for i := 0; i < len(ranks); i++ {
			currLen := 1
			for j := i + 1; j < len(ranks); j++ {
				if ranks[j] == ranks[j-1]+1 {
					currLen++
				} else {
					break
				}
			}
			if currLen >= 3 && currLen > bestLen {
				bestLen = currLen
				bestStart = i
			}
		}

		if bestLen >= 3 {
			straight := make([]domain.Card, 0, bestLen)
			for k := 0; k < bestLen; k++ {
				r := int32(ranks[bestStart+k])
				straight = append(straight, rankMap[r][0])
			}

			cards = removeSubset(cards, straight)
			stats.Cards += bestLen
			stats.Count++
			if bestLen > stats.MaxLen {
				stats.MaxLen = bestLen
			}
			found = true
		}

		if !found {
			break
		}
	}

	return cards, stats
}

// extractPines removes consecutive-pair sequences (3+ pairs) greedily and returns stats.
func extractPines(cards []domain.Card) ([]domain.Card, pineStats) {
	stats := pineStats{}
	if len(cards) < 6 {
		return cards, stats
	}

	counts := make(map[int32]int)
	for _, c := range cards {
		counts[c.Rank]++
	}

	for {
		var ranks []int
		for rank, count := range counts {
			if rank == 12 { // 2s cannot be part of consecutive pairs
				continue
			}
			if count >= 2 {
				ranks = append(ranks, int(rank))
			}
		}
		sort.Ints(ranks)

		bestStart := -1
		bestLen := 0
		for i := 0; i < len(ranks); i++ {
			currLen := 1
			for j := i + 1; j < len(ranks); j++ {
				if ranks[j] == ranks[j-1]+1 {
					currLen++
				} else {
					break
				}
			}
			if currLen >= 3 && currLen > bestLen {
				bestLen = currLen
				bestStart = i
			}
		}

		if bestLen < 3 {
			break
		}

		for k := 0; k < bestLen; k++ {
			r := int32(ranks[bestStart+k])
			counts[r] -= 2
		}

		stats.Count++
		stats.Cards += bestLen * 2
		if bestLen > stats.MaxPairs {
			stats.MaxPairs = bestLen
		}
	}

	remaining := make([]domain.Card, 0, len(cards)-stats.Cards)
	for _, c := range cards {
		if counts[c.Rank] > 0 {
			remaining = append(remaining, c)
			counts[c.Rank]--
		}
	}

	return remaining, stats
}
