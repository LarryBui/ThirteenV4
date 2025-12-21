package integration

import (
	"context"
	"fmt"
	"testing"
	"time"

	"github.com/heroiclabs/nakama-common/rtapi"
	"github.com/heroiclabs/nakama-go/v2"
)

const (
	ServerKey = "defaultkey"
	HttpKey   = "defaulthttpkey"
	Host      = "127.0.0.1"
	Port      = 7350
	RPCPort   = 7350
)

type TestClient struct {
	Client  *nakama.Client
	Session *nakama.Session
	Socket  *nakama.Socket
	UserID  string
}

func NewTestClient(t *testing.T) *TestClient {
	client := nakama.NewClient(ServerKey, Host, Port, false)
	
	// Create unique ID
	deviceID := fmt.Sprintf("test_device_%d", time.Now().UnixNano())
	
	// Authenticate
	session, err := client.AuthenticateDevice(context.Background(), deviceID, true, "")
	if err != nil {
		t.Fatalf("Failed to authenticate: %v", err)
	}

	// Create Socket
	socket := client.NewSocket()
	if err := socket.Connect(context.Background(), session, true); err != nil {
		t.Fatalf("Failed to connect socket: %v", err)
	}

	return &TestClient{
		Client:  client,
		Session: session,
		Socket:  socket,
		UserID:  session.UserId,
	}
}

func (tc *TestClient) Close() {
	if tc.Socket != nil {
		tc.Socket.Close()
	}
}

// JoinMatchViaRPC calls the 'find_match' RPC and joins the returned match ID.
func (tc *TestClient) FindAndJoinMatch(t *testing.T) string {
	payload := "{\"min_count\": 2}" // Simple payload
	rpc, err := tc.Client.RpcFunc(context.Background(), tc.Session, "find_match", payload)
	if err != nil {
		t.Fatalf("RPC find_match failed: %v", err)
	}

	// RPC returns match ID in payload string
	matchID := rpc.Payload
	if matchID == "" {
		t.Fatalf("RPC find_match returned empty ID")
	}

	// Join Match
	_, err = tc.Socket.JoinMatch(context.Background(), nil, matchID, nil)
	if err != nil {
		t.Fatalf("Failed to join match %s: %v", matchID, err)
	}

	return matchID
}

// WaitForEvent waits for a specific opcode from the socket.
func (tc *TestClient) WaitForMatchState(t *testing.T, opCode int64, timeout time.Duration) *rtapi.MatchData {
	ch := make(chan *rtapi.MatchData)
	
	// Hook into socket (This is simplistic; robust tests might need a better event bus)
	// nakama-go socket callbacks are set on the socket object.
	// We need to overwrite OnMatchData.
	
	originalHandler := tc.Socket.OnMatchData
	tc.Socket.OnMatchData = func(data *rtapi.MatchData) {
		if data.OpCode == opCode {
			ch <- data
		}
		if originalHandler != nil {
			originalHandler(data)
		}
	}

	select {
	case data := <-ch:
		return data
	case <-time.After(timeout):
		t.Fatalf("Timeout waiting for OpCode %d", opCode)
		return nil
	}
}
