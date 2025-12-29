package internal

import "tienlen/internal/domain"

// GamePhase describes the current strategic stage of a match.
type GamePhase int

const (
	// PhaseOpening indicates all active players still hold 13 cards.
	PhaseOpening GamePhase = iota
	// PhaseMid indicates no one has reached the endgame threshold yet.
	PhaseMid
	// PhaseEnd indicates at least one player finished or any active player has <= 5 cards.
	PhaseEnd
)

// DetectPhase infers the phase based on active players' hand sizes and finish state.
func DetectPhase(game *domain.Game) GamePhase {
	if game == nil || len(game.Players) == 0 {
		return PhaseMid
	}

	activePlayers := 0
	opening := true
	end := false

	for _, player := range game.Players {
		if player == nil {
			continue
		}
		if player.Finished || len(player.Hand) == 0 {
			end = true
			continue
		}
		activePlayers++
		if len(player.Hand) != 13 {
			opening = false
		}
		if len(player.Hand) <= 5 {
			end = true
		}
	}

	if activePlayers == 0 {
		return PhaseEnd
	}
	if opening {
		return PhaseOpening
	}
	if end {
		return PhaseEnd
	}
	return PhaseMid
}
