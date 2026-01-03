package internal

import (
	"tienlen/internal/domain"
)

// OrganizedHand represents a tactical partitioning of a player's hand.
type OrganizedHand struct {
	Bombs     []domain.CardCombination
	Straights []domain.CardCombination
	Triples   []domain.CardCombination
	Pairs     []domain.CardCombination
	Trash     []domain.Card // Cards not part of any strong structure
}

// GetTacticalOptions generates multiple valid partitioning strategies for the hand.
func GetTacticalOptions(hand []domain.Card) []OrganizedHand {
	// Option 1: Straights First (Standard/Aggressive)
	opt1 := PartitionHand(hand)

	// Option 2: Pairs First (Defensive/Cohesive)
	opt2 := PartitionHandPairsFirst(hand)

	return []OrganizedHand{opt1, opt2}
}

// PartitionHandPairsFirst organizes a hand prioritizing Pairs over Straights.
func PartitionHandPairsFirst(hand []domain.Card) OrganizedHand {
	organized := OrganizedHand{}
	if len(hand) == 0 {
		return organized
	}

	// Working copy of the hand
	pool := make([]domain.Card, len(hand))
	copy(pool, hand)
	domain.SortHand(pool)

	// Priority 1: Extract Bombs (Quads and Pines) - Always top priority
	organized.Bombs, pool = ExtractBombs(pool)

	// Priority 2: Extract Triples and Pairs (Sets) - PRIORITIZED over Straights here
	organized.Triples, organized.Pairs, pool = ExtractSets(pool)

	// Priority 3: Extract Straights
	organized.Straights, pool = ExtractStraights(pool)

	organized.Trash = pool

	return organized
}

// PartitionHand organizes a hand into logical structures following a priority hierarchy.
func PartitionHand(hand []domain.Card) OrganizedHand {
	organized := OrganizedHand{}
	if len(hand) == 0 {
		return organized
	}

	// Working copy of the hand
	pool := make([]domain.Card, len(hand))
	copy(pool, hand)
	domain.SortHand(pool)

	// Priority 1: Extract Bombs (Quads and Pines)
	organized.Bombs, pool = ExtractBombs(pool)

	// Priority 2: Extract Straights
	organized.Straights, pool = ExtractStraights(pool)

	// Priority 3: Extract Triples and Pairs
	organized.Triples, organized.Pairs, pool = ExtractSets(pool)

	organized.Trash = pool

	return organized
}

// ExtractSets identifies and removes triples and pairs from the pool.
func ExtractSets(hand []domain.Card) (triples []domain.CardCombination, pairs []domain.CardCombination, remaining []domain.Card) {
	tempPool := make([]domain.Card, len(hand))
	copy(tempPool, hand)
	domain.SortHand(tempPool)

	rankCounts := make(map[int32][]domain.Card)
	var ranks []int
	for _, c := range tempPool {
		if _, ok := rankCounts[c.Rank]; !ok {
			ranks = append(ranks, int(c.Rank))
		}
		rankCounts[c.Rank] = append(rankCounts[c.Rank], c)
	}

	// Sort ranks for deterministic extraction
	for i := 0; i < len(ranks); i++ {
		for j := i + 1; j < len(ranks); j++ {
			if ranks[i] > ranks[j] {
				ranks[i], ranks[j] = ranks[j], ranks[i]
			}
		}
	}

	finalRemaining := make([]domain.Card, 0)

	for _, r := range ranks {
		cards := rankCounts[int32(r)]
		switch len(cards) {
		case 3:
			triples = append(triples, domain.IdentifyCombination(cards))
		case 2:
			pairs = append(pairs, domain.IdentifyCombination(cards))
		case 1:
			finalRemaining = append(finalRemaining, cards[0])
		default:
			// This shouldn't happen if Quads were extracted first, but for safety:
			finalRemaining = append(finalRemaining, cards...)
		}
	}

	return triples, pairs, finalRemaining
}

// ExtractStraights identifies and removes straight sequences (length >= 3) from the pool.
func ExtractStraights(hand []domain.Card) ([]domain.CardCombination, []domain.Card) {
	var straights []domain.CardCombination
	remaining := make([]domain.Card, len(hand))
	copy(remaining, hand)

	for {
		// Group by rank
		rankMap := make(map[int32][]domain.Card)
		var ranks []int
		for _, c := range remaining {
			if c.Rank == 12 { // 2s cannot be in a straight
				continue
			}
			if _, ok := rankMap[c.Rank]; !ok {
				ranks = append(ranks, int(c.Rank))
			}
			rankMap[c.Rank] = append(rankMap[c.Rank], c)
		}
		domain.SortHand(remaining) // helper sort isn't enough, we need rank sort
		// but rankMap keys are int32, we need to sort ranks slice
		// Let's rely on simple sort
		// We'll reimplement sort manually for ranks slice
		for i := 0; i < len(ranks); i++ {
			for j := i + 1; j < len(ranks); j++ {
				if ranks[i] > ranks[j] {
					ranks[i], ranks[j] = ranks[j], ranks[i]
				}
			}
		}

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

		// Extract the straight
		straightCards := make([]domain.Card, 0, bestLen)
		for k := 0; k < bestLen; k++ {
			r := int32(ranks[bestStart+k])
			// Prefer taking the lowest suit card to save higher suits for singles/pairs?
			// Or highest suit to make the straight stronger?
			// Strategy: Save higher suits for singles (defensive) or pairs. Take lowest.
			card := rankMap[r][0]
			straightCards = append(straightCards, card)
		}
		
		straights = append(straights, domain.IdentifyCombination(straightCards))
		remaining = removeSubset(remaining, straightCards)
	}

	return straights, remaining
}

// ExtractBombs identifies and removes all Quads and Consecutive Pairs (3+) from the pool.
func ExtractBombs(hand []domain.Card) ([]domain.CardCombination, []domain.Card) {
	var bombs []domain.CardCombination
	remaining := make([]domain.Card, len(hand))
	copy(remaining, hand)

	// 1. Extract Quads FIRST
	for {
		found := false
		domain.SortHand(remaining)
		for i := 0; i <= len(remaining)-4; i++ {
			if remaining[i].Rank == remaining[i+3].Rank {
				quad := remaining[i : i+4]
				quadCopy := make([]domain.Card, 4)
				copy(quadCopy, quad)
				bombs = append(bombs, domain.IdentifyCombination(quadCopy))
				remaining = removeSubset(remaining, quadCopy)
				found = true
				break
			}
		}
		if !found {
			break
		}
	}

	// 2. Extract Consecutive Pairs (Pines) - 3 or more pairs
	for {
		pines, rest := extractLongestPine(remaining)
		if len(pines) == 0 {
			break
		}
		bombs = append(bombs, domain.IdentifyCombination(pines))
		remaining = rest
	}

	return bombs, remaining
}

// extractLongestPine helper for partitioning
func extractLongestPine(hand []domain.Card) ([]domain.Card, []domain.Card) {
	if len(hand) < 6 {
		return nil, hand
	}

	// Group ALL available pairs by rank (to avoid missing a pine due to a quad)
	rankPairs := make(map[int32][][]domain.Card)
	var ranks []int32
	
	// Count ranks
	counts := make(map[int32]int)
	rankToCards := make(map[int32][]domain.Card)
	for _, c := range hand {
		counts[c.Rank]++
		rankToCards[c.Rank] = append(rankToCards[c.Rank], c)
	}

	for r := int32(0); r < 12; r++ { // 3 to Ace
		if counts[r] >= 2 {
			ranks = append(ranks, r)
			// Use the TWO LOWEST cards of this rank for the pine to save higher suits for singles/pairs
			// (Though for a bomb, suits don't matter much in Tien Len, but it's a good heuristic)
			pair := []domain.Card{rankToCards[r][0], rankToCards[r][1]}
			rankPairs[r] = append(rankPairs[r], pair)
		}
	}

	if len(ranks) < 3 {
		return nil, hand
	}

	// Find longest consecutive sequence in ranks
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
		pine := make([]domain.Card, 0, bestLen*2)
		for k := 0; k < bestLen; k++ {
			pine = append(pine, rankPairs[ranks[bestStart+k]][0]...)
		}
		return pine, removeSubset(hand, pine)
	}

	return nil, hand
}