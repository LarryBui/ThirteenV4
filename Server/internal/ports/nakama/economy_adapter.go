package nakama

import (
	"context"
	"encoding/json"
	"fmt"
	"tienlen/internal/ports"

	"github.com/heroiclabs/nakama-common/runtime"
)

// NakamaEconomyAdapter implements ports.EconomyPort using Nakama's wallet system.
type NakamaEconomyAdapter struct {
	nk runtime.NakamaModule
}

// NewNakamaEconomyAdapter creates a new economy adapter.
func NewNakamaEconomyAdapter(nk runtime.NakamaModule) *NakamaEconomyAdapter {
	return &NakamaEconomyAdapter{
		nk: nk,
	}
}

// GetBalance retrieves the current gold balance for a user.
func (a *NakamaEconomyAdapter) GetBalance(ctx context.Context, userID string) (int64, error) {
	account, err := a.nk.AccountGetId(ctx, userID)
	if err != nil {
		return 0, fmt.Errorf("failed to get account: %w", err)
	}

	var wallet map[string]int64
	if err := json.Unmarshal([]byte(account.Wallet), &wallet); err != nil {
		return 0, fmt.Errorf("failed to unmarshal wallet: %w", err)
	}

	return wallet["gold"], nil
}

// UpdateBalances applies multiple wallet changes.
func (a *NakamaEconomyAdapter) UpdateBalances(ctx context.Context, updates []ports.WalletUpdate) error {
	for _, update := range updates {
		if update.Amount == 0 {
			continue
		}

		changes := map[string]int64{
			"gold": update.Amount,
		}

		_, _, err := a.nk.WalletUpdate(ctx, update.UserID, changes, update.Metadata, true)
		if err != nil {
			return fmt.Errorf("failed to update wallet for user %s: %w", update.UserID, err)
		}
	}
	return nil
}