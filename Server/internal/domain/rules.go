package domain

import "sort"

// CardCombinationType represents the type of card combination.
type CardCombinationType int

const (
	Invalid CardCombinationType = iota
	Single
	Pair
	Triple
	Quad
	Straight // Sequence of 3 or more cards
	Bomb     // Specific variants like consecutive pairs
)

// CardCombination represents a detected combination of cards.
type CardCombination struct {
	Type  CardCombinationType
	Cards []Card // The cards forming the combination, sorted
	Value int32  // The "power" of the highest card in the combination
	Count int    // Number of cards in the combination
}

// IsValidSet checks if the cards form a legal Tien Len combination.
func IsValidSet(cards []Card) bool {
	if len(cards) == 0 {
		return false
	}
	if len(cards) == 1 {
		return true
	}

	// Same-rank sets: pair, triple, quad (bomb)
	if allSameRank(cards) {
		return len(cards) <= 4
	}

	// Straights (sanh): length >= 3, ranks consecutive, cannot contain 2 (rank 12), no duplicates.
	if isStraight(cards) {
		return true
	}

	// Consecutive pairs (doi thong): even length >= 6, pairs of same rank, ranks consecutive, cannot contain 2.
	if isConsecutivePairs(cards) {
		return true
	}

	return false
}

// CanBeat determines if newCards can beat prevCards according to Tien Len rules.
// Includes full "Pig Chopping" logic for Quads and Consecutive Pairs (Pine/Thong).
func CanBeat(prevCards, newCards []Card) bool {
	// Identify types for chopping logic
	isNewQuad := isQuad(newCards)
	isNew3Pine := isThreeConsecutivePairs(newCards)
	isNew4Pine := isFourConsecutivePairs(newCards)
	isNew5Pine := isFiveConsecutivePairs(newCards)

	// Identify prev types
	isPrevSingle2 := len(prevCards) == 1 && prevCards[0].Rank == 12
	isPrevPair2 := len(prevCards) == 2 && allSameRank(prevCards) && prevCards[0].Rank == 12
	isPrevQuad := isQuad(prevCards)
	isPrev3Pine := isThreeConsecutivePairs(prevCards)
	isPrev4Pine := isFourConsecutivePairs(prevCards)
	isPrev5Pine := isFiveConsecutivePairs(prevCards)

	// --- 5 Pairs of Consecutive Sequence (5-Pine) ---
	// Beats: Single 2, Pair 2, Quad, 4-Pine, Smaller 5-Pine
	if isNew5Pine {
		if isPrevSingle2 || isPrevPair2 || isPrevQuad || isPrev4Pine || isPrev3Pine {
			return true
		}
		if isPrev5Pine {
			return getMaxPower(newCards) > getMaxPower(prevCards)
		}
	}

	// --- 4 Pairs of Consecutive Sequence (4-Pine) ---
	// Beats: Single 2, Pair 2, Quad, Smaller 4-Pine
	if isNew4Pine {
		if isPrevSingle2 || isPrevPair2 || isPrevQuad || isPrev3Pine {
			return true
		}
		if isPrev4Pine {
			return getMaxPower(newCards) > getMaxPower(prevCards)
		}
	}

	// --- Quad (Four of a Kind) ---
	// Beats: Single 2, Pair 2, Smaller Quad, 3-Pine
	if isNewQuad {
		if isPrevSingle2 || isPrevPair2 || isPrev3Pine {
			return true
		}
		if isPrevQuad {
			return newCards[0].Rank > prevCards[0].Rank
		}
	}

	// --- 3 Pairs of Consecutive Sequence (3-Pine) ---
	// Beats: Single 2, Smaller 3-Pine
	if isNew3Pine {
		if isPrevSingle2 {
			return true
		}
		if isPrev3Pine {
			return getMaxPower(newCards) > getMaxPower(prevCards)
		}
	}

	// --- Standard Rules ---
	// 1. Must be same length
	if len(prevCards) != len(newCards) {
		return false
	}

	// 2. Must be same type (e.g. Pair vs Pair, Triple vs Triple)
	// (Validation of 'isType' is assumed handled by IsValidSet before calling CanBeat,
	// but we implicitly rely on structure similarity here).

	// 3. Compare highest card power
	return getMaxPower(newCards) > getMaxPower(prevCards)
}

// IdentifyCombination analyzes a set of cards and returns the strongest valid Tien Len combination.
func IdentifyCombination(cards []Card) CardCombination {
	if !IsValidSet(cards) {
		return CardCombination{Type: Invalid}
	}

	SortHand(cards)
	n := len(cards)

	if n == 1 {
		return CardCombination{Type: Single, Cards: cards, Value: CardPower(cards[0]), Count: 1}
	}

	if allSameRank(cards) {
		val := CardPower(cards[n-1])
		switch n {
		case 2:
			return CardCombination{Type: Pair, Cards: cards, Value: val, Count: 2}
		case 3:
			return CardCombination{Type: Triple, Cards: cards, Value: val, Count: 3}
		case 4:
			return CardCombination{Type: Bomb, Cards: cards, Value: val, Count: 4}
		}
	}

	if isStraight(cards) {
		return CardCombination{Type: Straight, Cards: cards, Value: CardPower(cards[n-1]), Count: n}
	}

	if isConsecutivePairs(cards) {
		return CardCombination{Type: Bomb, Cards: cards, Value: CardPower(cards[n-1]), Count: n}
	}

	return CardCombination{Type: Invalid}
}

func isQuad(cards []Card) bool {
	return len(cards) == 4 && allSameRank(cards)
}

func isThreeConsecutivePairs(cards []Card) bool {
	return len(cards) == 6 && isConsecutivePairs(cards)
}

func isFourConsecutivePairs(cards []Card) bool {
	return len(cards) == 8 && isConsecutivePairs(cards)
}

func isFiveConsecutivePairs(cards []Card) bool {
	return len(cards) == 10 && isConsecutivePairs(cards)
}

func getMaxPower(cards []Card) int32 {
	maxP := int32(-1)
	for _, c := range cards {
		p := CardPower(c)
		if p > maxP {
			maxP = p
		}
	}
	return maxP
}

func allSameRank(cards []Card) bool {
	if len(cards) == 0 {
		return false
	}
	r := cards[0].Rank
	for _, c := range cards {
		if c.Rank != r {
			return false
		}
	}
	return true
}

func isStraight(cards []Card) bool {
	if len(cards) < 3 {
		return false
	}
	ranks := make([]int32, len(cards))
	for i, c := range cards {
		if c.Rank == 12 { // 2 cannot be in a straight
			return false
		}
		ranks[i] = c.Rank
	}
	sort.Slice(ranks, func(i, j int) bool { return ranks[i] < ranks[j] })

	for i := 1; i < len(ranks); i++ {
		if ranks[i] == ranks[i-1] {
			return false // duplicate rank not allowed
		}
		if ranks[i] != ranks[i-1]+1 {
			return false // not consecutive
		}
	}
	return true
}

func isConsecutivePairs(cards []Card) bool {
	if len(cards) < 6 || len(cards)%2 != 0 {
		return false
	}
	ranks := make([]int32, len(cards))
	for i, c := range cards {
		if c.Rank == 12 { // 2 cannot be part of consecutive pairs
			return false
		}
		ranks[i] = c.Rank
	}
	sort.Slice(ranks, func(i, j int) bool { return ranks[i] < ranks[j] })

	// Check that cards are grouped in pairs of the same rank.
	pairRanks := make([]int32, 0, len(ranks)/2)
	for i := 0; i < len(ranks); i += 2 {
		if ranks[i] != ranks[i+1] {
			return false
		}
		pairRanks = append(pairRanks, ranks[i])
	}

	// Check consecutive ranks across pairs.
	for i := 1; i < len(pairRanks); i++ {
		if pairRanks[i] != pairRanks[i-1]+1 {
			return false
		}
	}
	return true
}
