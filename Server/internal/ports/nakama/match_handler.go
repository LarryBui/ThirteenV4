package nakama

import (
	"context"
	"database/sql"
	"encoding/json"

	"tienlen/internal/app"
	"tienlen/internal/domain"
	pb "tienlen/proto"

	"github.com/heroiclabs/nakama-common/runtime"
	"google.golang.org/protobuf/proto"
)

type matchHandler struct {
	svc       *app.Service
	state     *domain.MatchState
	presences map[string]runtime.Presence
}

func newMatchHandler() *matchHandler {
	return &matchHandler{
		svc:       app.NewService(nil),
		state:     &domain.MatchState{Phase: domain.PhaseLobby, Players: map[string]*domain.Player{}},
		presences: make(map[string]runtime.Presence),
	}
}

func (m *matchHandler) MatchInit(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, params map[string]interface{}) (interface{}, int, string) {
	labelBytes, _ := json.Marshal(domain.ComputeLabel(m.state))
	return m, 10, string(labelBytes) // tickrate must be 1..60
}

func (m *matchHandler) MatchJoinAttempt(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presence runtime.Presence, metadata map[string]string) (interface{}, bool, string) {

	// Allow rejoin; disallow new joins once playing.
	if m.state.Phase != domain.PhaseLobby {
		if _, ok := m.state.Players[presence.GetUserId()]; ok {
			return m, true, ""
		}
		return m, false, "match_in_progress"
	}

	// Capacity check (4 seats)
	if len(m.state.Players) >= 4 {
		return m, false, "match_full"
	}

	return m, true, ""
}

func (m *matchHandler) MatchJoin(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {

	for _, p := range presences {
		uid := p.GetUserId()
		m.presences[uid] = p

		events, err := m.svc.Join(m.state, uid)
		if err != nil {
			logger.Error("join error: %v", err)
			continue
		}
		m.dispatchEvents(logger, dispatcher, events)
	}

	m.updateLabel(dispatcher)
	return m
}

func (m *matchHandler) MatchLeave(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {

	for _, p := range presences {
		uid := p.GetUserId()
		delete(m.presences, uid)

		events, err := m.svc.Leave(m.state, uid)
		if err != nil {
			logger.Error("leave error: %v", err)
			continue
		}
		m.dispatchEvents(logger, dispatcher, events)
	}

	m.updateLabel(dispatcher)
	return m
}

func (m *matchHandler) MatchLoop(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher, tick int64, state interface{}, messages []runtime.MatchData) interface{} {

	for _, msg := range messages {
		switch msg.GetOpCode() {
		case OpStartGame:
			m.handleStartGame(logger, dispatcher, msg)
		case OpPlayCards:
			m.handlePlayCards(logger, dispatcher, msg)
		case OpPassTurn:
			m.handlePass(logger, dispatcher, msg)
		case OpRequestNewGame:
			m.handleRequestNewGame(logger, dispatcher, msg)
		}
	}

	return m
}

func (m *matchHandler) MatchTerminate(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher, tick int64, state interface{}, graceSeconds int) interface{} {
	return m
}

func (m *matchHandler) MatchSignal(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule,
	dispatcher runtime.MatchDispatcher, tick int64, state interface{}, data string) (interface{}, string) {
	return m, ""
}

/* ---- message handlers ---- */

func (m *matchHandler) handleStartGame(logger runtime.Logger, dispatcher runtime.MatchDispatcher, msg runtime.MatchData) {
	events, err := m.svc.StartGame(m.state, msg.GetUserId())
	if err != nil {
		logger.Error("start game error: %v", err)
		return
	}
	m.dispatchEvents(logger, dispatcher, events)
	m.updateLabel(dispatcher)
}

func (m *matchHandler) handlePlayCards(logger runtime.Logger, dispatcher runtime.MatchDispatcher, msg runtime.MatchData) {
	var payload pb.PlayCardsRequest
	if err := proto.Unmarshal(msg.GetData(), &payload); err != nil {
		logger.Error("unmarshal PlayCardsRequest: %v", err)
		return
	}

	events, err := m.svc.PlayCards(m.state, msg.GetUserId(), cardsFromProto(payload.Cards))
	if err != nil {
		logger.Error("play cards error: %v", err)
		return
	}
	m.dispatchEvents(logger, dispatcher, events)
	if m.state.Phase == domain.PhaseEnded {
		m.updateLabel(dispatcher)
	}
}

func (m *matchHandler) handlePass(logger runtime.Logger, dispatcher runtime.MatchDispatcher, msg runtime.MatchData) {
	var payload pb.PassTurnRequest
	if len(msg.GetData()) > 0 {
		if err := proto.Unmarshal(msg.GetData(), &payload); err != nil {
			logger.Error("unmarshal PassTurnRequest: %v", err)
			return
		}
	}

	events, err := m.svc.PassTurn(m.state, msg.GetUserId())
	if err != nil {
		logger.Error("pass turn error: %v", err)
		return
	}
	m.dispatchEvents(logger, dispatcher, events)
}

func (m *matchHandler) handleRequestNewGame(logger runtime.Logger, dispatcher runtime.MatchDispatcher, msg runtime.MatchData) {
	var payload pb.RequestNewGameRequest
	if len(msg.GetData()) > 0 {
		if err := proto.Unmarshal(msg.GetData(), &payload); err != nil {
			logger.Error("unmarshal RequestNewGameRequest: %v", err)
			return
		}
	}

	if err := m.svc.RequestNewGame(m.state, msg.GetUserId()); err != nil {
		logger.Error("request new game error: %v", err)
		return
	}
	m.updateLabel(dispatcher)
}

/* ---- dispatch helpers ---- */

func (m *matchHandler) dispatchEvents(logger runtime.Logger, dispatcher runtime.MatchDispatcher, events []app.Event) {
	for _, ev := range events {
		opcode, payload := m.eventToOpcodePayload(logger, ev)
		if opcode == 0 || payload == nil {
			continue
		}

		var targets []runtime.Presence
		if len(ev.Recipients) > 0 {
			for _, uid := range ev.Recipients {
				if p, ok := m.presences[uid]; ok {
					targets = append(targets, p)
				}
			}
		}

		_ = dispatcher.BroadcastMessage(opcode, payload, targets, nil, true)
	}
}

func (m *matchHandler) eventToOpcodePayload(logger runtime.Logger, ev app.Event) (int64, []byte) {
	switch ev.Kind {
	case app.EventPlayerJoined:
		payload := ev.Payload.(app.PlayerJoinedPayload)
		msg := &pb.PlayerJoinedEvent{UserId: payload.UserID, Seat: int32(payload.Seat), Owner: payload.Owner}
		return OpPlayerJoined, marshal(logger, msg)
	case app.EventPlayerLeft:
		payload := ev.Payload.(app.PlayerLeftPayload)
		msg := &pb.PlayerLeftEvent{UserId: payload.UserID}
		return OpPlayerLeft, marshal(logger, msg)
	case app.EventGameStarted:
		msg := &pb.GameStartedEvent{Phase: string(m.state.Phase)}
		return OpGameStarted, marshal(logger, msg)
	case app.EventHandDealt:
		payload := ev.Payload.(app.HandDealtPayload)
		msg := &pb.HandDealtEvent{Hand: cardsToProto(payload.Hand)}
		return OpHandDealt, marshal(logger, msg)
	case app.EventCardPlayed:
		payload := ev.Payload.(app.CardPlayedPayload)
		msg := &pb.CardPlayedEvent{UserId: payload.UserID, Cards: cardsToProto(payload.Cards)}
		return OpCardPlayed, marshal(logger, msg)
	case app.EventTurnPassed:
		payload := ev.Payload.(app.TurnPassedPayload)
		msg := &pb.TurnPassedEvent{UserId: payload.UserID}
		return OpTurnPassed, marshal(logger, msg)
	case app.EventGameEnded:
		payload := ev.Payload.(app.GameEndedPayload)
		msg := &pb.GameEndedEvent{FinishOrder: payload.FinishOrder}
		return OpGameEnded, marshal(logger, msg)
	default:
		return 0, nil
	}
}

func (m *matchHandler) updateLabel(dispatcher runtime.MatchDispatcher) {
	labelBytes, _ := json.Marshal(domain.ComputeLabel(m.state))
	_ = dispatcher.MatchLabelUpdate(string(labelBytes))
}

func marshal(logger runtime.Logger, msg proto.Message) []byte {
	b, err := proto.Marshal(msg)
	if err != nil {
		logger.Error("marshal event: %v", err)
		return nil
	}
	return b
}
