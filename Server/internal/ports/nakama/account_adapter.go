package nakama

import (
	"context"

	"tienlen/internal/ports"

	"github.com/heroiclabs/nakama-common/runtime"
)

// NakamaAccountAdapter implements ports.AccountPort using Nakama's account API.
type NakamaAccountAdapter struct {
	nk runtime.NakamaModule
}

// NewNakamaAccountAdapter creates a new account adapter.
func NewNakamaAccountAdapter(nk runtime.NakamaModule) *NakamaAccountAdapter {
	return &NakamaAccountAdapter{nk: nk}
}

// UpdateProfile updates the account username and display name in Nakama.
// userID identifies the account to update; username/displayName are applied as provided.
// Returns an error if the Nakama update fails.
func (a *NakamaAccountAdapter) UpdateProfile(ctx context.Context, userID, username, displayName string) error {
	return a.nk.AccountUpdateId(ctx, userID, username, nil, displayName, "", "", "", "")
}

var _ ports.AccountPort = (*NakamaAccountAdapter)(nil)
