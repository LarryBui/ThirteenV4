package domain

import (
	"testing"
)

func TestCalculateSettlement(t *testing.T) {
	tests := []struct {
		name            string
		playerCount     int
		finishOrder     []int // seat indices 0-based
		baseBet         int64
		expectedChanges map[string]int64 // seat index -> amount
	}{
		{
			name:        "4 Players: Standard distribution",
			playerCount: 4,
			finishOrder: []int{0, 1, 2, 3}, // 1st: Seat 0, 2nd: Seat 1, 3rd: Seat 2, 4th: Seat 3
			baseBet:     100,
			expectedChanges: map[string]int64{
				"u0": 200,  // +2
				"u1": 100,  // +1
				"u2": -100, // -1
				"u3": -200, // -2
			},
		},
		{
			name:        "3 Players: Standard distribution",
			playerCount: 3,
			finishOrder: []int{2, 0, 1}, // 1st: Seat 2, 2nd: Seat 0, 3rd: Seat 1
			baseBet:     100,
			expectedChanges: map[string]int64{
				"u2": 300,  // +3
				"u0": -100, // -1
				"u1": -200, // -2
			},
		},
		{
			name:        "2 Players: Standard distribution",
			playerCount: 2,
			finishOrder: []int{1, 0}, // 1st: Seat 1, 2nd: Seat 0
			baseBet:     100,
			expectedChanges: map[string]int64{
				"u1": 100,  // +1
				"u0": -100, // -1
			},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			players := make(map[string]*Player)
			for i := 0; i < tt.playerCount; i++ {
				uid := "u" + string(rune('0'+i))
				players[uid] = &Player{
					UserID: uid,
					Seat:   i,
				}
			}

			game := &Game{
				Players:          players,
				FinishOrderSeats: tt.finishOrder,
				BaseBet:          tt.baseBet,
			}

			settlement := game.CalculateSettlement()
			
			if len(settlement.BalanceChanges) != tt.playerCount {
				t.Errorf("expected %d changes, got %d", tt.playerCount, len(settlement.BalanceChanges))
			}

			for uid, want := range tt.expectedChanges {
				if got := settlement.BalanceChanges[uid]; got != want {
					t.Errorf("player %s: got %d, want %d", uid, got, want)
				}
			}
		})
	}
}
