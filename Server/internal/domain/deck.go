package domain

import (
	"math/rand"
	"sort"
)

// NewDeck returns a sorted 52-card deck.
func NewDeck() []Card {
	deck := make([]Card, 0, 52)
	for r := int32(0); r <= 12; r++ {
		for s := int32(0); s <= 3; s++ {
			deck = append(deck, Card{Rank: r, Suit: s})
		}
	}
	return deck
}

// ShuffleDeck returns a shuffled copy of the given deck.
func ShuffleDeck(deck []Card) []Card {
	out := make([]Card, len(deck))
	copy(out, deck)
	rand.Shuffle(len(out), func(i, j int) { out[i], out[j] = out[j], out[i] })
	return out
}

// SortHand orders a hand by ascending power.
func SortHand(cards []Card) {
	sort.Slice(cards, func(i, j int) bool {
		return CardPower(cards[i]) < CardPower(cards[j])
	})
}

// CardPower returns the absolute power value of a card (Rank*4 + Suit).
func CardPower(c Card) int32 {
	return c.Rank*4 + c.Suit
}

// GetSmallestCard returns the card with the lowest power in the hand.
// Assumes hand is not empty. Returns zero value card if empty.
func GetSmallestCard(hand []Card) Card {
	if len(hand) == 0 {
		return Card{}
	}
	minCard := hand[0]
	minVal := CardPower(minCard)
	
	for _, c := range hand[1:] {
		val := CardPower(c)
		if val < minVal {
			minVal = val
			minCard = c
		}
	}
	return minCard
}