package brain

import (
	"tienlen/internal/domain"
)

// OpponentProfile tracks the behavioral history of a specific player.
type OpponentProfile struct {
	Seat           int
	CardsRemaining int
	// Weaknesses maps a combination type to the strongest value the player FAILED to beat.
	Weaknesses map[domain.CardCombinationType]int32
	// PlayedStats tracks how many of each combination type this opponent has played.
	PlayedStats map[domain.CardCombinationType]int
}

// NewOpponentProfile initializes a profile for a specific seat.
func NewOpponentProfile(seat int) *OpponentProfile {
	return &OpponentProfile{
		Seat:        seat,
		Weaknesses:  make(map[domain.CardCombinationType]int32),
		PlayedStats: make(map[domain.CardCombinationType]int),
	}
}

// RecordPlay logs a combination played by this opponent.
func (p *OpponentProfile) RecordPlay(combo domain.CardCombination) {
	if combo.Type == domain.Invalid {
		return
	}
	p.PlayedStats[combo.Type]++
}

// RecordFailure notes that this opponent could not (or chose not to) beat a specific combo.
func (p *OpponentProfile) RecordFailure(combo domain.CardCombination) {
	if combo.Type == domain.Invalid {
		return
	}
	
	currentMax, ok := p.Weaknesses[combo.Type]
	if !ok || combo.Value > currentMax {
		p.Weaknesses[combo.Type] = combo.Value
	}
}

// CanPossiblyBeat returns true if we have no evidence that the player cannot beat this combo.
func (p *OpponentProfile) CanPossiblyBeat(combo domain.CardCombination) bool {
	maxFailed, ok := p.Weaknesses[combo.Type]
	if !ok {
		return true // No evidence yet
	}
	
	// If the current combo is stronger than (or equal to) something they already failed to beat,
	// they certainly cannot beat this one either (assuming rational play).
	if combo.Value >= maxFailed {
		return false
	}
	
	return true
}