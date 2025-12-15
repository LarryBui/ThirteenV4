package nakama

import (
	"context"
	"database/sql"

	"github.com/heroiclabs/nakama-common/api"
	"github.com/heroiclabs/nakama-common/runtime"
	"google.golang.org/protobuf/types/known/wrapperspb"
	"github.com/google/uuid"
)

// BeforeAuthenticateDevice intercepts device authentication to force creation of a new user every time.
func BeforeAuthenticateDevice(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, in *api.AuthenticateDeviceRequest) (*api.AuthenticateDeviceRequest, error) {
	// Generate a new random UUID for the device ID
	newDeviceID := uuid.New().String()

	logger.Info("Intercepting Device Auth. Replacing original Device ID '%s' with new random ID '%s' to force new user creation.", in.Account.Id, newDeviceID)

	// Replace the Device ID with the new random one
	in.Account.Id = newDeviceID

	// Force 'Create' to true so the account is created
	in.Create = &wrapperspb.BoolValue{Value: true}

	return in, nil
}
