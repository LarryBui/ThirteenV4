package internal

import "tienlen/internal/domain"

// PhaseWeights tune move scoring for a specific phase.
type PhaseWeights struct {
	HandScoreWeight       float64
	StraightCardWeight    float64
	PineCardWeight        float64
	PairWeight            float64
	TripleWeight          float64
	QuadWeight            float64
	SingleWeight          float64
	TotalCardWeight       float64
	UseTwoPenalty         float64
	UseBombPenalty        float64
	UseHighCardPenalty    float64
	FinishBonus           float64
	BlockerHighCardBonus  float64
}

// BotTuning defines phase weights and thresholds for a bot difficulty.
type BotTuning struct {
	Opening         PhaseWeights
	Mid             PhaseWeights
	End             PhaseWeights
	PassThreshold   float64
	ThreatThreshold int
}

// ForPhase returns the weights that match the supplied phase.
func (t BotTuning) ForPhase(phase GamePhase) PhaseWeights {
	switch phase {
	case PhaseOpening:
		return t.Opening
	case PhaseEnd:
		return t.End
	default:
		return t.Mid
	}
}

// ScoredMove holds a move with its computed score and supporting metadata.
type ScoredMove struct {
	Move             ValidMove
	Score            float64
	Combo            domain.CardCombination
	Remaining        []domain.Card
	RemainingProfile HandProfile
}

// ScoreHand evaluates a hand using the configured weights and structure profile.
func ScoreHand(hand []domain.Card, weights PhaseWeights) float64 {
	profile := ProfileHand(hand)
	return scoreHandWithProfile(hand, profile, weights)
}

// BuildScoredMoves scores each move using phase weights and optional blocking bias.
func BuildScoredMoves(hand []domain.Card, moves []ValidMove, weights PhaseWeights, threat bool) []ScoredMove {
	scored := make([]ScoredMove, 0, len(moves))
	for _, move := range moves {
		remaining := domain.RemoveCards(hand, move.Cards)
		profile := ProfileHand(remaining)
		score := scoreHandWithProfile(remaining, profile, weights)

		if len(remaining) == 0 {
			score += weights.FinishBonus
		}

		combo := domain.IdentifyCombination(move.Cards)
		score -= weights.UseHighCardPenalty * float64(combo.Value)

		if combo.Type == domain.Bomb {
			score -= weights.UseBombPenalty
		}

		twosUsed := countRank(move.Cards, 12)
		score -= weights.UseTwoPenalty * float64(twosUsed)

		if threat && combo.Type == domain.Single {
			score += weights.BlockerHighCardBonus * float64(combo.Value)
		}

		scored = append(scored, ScoredMove{
			Move:             move,
			Score:            score,
			Combo:            combo,
			Remaining:        remaining,
			RemainingProfile: profile,
		})
	}
	return scored
}

// DetectThreat reports whether any opponent is at or below the supplied card threshold.
func DetectThreat(game *domain.Game, seat int, threshold int) bool {
	if threshold <= 0 || game == nil {
		return false
	}
	for _, player := range game.Players {
		if player == nil || player.Seat == seat || player.Finished || len(player.Hand) == 0 {
			continue
		}
		if len(player.Hand) <= threshold {
			return true
		}
	}
	return false
}

func scoreHandWithProfile(hand []domain.Card, profile HandProfile, weights PhaseWeights) float64 {
	score := 0.0
	score += weights.HandScoreWeight * EvaluateHand(hand)
	score += weights.StraightCardWeight * float64(profile.StraightCards)
	score += weights.PineCardWeight * float64(profile.PineCards)
	score += weights.PairWeight * float64(profile.Pairs)
	score += weights.TripleWeight * float64(profile.Triples)
	score += weights.QuadWeight * float64(profile.Quads)
	score += weights.SingleWeight * float64(profile.Singles)
	score += weights.TotalCardWeight * float64(profile.TotalCards)
	return score
}

func countRank(cards []domain.Card, rank int32) int {
	count := 0
	for _, c := range cards {
		if c.Rank == rank {
			count++
		}
	}
	return count
}
