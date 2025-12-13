package main

import (
	"context"
	"database/sql"
	"encoding/json"
	"math/rand"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

// TienLenMatch implements Nakama's runtime.Match interface for the Tien Len game.
type TienLenMatch struct{}

// MatchState holds authoritative state for a Tien Len match instance.
type MatchState struct {
	Phase Phase

	Players map[string]*PlayerState // userId -> player
	Seats   [4]string               // index 0..3 => userId or ""

	OwnerUserID string

	// Turn / Round tracking
	CurrentTurnSeat int
	RoundLeaderSeat int
	LastPlaySeat    int

	// Finish order
	FinishOrder []string // userIds in order they went out
}

// MatchInit boots a new Tien Len match with lobby phase and default label.
func (m *TienLenMatch) MatchInit(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, params map[string]interface{}) (interface{}, int, string) {
	state := &MatchState{
		Phase:   PhaseLobby,
		Players: map[string]*PlayerState{},
	}

	labelBytes, _ := json.Marshal(Label{Open: true, Game: "tienlen", Phase: string(PhaseLobby)})
	return state, 10, string(labelBytes) // tickrate must be 1..60 :contentReference[oaicite:14]{index=14}
}

// MatchJoinAttempt validates whether a presence is allowed to join the match.
func (m *TienLenMatch) MatchJoinAttempt(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presence runtime.Presence, metadata map[string]string) (interface{}, bool, string) {

	s := state.(*MatchState)

	// Allow rejoin; disallow new joins once playing (prototype rule).
	if s.Phase != PhaseLobby {
		if _, ok := s.Players[presence.GetUserId()]; ok {
			return state, true, ""
		}
		return state, false, "match_in_progress"
	}

	// Capacity check (4 seats)
	if len(s.Players) >= 4 {
		return state, false, "match_full"
	}

	return state, true, ""
}

// MatchJoin mutates state when presences join and assigns seats/owner.
func (m *TienLenMatch) MatchJoin(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {

	s := state.(*MatchState)

	for _, p := range presences {
		uid := p.GetUserId()

		// Rejoin updates presence
		if existing, ok := s.Players[uid]; ok {
			existing.Presence = p
			continue
		}

		seat := lowestAvailableSeat(&s.Seats)
		s.Seats[seat] = uid

		isOwner := false
		if s.OwnerUserID == "" {
			s.OwnerUserID = uid
			isOwner = true
		}

		s.Players[uid] = &PlayerState{
			UserID:   uid,
			Presence: p,
			Seat:     seat + 1, // 1..4 externally
			IsOwner:  isOwner,
		}

		evt, _ := json.Marshal(map[string]any{
			"user_id": uid,
			"seat":    seat + 1,
			"owner":   isOwner,
		})
		_ = dispatcher.BroadcastMessage(OpPlayerJoined, evt, nil, nil, true) // :contentReference[oaicite:15]{index=15}
	}

	_ = dispatcher.MatchLabelUpdate(buildLabel(s)) // :contentReference[oaicite:16]{index=16}
	return state
}

// MatchLeave mutates state when presences leave, freeing seats and reassigning owner.
func (m *TienLenMatch) MatchLeave(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {

	s := state.(*MatchState)

	for _, p := range presences {
		uid := p.GetUserId()

		if pl, ok := s.Players[uid]; ok {
			// free seat
			s.Seats[pl.Seat-1] = ""
			delete(s.Players, uid)

			evt, _ := json.Marshal(map[string]any{"user_id": uid})
			_ = dispatcher.BroadcastMessage(OpPlayerLeft, evt, nil, nil, true) // :contentReference[oaicite:17]{index=17}
		}

		// Owner reassignment
		if s.OwnerUserID == uid && len(s.Players) > 0 {
			// pick first remaining (prototype; you can randomize)
			for other := range s.Players {
				s.OwnerUserID = other
				s.Players[other].IsOwner = true
				break
			}
		}
	}

	_ = dispatcher.MatchLabelUpdate(buildLabel(s)) // :contentReference[oaicite:18]{index=18}
	return state
}

// MatchLoop processes in-match messages for game flow.
func (m *TienLenMatch) MatchLoop(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher, tick int64, state interface{}, messages []runtime.MatchData) interface{} {

	s := state.(*MatchState)

	for _, msg := range messages {
		switch msg.GetOpCode() {
		case OpStartGame:
			handleStartGame(logger, dispatcher, s, msg)

		case OpPlayCards:
			handlePlayCards(logger, dispatcher, s, msg)

		case OpPassTurn:
			handlePass(logger, dispatcher, s, msg)

		case OpRequestNewGame:
			handleRequestNewGame(logger, dispatcher, s, msg)
		}
	}

	return state
}

// MatchTerminate runs on match shutdown; no-op for now.
func (m *TienLenMatch) MatchTerminate(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher, tick int64, state interface{}, graceSeconds int) interface{} {
	return state
}

// MatchSignal handles out-of-band signals; unused in this prototype.
func (m *TienLenMatch) MatchSignal(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher, tick int64, state interface{}, data string) (interface{}, string) {
	return state, ""
}

/* ---- helpers (prototype) ---- */

func lowestAvailableSeat(seats *[4]string) int {
	for i := 0; i < 4; i++ {
		if seats[i] == "" {
			return i
		}
	}
	return 0
}

func buildLabel(s *MatchState) string {
	open := s.Phase == PhaseLobby && len(s.Players) < 4
	b, _ := json.Marshal(Label{Open: open, Game: "tienlen", Phase: string(s.Phase)})
	return string(b)
}

/* ---- message handlers (prototype) ---- */

func handleStartGame(logger runtime.Logger, dispatcher runtime.MatchDispatcher, s *MatchState, msg runtime.MatchData) {
	if s.Phase != PhaseLobby {
		return
	}
	uid := msg.GetUserId()
	if uid != s.OwnerUserID {
		return
	}

	if len(s.Players) < 2 {
		return // you allow starting with 2+
	}

	// Deal 13 cards each (prototype shuffle)
	deck := newDeck()
	shuffle(deck)

	i := 0
	for _, pl := range s.Players {
		pl.Hand = append([]Card{}, deck[i:i+13]...)
		pl.HasPassed = false
		pl.Finished = false
		i += 13

		// send hand privately
		private, _ := json.Marshal(map[string]any{"hand": pl.Hand})
		_ = dispatcher.BroadcastMessage(OpHandDealt, private, []runtime.Presence{pl.Presence}, nil, true) // :contentReference[oaicite:19]{index=19}
	}

	s.Phase = PhasePlaying
	s.FinishOrder = nil

	evt, _ := json.Marshal(map[string]any{"phase": "playing"})
	_ = dispatcher.BroadcastMessage(OpGameStarted, evt, nil, nil, true) // :contentReference[oaicite:20]{index=20}
	_ = dispatcher.MatchLabelUpdate(buildLabel(s))                // :contentReference[oaicite:21]{index=21}
}

func handlePlayCards(logger runtime.Logger, dispatcher runtime.MatchDispatcher, s *MatchState, msg runtime.MatchData) {
	if s.Phase != PhasePlaying {
		return
	}
	uid := msg.GetUserId()
	pl := s.Players[uid]
	if pl == nil || pl.Finished {
		return
	}

	// TODO: enforce turn seat + validate combination beats board + validate card ownership.
	// Prototype: accept payload as list of cards and remove from hand.
	var payload struct {
		Cards []Card `json:"cards"`
	}
	_ = json.Unmarshal(msg.GetData(), &payload)
	if len(payload.Cards) == 0 {
		return
	}

	pl.Hand = removeCards(pl.Hand, payload.Cards)
	if len(pl.Hand) == 0 && !pl.Finished {
		pl.Finished = true
		s.FinishOrder = append(s.FinishOrder, uid)
	}

	evt, _ := json.Marshal(map[string]any{"user_id": uid, "cards": payload.Cards})
	_ = dispatcher.BroadcastMessage(OpCardPlayed, evt, nil, nil, true) // :contentReference[oaicite:22]{index=22}

	// End condition: game ends when only ONE player remains with cards (second-to-last emptied). (Your rule)
	if countPlayersWithCards(s) <= 1 {
		s.Phase = PhaseEnded
		end, _ := json.Marshal(map[string]any{"finish_order": s.FinishOrder})
		_ = dispatcher.BroadcastMessage(OpGameEnded, end, nil, nil, true) // :contentReference[oaicite:23]{index=23}
		_ = dispatcher.MatchLabelUpdate(buildLabel(s))              // :contentReference[oaicite:24]{index=24}
	}
}

func handlePass(logger runtime.Logger, dispatcher runtime.MatchDispatcher, s *MatchState, msg runtime.MatchData) {
	if s.Phase != PhasePlaying {
		return
	}
	uid := msg.GetUserId()
	pl := s.Players[uid]
	if pl == nil || pl.Finished {
		return
	}

	pl.HasPassed = true
	evt, _ := json.Marshal(map[string]any{"user_id": uid})
	_ = dispatcher.BroadcastMessage(OpTurnPassed, evt, nil, nil, true) // :contentReference[oaicite:25]{index=25}

	// TODO: implement round end detection:
	// A round ends when all *other active (not finished)* players have passed.
	// Then clear pass flags and let last player who played lead again.
}

func handleRequestNewGame(logger runtime.Logger, dispatcher runtime.MatchDispatcher, s *MatchState, msg runtime.MatchData) {
	if s.Phase != PhaseEnded {
		return
	}
	if msg.GetUserId() != s.OwnerUserID {
		return
	}

	// Reset back to lobby (prototype).
	for _, pl := range s.Players {
		pl.Hand = nil
		pl.HasPassed = false
		pl.Finished = false
	}
	s.Phase = PhaseLobby
	s.FinishOrder = nil

	_ = dispatcher.MatchLabelUpdate(buildLabel(s)) // :contentReference[oaicite:26]{index=26}
}

func newDeck() []Card {
	suits := []string{"S", "H", "D", "C"}
	var deck []Card
	for _, s := range suits {
		for r := 0; r <= 12; r++ {
			deck = append(deck, Card{Suit: s, Rank: r})
		}
	}
	return deck
}

func shuffle(deck []Card) {
	rng := rand.New(rand.NewSource(time.Now().UnixNano()))
	rng.Shuffle(len(deck), func(i, j int) { deck[i], deck[j] = deck[j], deck[i] })
}

func removeCards(hand []Card, played []Card) []Card {
	// Prototype: O(n*m) removal; replace with map-based removal later.
	out := append([]Card{}, hand...)
	for _, pc := range played {
		for i := 0; i < len(out); i++ {
			if out[i].Suit == pc.Suit && out[i].Rank == pc.Rank {
				out = append(out[:i], out[i+1:]...)
				break
			}
		}
	}
	return out
}

func countPlayersWithCards(s *MatchState) int {
	n := 0
	for _, pl := range s.Players {
		if !pl.Finished && len(pl.Hand) > 0 {
			n++
		}
	}
	return n
}
