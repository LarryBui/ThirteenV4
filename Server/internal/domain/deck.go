package domain

import (
	"math/rand"
	"time"
)

// NewDeck returns a standard 52-card deck in domain format.
func NewDeck() []Card {
	suits := []string{"S", "C", "D", "H"}
	deck := make([]Card, 0, 52)
	for _, suit := range suits {
		for rank := 0; rank < 13; rank++ {
			deck = append(deck, Card{
				Suit: suit,
				Rank: rank,
			})
		}
	}
	return deck
}

// Shuffle randomizes the deck order in-place.
func Shuffle(deck []Card) {
	rng := rand.New(rand.NewSource(time.Now().UnixNano()))
	rng.Shuffle(len(deck), func(i, j int) {
		deck[i], deck[j] = deck[j], deck[i]
	})
}
