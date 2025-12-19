package nakama

import "testing"

func TestFindFirstHumanSeat(t *testing.T) {
	tests := []struct {
		name  string
		seats []string
		want  int
	}{
		{
			name:  "FirstHumanAfterBot",
			seats: []string{"bot:0", "user-1", "", ""},
			want:  1,
		},
		{
			name:  "AllBots",
			seats: []string{"bot:0", "bot:1", "", ""},
			want:  -1,
		},
		{
			name:  "AllEmpty",
			seats: []string{"", "", "", ""},
			want:  -1,
		},
		{
			name:  "FirstHumanIsSeatZero",
			seats: []string{"user-1", "bot:1", "user-2", ""},
			want:  0,
		},
	}

	for _, test := range tests {
		test := test
		t.Run(test.name, func(t *testing.T) {
			if got := findFirstHumanSeat(test.seats); got != test.want {
				t.Fatalf("findFirstHumanSeat() = %d, want %d", got, test.want)
			}
		})
	}
}

func TestShouldTerminateAllBots(t *testing.T) {
	tests := []struct {
		name  string
		seats []string
		want  bool
	}{
		{
			name:  "BotsOnly",
			seats: []string{"bot:0", "bot:1", "bot:2", "bot:3"},
			want:  true,
		},
		{
			name:  "BotsAndEmpty",
			seats: []string{"bot:0", "", "bot:2", ""},
			want:  true,
		},
		{
			name:  "HumansPresent",
			seats: []string{"bot:0", "user-1", "", ""},
			want:  false,
		},
		{
			name:  "AllEmpty",
			seats: []string{"", "", "", ""},
			want:  false,
		},
	}

	for _, test := range tests {
		test := test
		t.Run(test.name, func(t *testing.T) {
			if got := shouldTerminateAllBots(test.seats); got != test.want {
				t.Fatalf("shouldTerminateAllBots() = %t, want %t", got, test.want)
			}
		})
	}
}
