package domain

import (
	"sort"
)

// CardCombinationType represents the type of card combination.
type CardCombinationType int

const (
	Invalid CardCombinationType = iota
	Single
	Pair
	Triple
	Quad
	Straight // Sequence of 3 or more cards
	Bomb     // 3 pairs / 4 pairs in a sequence. Not a standard rule, but a variant. For now consider it a sequence of a single rank (e.g. four of a kind).
)

// CardCombination represents a detected combination of cards.
type CardCombination struct {
	Type  CardCombinationType
	Cards []Card // The cards forming the combination, sorted
	Value int    // The "rank" or "strength" of the combination (e.g., highest card in a straight, rank of a pair)
	Count int    // Number of cards in the combination
}

// CompareRanks compares two ranks, accounting for Tien Len's special rank order (3 smallest, 2 largest).
func CompareRanks(rank1, rank2 int) int {
	// 3 is 0, 4 is 1, ..., Ace is 11, 2 is 12
	// If the difference is 0, ranks are equal.
	if rank1 == rank2 {
		return 0
	}
	// 2 is the highest rank
	if rank1 == 12 { // rank1 is 2
		return 1
	}
	if rank2 == 12 { // rank2 is 2
		return -1
	}
	if rank1 > rank2 {
		return 1
	}
	return -1
}

// SortCards sorts a slice of cards primarily by rank (Tien Len order) and then by suit.
func SortCards(cards []Card) {
	sort.Slice(cards, func(i, j int) bool {
		cmpRank := CompareRanks(cards[i].Rank, cards[j].Rank)
		if cmpRank != 0 {
			return cmpRank < 0 // ascending rank
		}
		// If ranks are equal, sort by suit (S < C < D < H, or any consistent order)
		return cards[i].Suit < cards[j].Suit
	})
}

// IdentifyCombination analyzes a set of cards and returns the strongest valid Tien Len combination.
func IdentifyCombination(cards []Card) CardCombination {
	n := len(cards)
	if n == 0 {
		return CardCombination{Type: Invalid}
	}

	SortCards(cards)

	// Check for single card
	if n == 1 {
		return CardCombination{Type: Single, Cards: cards, Value: cards[0].Rank, Count: 1}
	}

	// Group cards by rank
	rankCounts := make(map[int]int)
	for _, card := range cards {
		rankCounts[card.Rank]++
	}

	// Check for Pair, Triple, Quad
	if n >= 2 && n <= 4 {
		isGroup := true
		rank := cards[0].Rank
		for _, card := range cards {
			if card.Rank != rank {
				isGroup = false
				break
			}
		}
		if isGroup {
			switch n {
			case 2:
				return CardCombination{Type: Pair, Cards: cards, Value: rank, Count: 2}
			case 3:
				return CardCombination{Type: Triple, Cards: cards, Value: rank, Count: 3}
			case 4:
				// Ensure all 4 cards are different suits (possible for a Bomb "Four of a kind")
				if len(rankCounts) == 1 && rankCounts[rank] == 4 {
					return CardCombination{Type: Bomb, Cards: cards, Value: rank, Count: 4}
				}
			}
		}
	}

	// Check for Straight (3 or more cards in sequence)
	if n >= 3 {
		isStraight := true
		currentRank := cards[0].Rank
		// All cards must have unique ranks and be sequential
		for i := 1; i < n; i++ {
			expectedRank := (currentRank + 1) % 13 // Handles wrap-around from King (10) to Ace (11), then 2 (12)
			if cards[i].Rank != expectedRank {
				isStraight = false
				break
			}
			// Special handling for 2s in straights: a straight cannot end with a 2 (Tien Len rule)
			if cards[i].Rank == 12 { // If the current card is a 2
				isStraight = false
				break
			}
			currentRank = cards[i].Rank
		}

		if isStraight {
			return CardCombination{Type: Straight, Cards: cards, Value: cards[n-1].Rank, Count: n} // Value is the highest card's rank
		}
	}

	// More complex combinations (e.g., three pairs, four pairs, specific bomb types)
	// These rules are highly variant. For now, prioritize simpler combinations.

	return CardCombination{Type: Invalid}
}

// CanBeat checks if 'newCombo' can legally beat 'lastCombo' according to Tien Len rules.
func CanBeat(newCombo, lastCombo CardCombination) bool {
	if newCombo.Type == Invalid || lastCombo.Type == Invalid {
		return false
	}

	// Special rule: If previous play was a Bomb, only a higher Bomb can beat it.
	// For this simplified version, let's assume a Bomb is a Four of a Kind.
	if lastCombo.Type == Bomb {
		if newCombo.Type == Bomb {
			return CompareRanks(newCombo.Value, lastCombo.Value) > 0 // Higher rank bomb
		}
		return false // Only another bomb can beat a bomb
	}

	// Straight beats anything of lower combination type or a lower straight of same length
	if newCombo.Type == Straight && lastCombo.Type != Bomb {
		if newCombo.Count == lastCombo.Count { // Must be same length
			return CompareRanks(newCombo.Value, lastCombo.Value) > 0
		}
		// A longer straight can beat a shorter one only if specifically allowed by house rules,
		// generally straights must be of the same length to beat each other.
		return false // Default: must be same length
	}

	// Pair beats a lower pair
	if newCombo.Type == Pair && lastCombo.Type == Pair {
		return CompareRanks(newCombo.Value, lastCombo.Value) > 0
	}

	// Triple beats a lower triple
	if newCombo.Type == Triple && lastCombo.Type == Triple {
		return CompareRanks(newCombo.Value, lastCombo.Value) > 0
	}

	// Single beats a lower single
	if newCombo.Type == Single && lastCombo.Type == Single {
		return CompareRanks(newCombo.Value, lastCombo.Value) > 0
	}

	// Default: combinations must be of the same type and size to beat each other (excluding bombs)
	return false
}
