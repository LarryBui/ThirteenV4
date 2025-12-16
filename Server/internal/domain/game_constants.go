package domain

const (
	OpCodeStartGame      int64 = 1
	OpCodeMatchStarted   int64 = 100 // Server -> Client
	OpCodePlayCards      int64 = 2
	OpCodePassTurn       int64 = 3
	OpCodeRequestNewGame int64 = 4
)
