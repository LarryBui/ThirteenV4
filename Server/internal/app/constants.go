package app

// MinPlayersToStartGame defines the minimum number of occupied seats required to start a game.
// This is intentionally centralized so local testing can relax the rule (e.g., set to 1) without
// changing multiple call sites.
const MinPlayersToStartGame = 1

