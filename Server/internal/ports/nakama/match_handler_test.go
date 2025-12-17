package nakama

import (
	"context"
	"testing"

	"github.com/heroiclabs/nakama-common/runtime"
	"google.golang.org/protobuf/proto"
	pb "tienlen/proto"
)

type fakeLogger struct{}

func (fakeLogger) Debug(format string, v ...interface{}) {}
func (fakeLogger) Info(format string, v ...interface{})  {}
func (fakeLogger) Warn(format string, v ...interface{})  {}
func (fakeLogger) Error(format string, v ...interface{}) {}
func (fakeLogger) WithField(key string, v interface{}) runtime.Logger {
	return fakeLogger{}
}
func (fakeLogger) WithFields(fields map[string]interface{}) runtime.Logger {
	return fakeLogger{}
}
func (fakeLogger) Fields() map[string]interface{} {
	return map[string]interface{}{}
}

type testPresence struct {
	userID string
}

func (p testPresence) GetHidden() bool                   { return false }
func (p testPresence) GetPersistence() bool              { return false }
func (p testPresence) GetUsername() string               { return "test-user" }
func (p testPresence) GetStatus() string                 { return "" }
func (p testPresence) GetReason() runtime.PresenceReason { return runtime.PresenceReasonUnknown }
func (p testPresence) GetUserId() string                 { return p.userID }
func (p testPresence) GetSessionId() string              { return "session" }
func (p testPresence) GetNodeId() string                 { return "node" }

type broadcastCall struct {
	opCode    int64
	data      []byte
	presences []runtime.Presence
	sender    runtime.Presence
	reliable  bool
}

type fakeDispatcher struct {
	broadcasts   []broadcastCall
	labelUpdates []string
}

func (d *fakeDispatcher) BroadcastMessage(opCode int64, data []byte, presences []runtime.Presence, sender runtime.Presence, reliable bool) error {
	d.broadcasts = append(d.broadcasts, broadcastCall{
		opCode:    opCode,
		data:      data,
		presences: presences,
		sender:    sender,
		reliable:  reliable,
	})
	return nil
}

func (d *fakeDispatcher) BroadcastMessageDeferred(opCode int64, data []byte, presences []runtime.Presence, sender runtime.Presence, reliable bool) error {
	return nil
}

func (d *fakeDispatcher) MatchKick(presences []runtime.Presence) error {
	return nil
}

func (d *fakeDispatcher) MatchLabelUpdate(label string) error {
	d.labelUpdates = append(d.labelUpdates, label)
	return nil
}

func TestMatchJoin_BroadcastsPlayerJoinedSnapshot(t *testing.T) {
	handler := &matchHandler{}
	dispatcher := &fakeDispatcher{}
	state := &MatchState{
		Tick:      123,
		Presences: make(map[string]runtime.Presence),
	}
	presence := testPresence{userID: "user-1"}

	got := handler.MatchJoin(context.Background(), fakeLogger{}, nil, nil, dispatcher, 0, state, []runtime.Presence{presence})
	matchState, ok := got.(*MatchState)
	if !ok {
		t.Fatalf("expected MatchState, got %T", got)
	}

	if matchState.Seats[0] != "user-1" {
		t.Fatalf("expected seat 0 to be user-1, got %q", matchState.Seats[0])
	}
	if matchState.OwnerID != "user-1" {
		t.Fatalf("expected owner to be user-1, got %q", matchState.OwnerID)
	}

	if len(dispatcher.broadcasts) != 1 {
		t.Fatalf("expected 1 broadcast, got %d", len(dispatcher.broadcasts))
	}

	call := dispatcher.broadcasts[0]
	if call.opCode != int64(pb.OpCode_OP_CODE_PLAYER_JOINED) {
		t.Fatalf("expected opcode %d, got %d", int64(pb.OpCode_OP_CODE_PLAYER_JOINED), call.opCode)
	}
	if call.presences != nil {
		t.Fatalf("expected broadcast to all presences, got %v", call.presences)
	}

	var payloadState pb.MatchStateSnapshot
	if err := proto.Unmarshal(call.data, &payloadState); err != nil {
		t.Fatalf("failed to unmarshal match state: %v", err)
	}

	if payloadState.OwnerId != "user-1" {
		t.Fatalf("expected payload owner user-1, got %q", payloadState.OwnerId)
	}
	if len(payloadState.Seats) != len(matchState.Seats) {
		t.Fatalf("expected %d seats, got %d", len(matchState.Seats), len(payloadState.Seats))
	}
	for i, seat := range matchState.Seats {
		if payloadState.Seats[i] != seat {
			t.Fatalf("expected seat %d to be %q, got %q", i, seat, payloadState.Seats[i])
		}
	}
	if payloadState.Tick != matchState.Tick {
		t.Fatalf("expected payload tick %d, got %d", matchState.Tick, payloadState.Tick)
	}
}
