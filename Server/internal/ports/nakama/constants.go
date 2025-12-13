package nakama

const (
	// RpcQuickMatch is the Nakama RPC id clients call to find or create a lobby-capable match.
	RpcQuickMatch = "quick_match"

	// MatchNameTienLen is the authoritative match handler name registered with Nakama.
	MatchNameTienLen = "tienlen_match"
)

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
