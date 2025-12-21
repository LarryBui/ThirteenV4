package bot

import (
	"sort"
	"tienlen/internal/bot/internal"
	"tienlen/internal/domain"
)

type GoodBot struct{}

func (b *GoodBot) CalculateMove(game *domain.Game, seat int) (Move, error) {
	// 1. Identify Context
	var player *domain.Player
	for _, p := range game.Players {
		if p.Seat == seat {
			player = p
			break
		}
	}
	if player == nil || len(player.Hand) == 0 {
		return Move{Pass: true}, nil
	}

	// 2. Determine constraint (Lead or Respond)
	lastCombo := game.LastPlayedCombination
	// If it's a new round (previous round finished or everyone passed), we lead.
	// Note: game.LastPlayedCombination might be cleared by the app service on new round,
	// or we check game.LastPlayerToPlaySeat. 
	// The domain logic usually clears LastPlayedCombination on "New Round".
	// We rely on LastPlayedCombination.Type == Invalid for Lead.

	// 3. Generate moves
	validMoves := internal.GetValidMoves(player.Hand, lastCombo)

	if len(validMoves) == 0 {
		return Move{Pass: true}, nil
	}

	// 4. Strategy: "Play Lowest"
	// Sort moves by the power of their highest card (ascending).
	// For tie-breaking (e.g. 3-4-5 vs 3-4-5 of diff suits), usually lowest suit wins, 
	// but standard cardPower handles it.
	sort.Slice(validMoves, func(i, j int) bool {
		comboI := domain.IdentifyCombination(validMoves[i].Cards)
		comboJ := domain.IdentifyCombination(validMoves[j].Cards)
		
		// If types are different (only possible on Lead), prefer playing "more cards" to dump hand?
		// Or play "lowest value".
		// Good Bot Logic:
		// If Lead: Play lowest Single, then lowest Pair, etc.
		// Actually, standard "safe" play is to get rid of lowest card period.
		// So we compare the lowest card in the combo? Or the highest?
		// Usually we compare the "Value" of the combo.
		
		// If types are different, we need a preference. 
		// "Good" bot prefers dumping trash (Singles) first? Or just lowest value combo?
		// Let's stick to "Lowest Value" (CardPower of the highest card in the combo).
		return comboI.Value < comboJ.Value
	})

	return Move{Cards: validMoves[0].Cards}, nil
}
