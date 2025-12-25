package app

import (
	"errors"
	"math/rand"
	"testing"

	"tienlen/internal/domain"
)

func TestStartGameDealsHands(t *testing.T) {
	rng := rand.New(rand.NewSource(42))
	svc := NewService(rng)

	// Pass player IDs directly to StartGame
	game, evs, err := svc.StartGame([]string{"u1", "u2"}, -1, 0)
	if err != nil {
		t.Fatalf("start game error: %v", err)
	}
	if game.Phase != domain.PhasePlaying {
		t.Fatalf("phase = %s, want playing", game.Phase)
	}

	gameStartedEvents := 0
	for _, ev := range evs {
		if ev.Kind == EventGameStarted {
			gameStartedEvents++
			payload := ev.Payload.(GameStartedPayload)
			if len(payload.Hand) != 13 {
				t.Fatalf("hand size = %d, want 13", len(payload.Hand))
			}
		}
	}
	if gameStartedEvents != 2 {
		t.Fatalf("game started events = %d, want 2", gameStartedEvents)
	}
}

func TestStartGameRejectsSinglePlayer(t *testing.T) {
	svc := NewService(nil)

	_, _, err := svc.StartGame([]string{"u1"}, -1, 0)
	if !errors.Is(err, ErrTooFewPlayers) {
		t.Fatalf("start game error = %v, want %v", err, ErrTooFewPlayers)
	}
}

func TestStartGameWithDeckRejectsSinglePlayer(t *testing.T) {
	svc := NewService(nil)

	_, _, err := svc.StartGameWithDeck([]string{"u1"}, -1, 0, domain.NewDeck())
	if !errors.Is(err, ErrTooFewPlayers) {
		t.Fatalf("start game with deck error = %v, want %v", err, ErrTooFewPlayers)
	}
}

func TestPlayCardsAndEnd(t *testing.T) {
	rng := rand.New(rand.NewSource(99))
	svc := NewService(rng)

	game, _, err := svc.StartGame([]string{"u1", "u2"}, -1, 0)
	if err != nil {
		t.Fatalf("start game error: %v", err)
	}

	// Force small hands for predictable end.
	// Suit 0=Spades, 1=Clubs, 2=Diamonds, 3=Hearts
	game.Players["u1"].Hand = []domain.Card{{Suit: 0, Rank: 0}}
	game.Players["u2"].Hand = []domain.Card{{Suit: 3, Rank: 1}}

	// Make sure it's u1's turn (since StartGame defaults to first player)
	game.CurrentTurn = 0 // u1 is seat 1 (index 0)

	evs, err := svc.PlayCards(game, 0, []domain.Card{{Suit: 0, Rank: 0}})
	if err != nil {
		t.Fatalf("play cards error: %v", err)
	}
	if game.Players["u1"].Finished != true {
		t.Fatalf("u1 should be finished")
	}
	// With two players, game ends when only one has cards left.
	if game.Phase != domain.PhaseEnded {
		t.Fatalf("game should have ended when one player remains")
	}
	if len(game.FinishOrderSeats) != 2 {
		t.Errorf("expected 2 players in finish order, got %d", len(game.FinishOrderSeats))
	}
	foundEnd := false
	for _, ev := range evs {
		if ev.Kind == EventGameEnded {
			foundEnd = true
			payload := ev.Payload.(GameEndedPayload)
			if len(payload.FinishOrderSeats) != 2 {
				t.Errorf("expected 2 players in GameEndedPayload, got %d", len(payload.FinishOrderSeats))
			}
			if len(payload.RemainingHands) == 0 {
				t.Errorf("expected remaining hands in GameEndedPayload")
			}
			// Player 2 lost, should have cards
			// Wait, the loser's cards are removed when the game ends? No, they stay in hand.
			// Actually, the game ends when count <= 1. The last player definitely has cards.
			// Let's check if there is at least one hand.
			foundHand := false
			for _, hand := range payload.RemainingHands {
				if len(hand) > 0 {
					foundHand = true
					break
				}
			}
			if !foundHand {
				t.Errorf("expected at least one remaining hand with cards")
			}
		}
	}
	if !foundEnd {
		t.Fatalf("expected game ended event")
	}
}

func TestPassAndRoundReset(t *testing.T) {
	svc := NewService(nil)
	players := []string{"u1", "u2", "u3"}
	game, _, _ := svc.StartGame(players, -1, 0)

	// Force hands to ensure they don't finish immediately
	for _, p := range game.Players {
		p.Hand = []domain.Card{{Suit: 0, Rank: 5}, {Suit: 1, Rank: 5}}
	}

	game.CurrentTurn = 0 // u1 is seat 1 -> index 0

	// u1 plays a single
	cards := []domain.Card{{Suit: 0, Rank: 5}}
	_, err := svc.PlayCards(game, 0, cards)
	if err != nil {
		t.Fatalf("u1 play error: %v", err)
	}

	if game.LastPlayerToPlaySeat != 0 {
		t.Fatalf("expected last player seat to be 0, got %d", game.LastPlayerToPlaySeat)
	}

	// u2 passes
	_, err = svc.PassTurn(game, 1)
	if err != nil {
		t.Fatalf("u2 pass error: %v", err)
	}

	// u3 passes
	_, err = svc.PassTurn(game, 2)
	if err != nil {
		t.Fatalf("u3 pass error: %v", err)
	}

	// Now it should be u1's turn again (seat 0), and the round should reset
	if game.CurrentTurn != 0 {
		t.Fatalf("expected turn to return to u1 (seat 0), got %d", game.CurrentTurn)
	}

	if game.LastPlayedCombination.Type != domain.Invalid {
		t.Fatalf("expected round to reset, but last played combo is still valid: %v", game.LastPlayedCombination.Type)
	}

	// Verify all players' HasPassed is reset
	for id, p := range game.Players {
		if p.HasPassed {
			t.Errorf("player %s should have HasPassed reset to false", id)
		}
	}
}

func TestPlayErrors(t *testing.T) {
	svc := NewService(nil)
	players := []string{"u1", "u2"}
	game, _, _ := svc.StartGame(players, -1, 0)

	game.Players["u1"].Hand = []domain.Card{{Suit: 0, Rank: 0}} // 3 Spades
	game.CurrentTurn = 0                                        // u1

	// 1. Play out of turn
	_, err := svc.PlayCards(game, 1, []domain.Card{{Suit: 0, Rank: 0}})
	if err != ErrNotYourTurn {
		t.Errorf("expected ErrNotYourTurn, got %v", err)
	}

	// 2. Play cards not in hand
	_, err = svc.PlayCards(game, 0, []domain.Card{{Suit: 3, Rank: 12}}) // 2 Hearts
	if err != ErrCardsNotInHand {
		t.Errorf("expected ErrCardsNotInHand, got %v", err)
	}

	// 3. Play valid cards but cannot beat previous
	game.LastPlayedCombination = domain.CardCombination{
		Type:  domain.Single,
		Cards: []domain.Card{{Suit: 3, Rank: 0}}, // 3 Hearts
	}
	_, err = svc.PlayCards(game, 0, []domain.Card{{Suit: 0, Rank: 0}}) // 3 Spades
	if err != ErrCannotBeat {
		t.Errorf("expected ErrCannotBeat, got %v", err)
	}
}

func TestRoundResetsWhenLastPlayerFinishes(t *testing.T) {
	svc := NewService(nil)
	players := []string{"u1", "u2", "u3"}
	game, _, _ := svc.StartGame(players, -1, 0)

	// Force u1 to have only one card
	game.Players["u1"].Hand = []domain.Card{{Suit: 3, Rank: 12}} // 2 Hearts
	game.CurrentTurn = 0                                         // u1

	// u1 plays their last card and finishes
	_, err := svc.PlayCards(game, 0, []domain.Card{{Suit: 3, Rank: 12}})
	if err != nil {
		t.Fatalf("u1 play error: %v", err)
	}

	if !game.Players["u1"].Finished {
		t.Fatal("u1 should be finished")
	}

	// u2 passes
	_, err = svc.PassTurn(game, 1)
	if err != nil {
		t.Fatalf("u2 pass error: %v", err)
	}

	// u3 passes
	_, err = svc.PassTurn(game, 2)
	if err != nil {
		t.Fatalf("u3 pass error: %v", err)
	}

	// Now it should be u2's turn (since u1 is finished), and the round should reset
	// u2 is seat 2 -> index 1
	if game.CurrentTurn != 1 {
		t.Fatalf("expected turn to go to u2 (seat index 1), got %d", game.CurrentTurn)
	}

	if game.LastPlayedCombination.Type != domain.Invalid {
		t.Fatalf("expected round to reset after everyone passed following a finished player")
	}
}

// TestNextPlayerAfterFinisherAndPasses exercises the seat sequencing when a player finishes.
// Player 2 plays a pair of "2 pigs" to finish, then players 3 and 1 pass in order.
// Since player 2 is done, the turn should return to player 3 after the pass chain.
func TestNextPlayerAfterFinisherAndPasses(t *testing.T) {
	svc := NewService(nil)
	players := []string{"p1", "p2", "p3"}
	game, _, err := svc.StartGame(players, -1, 0)
	if err != nil {
		t.Fatalf("start game error: %v", err)
	}

	game.Players["p1"].Hand = []domain.Card{
		{Suit: 0, Rank: 5},
		{Suit: 1, Rank: 6},
	}
	game.Players["p2"].Hand = []domain.Card{
		{Suit: 0, Rank: 12}, // 2 Spades
		{Suit: 1, Rank: 12}, // 2 Clubs
	}
	game.Players["p3"].Hand = []domain.Card{
		{Suit: 2, Rank: 6},
		{Suit: 3, Rank: 8},
	}

	game.CurrentTurn = 1

	// Player 2 plays both 2s and finishes.
	_, err = svc.PlayCards(game, 1, []domain.Card{
		{Suit: 0, Rank: 12},
		{Suit: 1, Rank: 12},
	})
	if err != nil {
		t.Fatalf("player 2 play error: %v", err)
	}
	if !game.Players["p2"].Finished {
		t.Fatalf("player 2 should be finished")
	}
	if game.CurrentTurn != 2 {
		t.Fatalf("expected player 3 to be next immediately after player 2 finishes, got seat %d", game.CurrentTurn)
	}

	// Player 3 passes, forcing a new round and giving the turn to player 1.
	_, err = svc.PassTurn(game, 2)
	if err != nil {
		t.Fatalf("player 3 pass error: %v", err)
	}
	if game.CurrentTurn != 0 {
		t.Fatalf("expected player 1 after player 3 passes, got seat %d", game.CurrentTurn)
	}
	if game.LastPlayedCombination.Type != domain.Invalid {
		t.Fatalf("expected round reset after player 3 passes")
	}

	// Player 1 passes, leaving player 3 as the only active seat; current turn should return to player 3.
	_, err = svc.PassTurn(game, 0)
	if err != nil {
		t.Fatalf("player 1 pass error: %v", err)
	}
	if game.CurrentTurn != 2 {
		t.Fatalf("expected player 3 to regain the turn, got seat %d", game.CurrentTurn)
	}
	if game.LastPlayedCombination.Type != domain.Invalid {
		t.Fatalf("expected round reset after player 1 passes")
	}
}

func TestTimeoutTurn(t *testing.T) {
	svc := NewService(nil)
	players := []string{"u1", "u2"}
	game, _, _ := svc.StartGame(players, -1, 0)

	// Case 1: New Round (Leader) Timeout -> Play Smallest Card
	game.LastPlayedCombination = domain.CardCombination{Type: domain.Invalid}
	game.CurrentTurn = 0 // u1

	// Ensure hand has a known smallest card
	// 3 Spades (0,0) is smallest.
	game.Players["u1"].Hand = []domain.Card{
		{Rank: 12, Suit: 3}, // 2 Hearts (Big)
		{Rank: 0, Suit: 0},  // 3 Spades (Smallest)
		{Rank: 5, Suit: 1},  // 8 Clubs (Medium)
	}

	events, err := svc.TimeoutTurn(game, 0)
	if err != nil {
		t.Fatalf("timeout turn error: %v", err)
	}

	// Should result in a PlayCards event
	foundPlay := false
	for _, ev := range events {
		if ev.Kind == EventCardPlayed {
			foundPlay = true
			p := ev.Payload.(CardPlayedPayload)
			if len(p.Cards) != 1 {
				t.Fatalf("expected 1 card played, got %d", len(p.Cards))
			}
			if p.Cards[0].Rank != 0 || p.Cards[0].Suit != 0 {
				t.Errorf("expected 3 Spades (0,0) to be played, got %+v", p.Cards[0])
			}
		}
	}
	if !foundPlay {
		t.Fatal("expected PlayCards event on new round timeout")
	}

	// Case 2: Mid Round Timeout -> Pass Turn
	game.LastPlayedCombination = domain.CardCombination{
		Type:  domain.Single,
		Cards: []domain.Card{{Rank: 0, Suit: 0}},
	}
	game.CurrentTurn = 1 // u2

	events, err = svc.TimeoutTurn(game, 1)
	if err != nil {
		t.Fatalf("timeout turn error (mid round): %v", err)
	}

	// Should result in a TurnPassed event
	foundPass := false
	for _, ev := range events {
		if ev.Kind == EventTurnPassed {
			foundPass = true
			p := ev.Payload.(TurnPassedPayload)
			if p.Seat != 1 {
				t.Errorf("expected pass from seat 1, got %d", p.Seat)
			}
		}
	}
	if !foundPass {
		t.Fatal("expected TurnPassed event on mid round timeout")
	}
}

func TestPlayerFinishedEvent(t *testing.T) {
	svc := NewService(nil)
	players := []string{"u1", "u2", "u3"}
	game, _, _ := svc.StartGame(players, -1, 0)

	// u1 has 1 card
	game.Players["u1"].Hand = []domain.Card{{Rank: 0, Suit: 0}}
	game.CurrentTurn = 0

	events, err := svc.PlayCards(game, 0, []domain.Card{{Rank: 0, Suit: 0}})
	if err != nil {
		t.Fatalf("PlayCards error: %v", err)
	}

	foundFinisher := false
	for _, ev := range events {
		if ev.Kind == EventPlayerFinished {
			foundFinisher = true
			payload := ev.Payload.(PlayerFinishedPayload)
			if payload.Seat != 0 {
				t.Errorf("expected seat 0, got %d", payload.Seat)
			}
			if payload.Rank != 1 {
				t.Errorf("expected rank 1, got %d", payload.Rank)
			}
		}
	}

	if !foundFinisher {
		t.Fatal("EventPlayerFinished not found in events")
	}
}
