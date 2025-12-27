package onboarding

import (
	"context"
	"errors"
	"math/rand"
	"testing"

	"tienlen/internal/ports"
)

type fakeAccountPort struct {
	updateErr error
}

func (f fakeAccountPort) UpdateProfile(ctx context.Context, userID, username, displayName string) error {
	return f.updateErr
}

type fakeEconomyPort struct {
	updateErr error
	updates   []ports.WalletUpdate
}

func (f *fakeEconomyPort) GetBalance(ctx context.Context, userID string) (int64, error) {
	return 0, nil
}

func (f *fakeEconomyPort) UpdateBalances(ctx context.Context, updates []ports.WalletUpdate) error {
	f.updates = updates
	return f.updateErr
}

func TestOnboardNewUser_GrantsWelcomeBonus(t *testing.T) {
	economy := &fakeEconomyPort{}
	service := NewService(fakeAccountPort{}, economy, rand.New(rand.NewSource(1)))

	result, err := service.OnboardNewUser(context.Background(), "user-1")
	if err != nil {
		t.Fatalf("OnboardNewUser returned error: %v", err)
	}
	if result.ProfileUpdateErr != nil {
		t.Fatalf("Expected no profile update error, got %v", result.ProfileUpdateErr)
	}

	if len(economy.updates) != 1 {
		t.Fatalf("Expected 1 wallet update, got %d", len(economy.updates))
	}
	if economy.updates[0].Amount != defaultWelcomeBonusGold {
		t.Fatalf("Expected welcome bonus %d, got %d", defaultWelcomeBonusGold, economy.updates[0].Amount)
	}
}

func TestOnboardNewUser_AccountUpdateFailureStillGrantsBonus(t *testing.T) {
	economy := &fakeEconomyPort{}
	service := NewService(fakeAccountPort{updateErr: errors.New("update failed")}, economy, rand.New(rand.NewSource(1)))

	result, err := service.OnboardNewUser(context.Background(), "user-1")
	if err != nil {
		t.Fatalf("OnboardNewUser returned error: %v", err)
	}
	if result.ProfileUpdateErr == nil {
		t.Fatal("Expected profile update error to be captured")
	}

	if len(economy.updates) != 1 {
		t.Fatalf("Expected 1 wallet update, got %d", len(economy.updates))
	}
}

func TestOnboardNewUser_WelcomeBonusFailureReturnsError(t *testing.T) {
	service := NewService(fakeAccountPort{}, &fakeEconomyPort{updateErr: errors.New("wallet failed")}, rand.New(rand.NewSource(1)))

	if _, err := service.OnboardNewUser(context.Background(), "user-1"); err == nil {
		t.Fatal("Expected error when welcome bonus fails")
	}
}
