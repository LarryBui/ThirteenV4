package internal

import "tienlen/internal/domain"

const (
	ScorePig        = 20.0
	ScoreBomb       = 30.0
	ScoreStraight   = 5.0 // Per card
	ScoreTriple     = 10.0
	ScorePair       = 5.0
	ScoreHighSingle = 2.0  // A, K, Q, J
	ScoreLowSingle  = -2.0 // 3..10
)

// EvaluateHand returns a heuristic score for the given hand.
// Higher is better.
func EvaluateHand(hand []domain.Card) float64 {
	// Work on a copy to destructively analyze
	cards := make([]domain.Card, len(hand))
	copy(cards, hand)
	domain.SortHand(cards) // Sort by power

	score := 0.0

	// 1. Count and remove 2s (Pigs)
	// They are always good, effectively standalone power.
	// (Unless they are part of a 4-of-a-kind bomb, but 4-2s is huge anyway).
	// Let's keep 2s for bombs check? 
	// Actually, 4-2s is a Quad (Bomb), dealing with it in Quads is better.
	// But 2s are not part of Straights or Pines. 
	// So we can extract Quads/Triples/Pairs of 2s later.
	// But usually, a single 2 is a "Pig" and valuable.
	// Let's treat them as regular cards for Quad/Triple/Pair logic, 
	// but give them huge bonus if they remain as singles?
	// No, 2s cannot be in Straights/Pines.
	
	// Better: Separate 2s first, but check for Quad 2s.
	// Actually, standard IdentifyCombination handles 2s in Quads/Pairs/Triples.
	
	// Strategy: Greedy Extraction
	
	// A. Extract Pines (Consecutive Pairs) - strongest bomb
	// (Skipping for now to keep it fast/simple, straightforward greedy usually: Quads -> Straights -> Triples -> Pairs)
	
	// B. Extract Quads
	cards, count := extractQuads(cards)
	score += float64(count) * ScoreBomb

	// C. Extract Straights
	var straightStats straightStats
	cards, straightStats = extractStraights(cards)
	score += float64(straightStats.Cards) * ScoreStraight // Cards count contributes to score
	
	// D. Extract Triples
	cards, count = extractTriples(cards)
	score += float64(count) * ScoreTriple

	// E. Extract Pairs
	cards, count = extractPairs(cards)
	score += float64(count) * ScorePair

	// F. Remaining Singles
	for _, c := range cards {
		if c.Rank == 12 { // 2
			score += ScorePig
		} else if c.Rank >= 9 { // J(9), Q(10), K(11), A(0? No, A is 12 in Power but Rank is 0... wait)
			// Need to check Rank mapping.
			// domain.deck: 0=3, ..., 12=2? 
			// Check standard Tien Len rule usually: 3=0, ..., 2=12.
			// Let's verify via cardPower.
			// If 3 is lowest, it should be rank 0.
			// If A is high, it is Rank 11.
			// If 2 is highest, it is Rank 12.
			
			if c.Rank == 11 || c.Rank == 10 || c.Rank == 9 { // A, K, Q, J(8? 3=0, 4=1, 5=2, 6=3, 7=4, 8=5, 9=6, 10=7, J=8, Q=9, K=10, A=11, 2=12)
				score += ScoreHighSingle
			} else {
				score += ScoreLowSingle
			}
		} else {
			// Ranks 0..8 (3..J)
			if c.Rank >= 9 { // Q, K, A
                 score += ScoreHighSingle
            } else {
                 score += ScoreLowSingle
            }
		}
	}
	
	// Note: Rank assumption:
	// 3=0, 4=1, 5=2, 6=3, 7=4, 8=5, 9=6, 10=7, J=8, Q=9, K=10, A=11, 2=12.
	// So High Singles (J+) are >= 8.
	
	return score
}

func extractQuads(cards []domain.Card) ([]domain.Card, int) {
	quadsFound := 0
	// Since sorted, quads are adjacent
	for i := 0; i <= len(cards)-4; {
		if cards[i].Rank == cards[i+3].Rank {
			// Found quad
			quad := cards[i : i+4]
			cards = removeSubset(cards, quad)
			quadsFound++
			// Reset index to 0 or re-evaluate? Removing changes indices.
			// Safest: restart or handle careful index.
			// Rebuilding slice means 'cards' is new. 
			// We can just continue from 'i' which now points to next card.
			// But 'removeSubset' creates new slice.
			// Let's use specific removal logic for sorted array.
			i = 0 
		} else {
			i++
		}
	}
	return cards, quadsFound
}

func extractTriples(cards []domain.Card) ([]domain.Card, int) {
	triplesFound := 0
	for i := 0; i <= len(cards)-3; {
		if cards[i].Rank == cards[i+2].Rank {
			subset := cards[i : i+3]
			cards = removeSubset(cards, subset)
			triplesFound++
			i = 0
		} else {
			i++
		}
	}
	return cards, triplesFound
}

func extractPairs(cards []domain.Card) ([]domain.Card, int) {
	pairsFound := 0
	for i := 0; i <= len(cards)-2; {
		if cards[i].Rank == cards[i+1].Rank {
			subset := cards[i : i+2]
			cards = removeSubset(cards, subset)
			pairsFound++
			i = 0
		} else {
			i++
		}
	}
	return cards, pairsFound
}
