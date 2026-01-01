package bot

import botinternal "tienlen/internal/bot/internal"

const finishBonus = 1000.0

// DefaultTuning balances structure preservation and hand reduction by phase.
var DefaultTuning = botinternal.BotTuning{
	Opening: botinternal.PhaseWeights{
		HandScoreWeight:    1.0,
		StraightCardWeight: 0.6,
		PineCardWeight:     0.8,
		PairWeight:         0.5,
		TripleWeight:       0.7,
		QuadWeight:         1.0,
		SingleWeight:       -1.0,
		TotalCardWeight:    -0.1,
		UseTwoPenalty:      6.0,
		UseBombPenalty:     4.0,
		UseHighCardPenalty: 0.5,
		FinishBonus:        finishBonus,
	},
	Mid: botinternal.PhaseWeights{
		HandScoreWeight:    1.0,
		StraightCardWeight: 0.5,
		PineCardWeight:     0.7,
		PairWeight:         0.6,
		TripleWeight:       0.8,
		QuadWeight:         1.0,
		SingleWeight:       -1.2,
		TotalCardWeight:    -0.3,
		UseTwoPenalty:      4.0,
		UseBombPenalty:     3.0,
		UseHighCardPenalty: 0.4,
		FinishBonus:        finishBonus,
	},
	End: botinternal.PhaseWeights{
		HandScoreWeight:      1.2,
		StraightCardWeight:   0.3,
		PineCardWeight:       0.4,
		PairWeight:           0.4,
		TripleWeight:         0.5,
		QuadWeight:           0.6,
		SingleWeight:         -1.5,
		TotalCardWeight:      -1.5,
		UseTwoPenalty:        0.7,
		UseBombPenalty:       1.0,
		UseHighCardPenalty:   0.2,
		FinishBonus:          finishBonus,
		BlockerHighCardBonus: 0.8,
	},
	PassThreshold:   -10.0,
	ThreatThreshold: 3,
}