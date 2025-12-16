package domain

import (
	"math/rand"
	"time"
	pb "tienlen/proto"
)

func NewDeck() []*pb.Card {
	deck := make([]*pb.Card, 0, 52)
	for suit := 0; suit < 4; suit++ {
		for rank := 0; rank < 13; rank++ {
			deck = append(deck, &pb.Card{
				Suit: pb.Suit(suit),
				Rank: pb.Rank(rank),
			})
		}
	}
	return deck
}

func Shuffle(deck []*pb.Card) {
	rng := rand.New(rand.NewSource(time.Now().UnixNano()))
	rng.Shuffle(len(deck), func(i, j int) {
		deck[i], deck[j] = deck[j], deck[i]
	})
}
