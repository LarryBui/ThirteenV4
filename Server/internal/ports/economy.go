package ports

import "context"

// WalletUpdate represents a single currency change for a user.
type WalletUpdate struct {
	UserID   string
	Amount   int64
	Metadata map[string]interface{}
}

// EconomyPort defines the interface for managing game currency.
type EconomyPort interface {
	// GetBalance retrieves the current gold balance for a user.
	GetBalance(ctx context.Context, userID string) (int64, error)

	// UpdateBalances applies multiple wallet changes atomically.
	// This is used at the end of a game to settle all bets.
	UpdateBalances(ctx context.Context, updates []WalletUpdate) error
}
