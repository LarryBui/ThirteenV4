package brain

import (
	"tienlen/internal/domain"
)

// CardStatus represents what the bot knows about a specific card.
type CardStatus int

const (
	StatusUnknown CardStatus = iota // We don't know who has it
	StatusMine                      // In the bot's hand
	StatusPlayed                    // Already on the table (discarded)
	StatusOpponent                  // Inferred to be in an opponent's hand
)

// GameMemory stores the bot's private "view" of the game.
type GameMemory struct {
	// DeckStatus tracks all 52 cards. Index = Rank*4 + Suit.
	DeckStatus [52]CardStatus
	// Opponents tracks behavioral profiles by seat index.
	Opponents map[int]*OpponentProfile
	// CurrentCombo represents the combination currently on the table to beat.
	CurrentCombo domain.CardCombination
}

// NewMemory initializes a fresh memory state.
func NewMemory() *GameMemory {
	return &GameMemory{
		Opponents: make(map[int]*OpponentProfile),
	}
}

// Reset clears the memory for a new game.
func (m *GameMemory) Reset() {
	for i := range m.DeckStatus {
		m.DeckStatus[i] = StatusUnknown
	}
	// Reset opponent profiles
	for seat, p := range m.Opponents {
		p.Weaknesses = make(map[domain.CardCombinationType]int32)
		p.PlayedStats = make(map[domain.CardCombinationType]int)
		p.CardsRemaining = 13 // Reset to starting hand size
		_ = seat
	}
	m.CurrentCombo = domain.CardCombination{Type: domain.Invalid}
}

// MarkMine records the cards currently in the bot's hand.
func (m *GameMemory) MarkMine(cards []domain.Card) {
	for _, c := range cards {
		m.DeckStatus[cardToIndex(c)] = StatusMine
	}
}

// MarkPlayed records cards that have been played on the table.
func (m *GameMemory) MarkPlayed(cards []domain.Card) {
	for _, c := range cards {
		m.DeckStatus[cardToIndex(c)] = StatusPlayed
	}
}

// MarkOpponent records cards inferred to be with opponents.
func (m *GameMemory) MarkOpponent(cards []domain.Card) {
	for _, c := range cards {
		m.DeckStatus[cardToIndex(c)] = StatusOpponent
	}
}

// UpdateHand synchronization. Marks current hand as Mine and others that were Mine as Unknown.
func (m *GameMemory) UpdateHand(hand []domain.Card) {
	// First, revert current Mine cards to Unknown (assuming they were played or shifted)
	for i, status := range m.DeckStatus {
		if status == StatusMine {
			m.DeckStatus[i] = StatusUnknown
		}
	}
	// Mark new hand
	m.MarkMine(hand)
}

// UpdateTable records the combination currently on the table.
func (m *GameMemory) UpdateTable(cards []domain.Card) {
	if len(cards) == 0 {
		m.CurrentCombo = domain.CardCombination{Type: domain.Invalid}
		return
	}
	m.CurrentCombo = domain.IdentifyCombination(cards)
	m.MarkPlayed(cards)
}

// RecordPlay logs that a specific seat played a set of cards.
func (m *GameMemory) RecordPlay(seat int, cards []domain.Card) {
	if len(cards) == 0 {
		return
	}

	p, ok := m.Opponents[seat]
	if !ok {
		p = NewOpponentProfile(seat)
		p.CardsRemaining = 13
		m.Opponents[seat] = p
	}

	p.RecordPlay(m.CurrentCombo)
	p.CardsRemaining -= len(cards)
	if p.CardsRemaining < 0 {
		p.CardsRemaining = 0
	}
}

// RecordPass notes that an opponent passed on the current table combination.
func (m *GameMemory) RecordPass(seat int) {
	if m.CurrentCombo.Type == domain.Invalid {
		return
	}

	p, ok := m.Opponents[seat]
	if !ok {
		p = NewOpponentProfile(seat)
		m.Opponents[seat] = p
	}
	p.RecordFailure(m.CurrentCombo)
}

// IsBoss returns true if no higher card exists in an unknown or opponent hand.
func (m *GameMemory) IsBoss(c domain.Card) bool {
	idx := cardToIndex(c)
	for i := idx + 1; i < 52; i++ {
		if m.DeckStatus[i] == StatusUnknown || m.DeckStatus[i] == StatusOpponent {
			return false
		}
	}
	return true
}

// IsPlayed returns true if the card is already out of the game.
func (m *GameMemory) IsPlayed(c domain.Card) bool {
	return m.DeckStatus[cardToIndex(c)] == StatusPlayed
}

// cardToIndex converts domain.Card to a 0-51 index.
// Rank: 0 (3) to 12 (2). Suit: 0 (Spades) to 3 (Hearts).
func cardToIndex(c domain.Card) int {
	return int(c.Rank)*4 + int(c.Suit)
}