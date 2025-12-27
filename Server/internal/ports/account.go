package ports

import "context"

// AccountPort defines the interface for updating account profiles.
type AccountPort interface {
	// UpdateProfile updates account profile fields for the given user.
	// userID identifies the account to update; username/displayName are applied as provided.
	// Returns an error if the profile update fails.
	UpdateProfile(ctx context.Context, userID, username, displayName string) error
}
