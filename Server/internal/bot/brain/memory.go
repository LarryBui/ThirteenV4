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
}

// NewMemory initializes a fresh memory state where all cards are unknown.
func NewMemory() *GameMemory {
	return &GameMemory{}
}

// Reset clears the memory for a new game.
func (m *GameMemory) Reset() {
	for i := range m.DeckStatus {
		m.DeckStatus[i] = StatusUnknown
	}
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
