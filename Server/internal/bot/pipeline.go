package bot

import (
	"tienlen/internal/bot/internal"
)

// SelectionContext holds the state for the hand organization decision pipeline.
type SelectionContext struct {
	Candidates  []internal.OrganizedHand
	CurrentBest internal.OrganizedHand
	SelectedIndex int
}

// SelectionRule represents a logic unit that can influence which hand organization strategy is chosen.
type SelectionRule interface {
	Name() string
	Apply(ctx *SelectionContext)
}

// FavorStraightsRule prefers hand organizations that maximize Straights.
type FavorStraightsRule struct{}

func (r *FavorStraightsRule) Name() string { return "FavorStraights" }

func (r *FavorStraightsRule) Apply(ctx *SelectionContext) {
	bestIdx := ctx.SelectedIndex
	maxStraights := countStraights(ctx.CurrentBest)

	for i, candidate := range ctx.Candidates {
		count := countStraights(candidate)
		if count > maxStraights {
			maxStraights = count
			bestIdx = i
		}
	}

	if bestIdx != ctx.SelectedIndex {
		ctx.SelectedIndex = bestIdx
		ctx.CurrentBest = ctx.Candidates[bestIdx]
	}
}

// FavorPairsRule prefers hand organizations that maximize Pairs.
type FavorPairsRule struct{}

func (r *FavorPairsRule) Name() string { return "FavorPairs" }

func (r *FavorPairsRule) Apply(ctx *SelectionContext) {
	bestIdx := ctx.SelectedIndex
	maxPairs := countPairs(ctx.CurrentBest)

	for i, candidate := range ctx.Candidates {
		count := countPairs(candidate)
		if count > maxPairs {
			maxPairs = count
			bestIdx = i
		}
	}

	if bestIdx != ctx.SelectedIndex {
		ctx.SelectedIndex = bestIdx
		ctx.CurrentBest = ctx.Candidates[bestIdx]
	}
}

func countStraights(hand internal.OrganizedHand) int {
	return len(hand.Straights)
}

func countPairs(hand internal.OrganizedHand) int {
	return len(hand.Pairs)
}
