package nakama

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"math/rand"
	"testing"
	"tienlen/internal/bot"
	"tienlen/internal/config"
	"tienlen/internal/domain"
	"tienlen/internal/ports"

	pb "tienlen/proto"

	"github.com/heroiclabs/nakama-common/api"
	"github.com/heroiclabs/nakama-common/runtime"
	"google.golang.org/protobuf/encoding/protojson"
	"google.golang.org/protobuf/proto"
)

// mockBotBalancer implements BotBalancer for testing.
type mockBotBalancer struct {
	accounts map[string]*api.Account
	wallets  map[string]map[string]int64
}

func (m *mockBotBalancer) AccountGetId(ctx context.Context, userID string) (*api.Account, error) {
	if acc, ok := m.accounts[userID]; ok {
		return acc, nil
	}
	// Return a default account with empty wallet if not found, to avoid nil pointers in logic
	return &api.Account{Wallet: "{}"}, nil
}

func (m *mockBotBalancer) WalletUpdate(ctx context.Context, userID string, changeset map[string]int64, metadata map[string]interface{}, updateLedger bool) (map[string]int64, map[string]int64, error) {
	if m.wallets == nil {
		m.wallets = make(map[string]map[string]int64)
	}
	if _, ok := m.wallets[userID]; !ok {
		m.wallets[userID] = make(map[string]int64)
	}
	prev := make(map[string]int64)
	for k, v := range m.wallets[userID] {
		prev[k] = v
	}
	for k, v := range changeset {
		m.wallets[userID][k] += v
	}
	return prev, m.wallets[userID], nil
}

// noopLogger implements runtime.Logger for tests that only need to satisfy the interface.
type noopLogger struct{}

func (noopLogger) Debug(string, ...interface{}) {}
func (noopLogger) Info(string, ...interface{})  {}
func (noopLogger) Warn(string, ...interface{})  {}
func (noopLogger) Error(string, ...interface{}) {}
func (noopLogger) WithField(string, interface{}) runtime.Logger {
	return noopLogger{}
}
func (noopLogger) WithFields(map[string]interface{}) runtime.Logger {
	return noopLogger{}
}
func (noopLogger) Fields() map[string]interface{} {
	return nil
}

// mockDispatcher records match dispatcher calls for assertions.
type mockDispatcher struct {
	broadcastCount int
	labelUpdates   int
	lastOpCode     int64
	lastData       []byte
}

func (md *mockDispatcher) BroadcastMessage(opCode int64, data []byte, presences []runtime.Presence, sender runtime.Presence, reliable bool) error {
	md.broadcastCount++
	md.lastOpCode = opCode
	md.lastData = append([]byte(nil), data...)
	return nil
}

func (md *mockDispatcher) BroadcastMessageDeferred(opCode int64, data []byte, presences []runtime.Presence, sender runtime.Presence, reliable bool) error {
	return nil
}

func (md *mockDispatcher) MatchKick(presences []runtime.Presence) error {
	return nil
}

func (md *mockDispatcher) MatchLabelUpdate(label string) error {
	md.labelUpdates++
	return nil
}

type mockEconomy struct {
	balances map[string]int64
	calls    map[string]int
}

func (me *mockEconomy) GetBalance(ctx context.Context, userID string) (int64, error) {
	if me.calls == nil {
		me.calls = make(map[string]int)
	}
	me.calls[userID]++
	if balance, ok := me.balances[userID]; ok {
		return balance, nil
	}
	return 0, errors.New("balance not found")
}

func (me *mockEconomy) UpdateBalances(ctx context.Context, updates []ports.WalletUpdate) error {
	return nil
}

func init() {
	// Load bot identities for testing.
	if err := bot.LoadIdentities("test_bot_identities.json"); err != nil {
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
			expected: `{"open":3,"state":"lobby"}`,
		},
		{
			name: "PlayingState",
			label: &pb.MatchLabel{
				Open:  0,
				State: "playing",
			},
			expected: `{"open":0,"state":"playing"}`,
		},
	}

	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			payload, err := (&protojson.MarshalOptions{EmitUnpopulated: true}).Marshal(test.label)
			if err != nil {
				t.Fatalf("Failed to marshal label: %v", err)
			}
			var compact bytes.Buffer
			if err := json.Compact(&compact, payload); err != nil {
				t.Fatalf("Failed to compact label JSON: %v", err)
			}
			if compact.String() != test.expected {
				t.Errorf("Got %s, want %s", compact.String(), test.expected)
			}
		})
	}
}

func TestResetTurnSecondsRemainingWithBonus(t *testing.T) {
	handler := &matchHandler{}
	state := &MatchState{
		Game: &domain.Game{
			Phase:       domain.PhasePlaying,
			CurrentTurn: 1,
		},
	}

	duration := 16
	if cfg := config.GetGameConfig(); cfg != nil {
		duration = cfg.TurnDurationSeconds
	}

	handler.resetTurnSecondsRemainingWithBonus(state, noopLogger{}, gameStartTurnTimerBonusSeconds)

	want := int64(duration + gameStartTurnTimerBonusSeconds)
	if state.TurnSecondsRemaining != want {
		t.Fatalf("TurnSecondsRemaining = %d, want %d", state.TurnSecondsRemaining, want)
	}
}

func TestProcessBots_AddsTwoBotsForSoloHuman(t *testing.T) {
	handler := &matchHandler{}
	dispatcher := &mockDispatcher{}
	state := &MatchState{
		Seats:                [4]string{"user-1", "", "", ""},
		Presences:            make(map[string]runtime.Presence),
		Bots:                 make(map[string]*bot.Agent),
		BotAutoFillDelay:     2,
		LastSinglePlayerTick: 8,
		Tick:                 10,
	}

	balancer := &mockBotBalancer{
		accounts: make(map[string]*api.Account),
	}
	handler.processBots(context.Background(), state, dispatcher, noopLogger{}, balancer)

	botCount := 0
	for _, seat := range state.Seats {
		if isBotUserId(seat) {
			botCount++
		}
	}

	if botCount != 2 {
		t.Fatalf("Expected 2 bots, got %d", botCount)
	}
	if state.GetOpenSeatsCount() != 1 {
		t.Fatalf("Expected 1 open seat after auto-fill, got %d", state.GetOpenSeatsCount())
	}
	if state.LastSinglePlayerTick != 0 {
		t.Fatalf("Expected auto-fill timer reset, got %d", state.LastSinglePlayerTick)
	}
	if dispatcher.broadcastCount == 0 || dispatcher.labelUpdates == 0 {
		t.Fatalf("Expected match state broadcast and label update after auto-fill")
	}
}

func TestBroadcastMatchState_IncludesBalances(t *testing.T) {
	handler := &matchHandler{}
	dispatcher := &mockDispatcher{}
	botID := bot.GetBotIdentity(0).UserID
	economy := &mockEconomy{
		balances: map[string]int64{
			"user-1": 1200,
			botID:    5000,
		},
	}
	state := &MatchState{
		Seats:     [4]string{"user-1", botID, "", ""},
		OwnerSeat: 0,
		Tick:      42,
		Presences: make(map[string]runtime.Presence),
		Economy:   economy,
	}

	handler.broadcastMatchState(context.Background(), state, dispatcher, noopLogger{})

	if dispatcher.lastOpCode != int64(pb.OpCode_OP_CODE_PLAYER_JOINED) {
		t.Fatalf("Expected opcode %d, got %d", pb.OpCode_OP_CODE_PLAYER_JOINED, dispatcher.lastOpCode)
	}
	if len(dispatcher.lastData) == 0 {
		t.Fatalf("Expected snapshot payload to be broadcast")
	}

	snapshot := &pb.MatchStateSnapshot{}
	if err := proto.Unmarshal(dispatcher.lastData, snapshot); err != nil {
		t.Fatalf("Failed to unmarshal snapshot: %v", err)
	}

	balances := make(map[string]int64)
	for _, player := range snapshot.Players {
		balances[player.UserId] = player.Balance
	}

	if got := balances["user-1"]; got != 1200 {
		t.Fatalf("Expected human balance 1200, got %d", got)
	}
	if got := balances[botID]; got != 5000 {
		t.Fatalf("Expected bot balance 5000, got %d", got)
	}
	if economy.calls["user-1"] != 1 {
		t.Fatalf("Expected balance lookup for human, got %d", economy.calls["user-1"])
	}
	if economy.calls[botID] != 1 {
		t.Fatalf("Expected balance lookup for bot, got %d", economy.calls[botID])
	}
}

func TestParseRiggedHandTexts_ParsesCards(t *testing.T) {
	hands, err := parseRiggedHandTexts([]riggedHandText{
		{Seat: 0, Cards: "3H, 10S, QD"},
	})
	if err != nil {
		t.Fatalf("Expected no error, got %v", err)
	}
	if len(hands) != 1 {
		t.Fatalf("Expected 1 hand, got %d", len(hands))
	}
	if hands[0].FillAll {
		t.Fatalf("Expected FillAll false, got true")
	}

	got := hands[0].Cards
	if len(got) != 3 {
		t.Fatalf("Expected 3 cards, got %d", len(got))
	}

	want := []domain.Card{
		{Rank: 0, Suit: 3},
		{Rank: 7, Suit: 0},
		{Rank: 9, Suit: 2},
	}
	for i, card := range want {
		if got[i] != card {
			t.Fatalf("Card %d = %+v, want %+v", i, got[i], card)
		}
	}
}

func TestParseRiggedHandTexts_AllToken(t *testing.T) {
	hands, err := parseRiggedHandTexts([]riggedHandText{
		{Seat: 1, Cards: "ALL"},
	})
	if err != nil {
		t.Fatalf("Expected no error, got %v", err)
	}
	if len(hands) != 1 {
		t.Fatalf("Expected 1 hand, got %d", len(hands))
	}
	if !hands[0].FillAll {
		t.Fatalf("Expected FillAll true, got false")
	}
	if len(hands[0].Cards) != 0 {
		t.Fatalf("Expected 0 explicit cards, got %d", len(hands[0].Cards))
	}
}

func TestParseRiggedHandTexts_DuplicateCard(t *testing.T) {
	_, err := parseRiggedHandTexts([]riggedHandText{
		{Seat: 0, Cards: "3H"},
		{Seat: 1, Cards: "3H"},
	})
	if err == nil {
		t.Fatalf("Expected error for duplicate card")
	}
}

func TestParseRiggedHandTexts_InvalidToken(t *testing.T) {
	_, err := parseRiggedHandTexts([]riggedHandText{
		{Seat: 0, Cards: "1H"},
	})
	if err == nil {
		t.Fatalf("Expected error for invalid token")
	}
}

func TestBuildRiggedDeck_TextWithoutAll_UsesShuffledFill(t *testing.T) {
	shuffle := deterministicShuffle(7)
	shuffled := shuffle(domain.NewDeck())
	plans := []riggedHandPlan{
		{Seat: 0, Cards: []domain.Card{{Rank: 0, Suit: 3}}, FillAll: false},
	}
	deck, err := buildRiggedDeckWithShuffler(plans, false, shuffle)
	if err != nil {
		t.Fatalf("Expected no error, got %v", err)
	}

	expected := domain.Card{}
	for _, card := range shuffled {
		if card == (domain.Card{Rank: 0, Suit: 3}) {
			continue
		}
		expected = card
		break
	}

	if deck[0] != (domain.Card{Rank: 0, Suit: 3}) {
		t.Fatalf("Expected explicit card first")
	}
	if deck[1] != expected {
		t.Fatalf("Expected shuffled fill %v, got %v", expected, deck[1])
	}
}

func TestBuildRiggedDeck_TextWithAll_UsesOrderedFill(t *testing.T) {
	plans := []riggedHandPlan{
		{Seat: 0, Cards: []domain.Card{{Rank: 0, Suit: 3}}, FillAll: true},
	}
	deck, err := buildRiggedDeckWithShuffler(plans, false, deterministicShuffle(7))
	if err != nil {
		t.Fatalf("Expected no error, got %v", err)
	}

	expected := domain.Card{Rank: 0, Suit: 0}
	if deck[0] != (domain.Card{Rank: 0, Suit: 3}) {
		t.Fatalf("Expected explicit card first")
	}
	if deck[1] != expected {
		t.Fatalf("Expected ordered fill %v, got %v", expected, deck[1])
	}
}

func deterministicShuffle(seed int64) func([]domain.Card) []domain.Card {
	return func(deck []domain.Card) []domain.Card {
		out := make([]domain.Card, len(deck))
		copy(out, deck)
		rng := rand.New(rand.NewSource(seed))
		rng.Shuffle(len(out), func(i, j int) {
			out[i], out[j] = out[j], out[i]
		})
		return out
	}
}
