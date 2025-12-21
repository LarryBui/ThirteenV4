package integration

import (
	"context"
	"testing"
	"time"

	"google.golang.org/protobuf/proto"
	pb "tienlen/proto"
)

func TestFullGameStart(t *testing.T) {
	// 1. Create 4 Clients
	clients := make([]*TestClient, 4)
	for i := 0; i < 4; i++ {
		clients[i] = NewTestClient(t)
		defer clients[i].Close()
	}
	t.Log("Created 4 clients")

	// 2. Client 0 creates a match (via find_match RPC which creates if none found)
	// We use the helper which handles finding/creating.
	matchID := clients[0].FindAndJoinMatch(t)
	t.Logf("Client 0 created/joined match: %s", matchID)

	// 3. Other clients join the SAME match
	for i := 1; i < 4; i++ {
		_, err := clients[i].Socket.JoinMatch(context.Background(), nil, matchID, nil)
		if err != nil {
			t.Fatalf("Client %d failed to join match: %v", i, err)
		}
		t.Logf("Client %d joined match", i)
	}

	// Wait a bit for presences to sync
	time.Sleep(1 * time.Second)

	// 4. Client 0 (Owner) sends StartGame
	// OpCode 1 is START_GAME
	startReq := &pb.StartGameRequest{}
	bytes, _ := proto.Marshal(startReq)

	t.Log("Client 0 sending StartGame...")
	_, err := clients[0].Socket.SendMatchState(context.Background(), matchID, 1, bytes, nil)
	if err != nil {
		t.Fatalf("Failed to send StartGame: %v", err)
	}

	// 5. Assert: All clients receive GameStarted event (OpCode 101)
	// OpCode 101 = OP_CODE_GAME_STARTED (from proto)
	const OpCodeGameStarted = 101

	// We'll use a WaitGroup logic or just check sequentially since they all should get it.
	// For simplicity, let's wait for Client 0 to get it first.
	
	for i, c := range clients {
		t.Logf("Waiting for GameStarted on Client %d...", i)
		data := c.WaitForMatchState(t, OpCodeGameStarted, 5*time.Second)
		
		var event pb.GameStartedEvent
		if err := proto.Unmarshal(data.Data, &event); err != nil {
			t.Errorf("Client %d failed to unmarshal GameStarted: %v", i, err)
			continue
		}
		
		if len(event.Hand) != 13 {
			t.Errorf("Client %d expected 13 cards, got %d", i, len(event.Hand))
		}
		t.Logf("Client %d received Hand: %v", i, event.Hand)
	}
	
	t.Log("TestPassed: Game started successfully with 4 players.")
}
