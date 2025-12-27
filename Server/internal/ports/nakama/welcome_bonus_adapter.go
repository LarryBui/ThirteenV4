package nakama

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"time"

	"tienlen/internal/ports"

	"github.com/heroiclabs/nakama-common/runtime"
)

const (
	welcomeBonusCollection = "onboarding"
	welcomeBonusKey        = "welcome_bonus_v1"
)

// NakamaWelcomeBonusAdapter grants a welcome bonus using Nakama storage + wallet updates.
type NakamaWelcomeBonusAdapter struct {
	nk runtime.NakamaModule
}

// NewNakamaWelcomeBonusAdapter creates a new welcome bonus adapter.
func NewNakamaWelcomeBonusAdapter(nk runtime.NakamaModule) *NakamaWelcomeBonusAdapter {
	return &NakamaWelcomeBonusAdapter{nk: nk}
}

// GrantWelcomeBonusOnce grants a welcome bonus and records a marker atomically.
func (a *NakamaWelcomeBonusAdapter) GrantWelcomeBonusOnce(ctx context.Context, userID string, amount int64, metadata map[string]interface{}) (bool, error) {
	if userID == "" {
		return false, fmt.Errorf("userID is required")
	}
	if amount <= 0 {
		return false, fmt.Errorf("amount must be positive")
	}

	marker := map[string]interface{}{
		"amount":     amount,
		"granted_at": time.Now().UTC().Format(time.RFC3339),
	}
	value, err := json.Marshal(marker)
	if err != nil {
		return false, fmt.Errorf("failed to marshal welcome bonus marker: %w", err)
	}

	storageWrites := []*runtime.StorageWrite{
		{
			Collection:      welcomeBonusCollection,
			Key:             welcomeBonusKey,
			UserID:          userID,
			Value:           string(value),
			Version:         "*",
			PermissionRead:  runtime.STORAGE_PERMISSION_NO_READ,
			PermissionWrite: runtime.STORAGE_PERMISSION_NO_WRITE,
		},
	}

	walletUpdates := []*runtime.WalletUpdate{
		{
			UserID:    userID,
			Changeset: map[string]int64{"gold": amount},
			Metadata:  metadata,
		},
	}

	_, _, err = a.nk.MultiUpdate(ctx, nil, storageWrites, nil, walletUpdates, true)
	if err != nil {
		if errors.Is(err, runtime.ErrStorageRejectedVersion) {
			return false, nil
		}
		return false, fmt.Errorf("failed to grant welcome bonus: %w", err)
	}

	return true, nil
}

var _ ports.WelcomeBonusPort = (*NakamaWelcomeBonusAdapter)(nil)
