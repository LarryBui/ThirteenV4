package ports

import "context"

// WelcomeBonusPort grants the welcome bonus at most once per user.
type WelcomeBonusPort interface {
	// GrantWelcomeBonusOnce attempts to grant a one-time welcome bonus.
	// Returns granted=false when the bonus was already granted.
	GrantWelcomeBonusOnce(ctx context.Context, userID string, amount int64, metadata map[string]interface{}) (bool, error)
}
