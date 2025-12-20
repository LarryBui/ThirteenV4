package nakama

import (
	"testing"
	"tienlen/internal/bot"

	pb "tienlen/proto"

	"google.golang.org/protobuf/encoding/protojson"
)

func init() {
	// Load bot identities for testing.
	if err := bot.LoadIdentities("../../../data/bot_identities.json"); err != nil {
		panic("Failed to load bot identities for tests: " + err.Error())
	}
}

func TestFindFirstHumanSeat(t *testing.T) {
	bot1 := bot.GetBotIdentity(0).UserID
	bot2 := bot.GetBotIdentity(1).UserID

	tests := []struct {
		name  string
		seats []string
		want  int
	}{
		{
			name:  "FirstHumanAfterBot",
			seats: []string{bot1, "user-1", "", ""},
			want:  1,
		},
		{
			name:  "AllBots",
			seats: []string{bot1, bot2, "", ""},
			want:  -1,
		},
		{
			name:  "AllEmpty",
			seats: []string{"", "", "", ""},
			want:  -1,
		},
		{
			name:  "FirstHumanIsSeatZero",
			seats: []string{"user-1", bot1, "user-2", ""},
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

func TestShouldTerminateNoHumans(t *testing.T) {
	bot1 := bot.GetBotIdentity(0).UserID
	bot2 := bot.GetBotIdentity(1).UserID
	bot3 := bot.GetBotIdentity(2).UserID
	bot4 := bot.GetBotIdentity(3).UserID

	tests := []struct {
		name  string
		seats []string
		want  bool
	}{
		{
			name:  "BotsOnly",
			seats: []string{bot1, bot2, bot3, bot4},
			want:  true,
		},
		{
			name:  "BotsAndEmpty",
			seats: []string{bot1, "", bot3, ""},
			want:  true,
		},
		{
			name:  "HumansPresent",
			seats: []string{bot1, "user-1", "", ""},
			want:  false,
		},
		{
			name:  "AllEmpty",
			seats: []string{"", "", "", ""},
			want:  true,
		},
	}

	for _, test := range tests {
		test := test
		t.Run(test.name, func(t *testing.T) {
			if got := shouldTerminateNoHumans(test.seats); got != test.want {
				t.Fatalf("shouldTerminateNoHumans() = %t, want %t", got, test.want)
			}
		})
	}
}

func TestMatchLabel_Marshal(t *testing.T) {
	tests := []struct {
		name     string
		label    *pb.MatchLabel
		expected string
	}{
		{
			name: "LobbyState",
			label: &pb.MatchLabel{
				Open:  3,
				State: "lobby",
			},
			expected: `{"open":3, "state":"lobby"}`,
		},
		{
			name: "PlayingState",
			label: &pb.MatchLabel{
				Open:  0,
				State: "playing",
			},
			expected: `{"open":0, "state":"playing"}`,
		},
	}

	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			bytes, err := (&protojson.MarshalOptions{EmitUnpopulated: true}).Marshal(test.label)
			if err != nil {
				t.Fatalf("Failed to marshal label: %v", err)
			}
			if string(bytes) != test.expected {
				t.Errorf("Got %s, want %s", string(bytes), test.expected)
			}
		})
	}
}