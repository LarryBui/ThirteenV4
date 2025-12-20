package internal

import (
	"sort"
	"tienlen/internal/domain"
)

// ValidMove represents a possible legal play.
type ValidMove struct {
	Cards []domain.Card
}

// GetValidMoves returns all legal moves for a player given their hand and the last played combination.
func GetValidMoves(hand []domain.Card, lastCombo domain.CardCombination) []ValidMove {
	domain.SortHand(hand)
	var moves []ValidMove

	// If it's a lead turn (new round), we can play any valid combination.
	if lastCombo.Type == domain.Invalid {
		moves = append(moves, findAllSingles(hand)...)
		moves = append(moves, findAllPairs(hand)...)
		moves = append(moves, findAllTriples(hand)...)
		moves = append(moves, findAllQuads(hand)...)
		moves = append(moves, findAllStraights(hand)...)
		moves = append(moves, findAllConsecutivePairs(hand)...)
		return moves
	}

	// If we are responding, we must match the type and count, OR "chop" (bomb/pine).
	
	// 1. Same-type matches
	switch lastCombo.Type {
	case domain.Single:
		moves = append(moves, findBeatingSingles(hand, lastCombo.Cards)...)
	case domain.Pair:
		moves = append(moves, findBeatingPairs(hand, lastCombo.Cards)...)
	case domain.Triple:
		moves = append(moves, findBeatingTriples(hand, lastCombo.Cards)...)
	case domain.Straight:
		moves = append(moves, findBeatingStraights(hand, lastCombo.Cards)...)
	case domain.Bomb: // Quads or Consecutive Pairs
		moves = append(moves, findBeatingBombs(hand, lastCombo.Cards)...)
	}

	// 2. Chopping logic (Bombs/Pines vs 2s or smaller bombs)
	moves = append(moves, findChoppingMoves(hand, lastCombo.Cards)...)

	return moves
}

func findAllSingles(hand []domain.Card) []ValidMove {
	var moves []ValidMove
	for _, c := range hand {
		moves = append(moves, ValidMove{Cards: []domain.Card{c}})
	}
	return moves
}

func findBeatingSingles(hand []domain.Card, prev []domain.Card) []ValidMove {
	var moves []ValidMove
	for _, c := range hand {
		newMove := []domain.Card{c}
		if domain.CanBeat(prev, newMove) {
			moves = append(moves, ValidMove{Cards: newMove})
		}
	}
	return moves
}

func findAllPairs(hand []domain.Card) []ValidMove {
	var moves []ValidMove
	for i := 0; i < len(hand)-1; i++ {
		for j := i + 1; j < len(hand); j++ {
			pair := []domain.Card{hand[i], hand[j]}
			if domain.IsValidSet(pair) && domain.IdentifyCombination(pair).Type == domain.Pair {
				moves = append(moves, ValidMove{Cards: pair})
			}
		}
	}
	return moves
}

func findBeatingPairs(hand []domain.Card, prev []domain.Card) []ValidMove {
	var moves []ValidMove
	for i := 0; i < len(hand)-1; i++ {
		for j := i + 1; j < len(hand); j++ {
			pair := []domain.Card{hand[i], hand[j]}
			if domain.IsValidSet(pair) && domain.IdentifyCombination(pair).Type == domain.Pair && domain.CanBeat(prev, pair) {
				moves = append(moves, ValidMove{Cards: pair})
			}
		}
	}
	return moves
}

func findAllTriples(hand []domain.Card) []ValidMove {
	var moves []ValidMove
	for i := 0; i < len(hand)-2; i++ {
		for j := i + 1; j < len(hand)-1; j++ {
			for k := j + 1; k < len(hand); k++ {
				triple := []domain.Card{hand[i], hand[j], hand[k]}
				if domain.IsValidSet(triple) && domain.IdentifyCombination(triple).Type == domain.Triple {
					moves = append(moves, ValidMove{Cards: triple})
				}
			}
		}
	}
	return moves
}

func findBeatingTriples(hand []domain.Card, prev []domain.Card) []ValidMove {
	var moves []ValidMove
	for i := 0; i < len(hand)-2; i++ {
		for j := i + 1; j < len(hand)-1; j++ {
			for k := j + 1; k < len(hand); k++ {
				triple := []domain.Card{hand[i], hand[j], hand[k]}
				if domain.IsValidSet(triple) && domain.IdentifyCombination(triple).Type == domain.Triple && domain.CanBeat(prev, triple) {
					moves = append(moves, ValidMove{Cards: triple})
				}
			}
		}
	}
	return moves
}

func findAllQuads(hand []domain.Card) []ValidMove {
	var moves []ValidMove
	for i := 0; i < len(hand)-3; i++ {
		if hand[i].Rank == hand[i+3].Rank {
			quad := []domain.Card{hand[i], hand[i+1], hand[i+2], hand[i+3]}
			moves = append(moves, ValidMove{Cards: quad})
		}
	}
	return moves
}

func findAllStraights(hand []domain.Card) []ValidMove {
	var moves []ValidMove
	// Filter out 2s as they can't be in straights
	var noTwos []domain.Card
	for _, c := range hand {
		if c.Rank != 12 {
			noTwos = append(noTwos, c)
		}
	}

	// Use a map to group cards by rank for easier sequence building
	rankMap := make(map[int32][]domain.Card)
	var ranks []int
	for _, c := range noTwos {
		if _, ok := rankMap[c.Rank]; !ok {
			ranks = append(ranks, int(c.Rank))
		}
		rankMap[c.Rank] = append(rankMap[c.Rank], c)
	}
	sort.Ints(ranks)

	// Find all consecutive rank sequences of length >= 3
	for i := 0; i < len(ranks)-2; i++ {
		for length := 3; i+length <= len(ranks); length++ {
			// Check if this sub-slice is consecutive
			isConsecutive := true
			for k := 1; k < length; k++ {
				if ranks[i+k] != ranks[i+k-1]+1 {
					isConsecutive = false
					break
				}
			}
			if !isConsecutive {
				break
			}

			// Generate all possible combinations of cards for this rank sequence
			// (If a player has multiple suits for a rank, there are multiple straights)
			// For simplicity and performance, the bot will consider only one straight 
			// for a given rank sequence for now (using the highest suits to keep it strong, 
			// or lowest to keep it efficient). 
			// Let's generate ONE straight using the lowest cards available to save high suits.
			currentStraight := make([]domain.Card, length)
			for k := 0; k < length; k++ {
				currentStraight[k] = rankMap[int32(ranks[i+k])][0]
			}
			moves = append(moves, ValidMove{Cards: currentStraight})
		}
	}
	return moves
}

func findBeatingStraights(hand []domain.Card, prev []domain.Card) []ValidMove {
	all := findAllStraights(hand)
	var moves []ValidMove
	for _, m := range all {
		if len(m.Cards) == len(prev) && domain.CanBeat(prev, m.Cards) {
			moves = append(moves, ValidMove{Cards: m.Cards})
		}
	}
	return moves
}

func findAllConsecutivePairs(hand []domain.Card) []ValidMove {
	var moves []ValidMove
	// Needs at least 3 pairs (6 cards)
	if len(hand) < 6 {
		return moves
	}

	// Group pairs by rank
	rankPairs := make(map[int32][][]domain.Card)
	var ranks []int
	for i := 0; i < len(hand)-1; i++ {
		if hand[i].Rank == hand[i+1].Rank && hand[i].Rank != 12 {
			rank := hand[i].Rank
			if _, ok := rankPairs[rank]; !ok {
				ranks = append(ranks, int(rank))
			}
			rankPairs[rank] = append(rankPairs[rank], []domain.Card{hand[i], hand[i+1]})
			// Skip to next potential pair
			i++
		}
	}
	sort.Ints(ranks)

	// Find consecutive ranks in pairGroups
	for i := 0; i < len(ranks)-2; i++ {
		for length := 3; i+length <= len(ranks); length++ {
			isConsecutive := true
			for k := 1; k < length; k++ {
				if ranks[i+k] != ranks[i+k-1]+1 {
					isConsecutive = false
					break
				}
			}
			if !isConsecutive {
				break
			}

			// Construct the 3, 4, or 5-pine
			pine := make([]domain.Card, 0, length*2)
			for k := 0; k < length; k++ {
				pine = append(pine, rankPairs[int32(ranks[i+k])][0]...)
			}
			moves = append(moves, ValidMove{Cards: pine})
		}
	}

	return moves
}

func findBeatingBombs(hand []domain.Card, prev []domain.Card) []ValidMove {
	// Find quads and pines that can beat the existing bomb
	var moves []ValidMove
	
	// Try quads
	quads := findAllQuads(hand)
	for _, q := range quads {
		if domain.CanBeat(prev, q.Cards) {
			moves = append(moves, ValidMove{Cards: q.Cards})
		}
	}

	// Try pines
	pines := findAllConsecutivePairs(hand)
	for _, p := range pines {
		if domain.CanBeat(prev, p.Cards) {
			moves = append(moves, ValidMove{Cards: p.Cards})
		}
	}

	return moves
}

func findChoppingMoves(hand []domain.Card, prev []domain.Card) []ValidMove {
	// Special logic: CanBeat already handles chopping but we need to generate 
	// the specific combinations that ARE capable of chopping (Quads, 3-Pine, etc)
	// even if the lengths don't match.
	
	var moves []ValidMove
	
	// Only bother if previous play was a 2 or a bomb
	isPrev2 := (len(prev) == 1 && prev[0].Rank == 12) || (len(prev) == 2 && domain.IdentifyCombination(prev).Type == domain.Pair && prev[0].Rank == 12)
	isPrevBomb := domain.IdentifyCombination(prev).Type == domain.Bomb
	
	if !isPrev2 && !isPrevBomb {
		return moves
	}

	// Generate all potential choppers
	choppers := append(findAllQuads(hand), findAllConsecutivePairs(hand)...)
	for _, c := range choppers {
		if domain.CanBeat(prev, c.Cards) {
			moves = append(moves, ValidMove{Cards: c.Cards})
		}
	}

	return moves
}
