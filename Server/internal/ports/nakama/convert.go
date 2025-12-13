package nakama

import (
	"tienlen/internal/domain"
	pb "tienlen/proto"
)

func cardsToProto(cards []domain.Card) []*pb.Card {
	out := make([]*pb.Card, 0, len(cards))
	for _, c := range cards {
		out = append(out, &pb.Card{
			Suit: suitToProto(c.Suit),
			Rank: int32(c.Rank),
		})
	}
	return out
}

func cardsFromProto(cards []*pb.Card) []domain.Card {
	out := make([]domain.Card, 0, len(cards))
	for _, c := range cards {
		out = append(out, domain.Card{
			Suit: suitFromProto(c.GetSuit()),
			Rank: int(c.GetRank()),
		})
	}
	return out
}

func suitToProto(s string) pb.Card_Suit {
	switch s {
	case "S":
		return pb.Card_SUIT_SPADES
	case "H":
		return pb.Card_SUIT_HEARTS
	case "D":
		return pb.Card_SUIT_DIAMONDS
	case "C":
		return pb.Card_SUIT_CLUBS
	default:
		return pb.Card_SUIT_UNSPECIFIED
	}
}

func suitFromProto(s pb.Card_Suit) string {
	switch s {
	case pb.Card_SUIT_SPADES:
		return "S"
	case pb.Card_SUIT_HEARTS:
		return "H"
	case pb.Card_SUIT_DIAMONDS:
		return "D"
	case pb.Card_SUIT_CLUBS:
		return "C"
	default:
		return ""
	}
}
