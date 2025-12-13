package main

// Op codes for client messages and server events.
const (
	// Client -> Server
	OpStartGame      int64 = 1
	OpPlayCards      int64 = 2
	OpPassTurn       int64 = 3
	OpRequestNewGame int64 = 4

	// Server -> Client events
	OpPlayerJoined int64 = 101
	OpPlayerLeft   int64 = 102
	OpGameStarted  int64 = 103
	OpHandDealt    int64 = 104 // send privately
	OpCardPlayed   int64 = 105
	OpTurnPassed   int64 = 106
	OpGameEnded    int64 = 107
)
