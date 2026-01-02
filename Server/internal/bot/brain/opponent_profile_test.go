package brain

import (
	"testing"
	"tienlen/internal/domain"
)

func TestOpponentProfile_RecordPlay(t *testing.T) {
	p := NewOpponentProfile(1)
	
	combo := domain.CardCombination{
		Type:  domain.Pair,
		Value: 20,
	}
	
	p.RecordPlay(combo)
	p.RecordPlay(combo) // Played two pairs
	
	if p.PlayedStats[domain.Pair] != 2 {
		t.Errorf("Expected 2 pairs played, got %d", p.PlayedStats[domain.Pair])
	}
	
	if p.PlayedStats[domain.Single] != 0 {
		t.Errorf("Expected 0 singles played")
	}
}
