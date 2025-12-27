package onboarding

import (
	"context"
	"errors"
	"math/rand"
	"testing"
)

type fakeAccountPort struct {
	updateErr error
}

func (f fakeAccountPort) UpdateProfile(ctx context.Context, userID, username, displayName string) error {
	return f.updateErr
}

type fakeWelcomeBonusPort struct {
	updateErr error
	updates   []welcomeBonusCall
	granted   bool
}

type welcomeBonusCall struct {
	userID   string
	amount   int64
	metadata map[string]interface{}
}

func (f *fakeWelcomeBonusPort) GrantWelcomeBonusOnce(ctx context.Context, userID string, amount int64, metadata map[string]interface{}) (bool, error) {
	f.updates = append(f.updates, welcomeBonusCall{
		userID:   userID,
		amount:   amount,
		metadata: metadata,
	})
	if f.updateErr != nil {
		return false, f.updateErr
	}
	return f.granted, nil
}

func TestOnboardNewUser_GrantsWelcomeBonus(t *testing.T) {
	bonuses := &fakeWelcomeBonusPort{granted: true}
	service := NewService(fakeAccountPort{}, bonuses, rand.New(rand.NewSource(1)))

	result, err := service.OnboardNewUser(context.Background(), "user-1")
	if err != nil {
		t.Fatalf("OnboardNewUser returned error: %v", err)
	}
	if result.ProfileUpdateErr != nil {
		t.Fatalf("Expected no profile update error, got %v", result.ProfileUpdateErr)
	}

	if len(bonuses.updates) != 1 {
		t.Fatalf("Expected 1 welcome bonus call, got %d", len(bonuses.updates))
	}
	if bonuses.updates[0].amount != defaultWelcomeBonusGold {
		t.Fatalf("Expected welcome bonus %d, got %d", defaultWelcomeBonusGold, bonuses.updates[0].amount)
	}
	if !result.WelcomeBonusGranted {
		t.Fatal("Expected welcome bonus to be marked as granted")
	}
}

func TestOnboardNewUser_AccountUpdateFailureStillGrantsBonus(t *testing.T) {
	bonuses := &fakeWelcomeBonusPort{granted: true}
	service := NewService(fakeAccountPort{updateErr: errors.New("update failed")}, bonuses, rand.New(rand.NewSource(1)))

	result, err := service.OnboardNewUser(context.Background(), "user-1")
	if err != nil {
		t.Fatalf("OnboardNewUser returned error: %v", err)
	}
	if result.ProfileUpdateErr == nil {
		t.Fatal("Expected profile update error to be captured")
	}

	if len(bonuses.updates) != 1 {
		t.Fatalf("Expected 1 welcome bonus call, got %d", len(bonuses.updates))
	}
	if !result.WelcomeBonusGranted {
		t.Fatal("Expected welcome bonus to be marked as granted")
	}
}

func TestOnboardNewUser_WelcomeBonusFailureReturnsError(t *testing.T) {
	service := NewService(fakeAccountPort{}, &fakeWelcomeBonusPort{updateErr: errors.New("wallet failed")}, rand.New(rand.NewSource(1)))

	if _, err := service.OnboardNewUser(context.Background(), "user-1"); err == nil {
		t.Fatal("Expected error when welcome bonus fails")
	}
}

func TestOnboardNewUser_WelcomeBonusAlreadyGranted(t *testing.T) {
	bonuses := &fakeWelcomeBonusPort{granted: false}
	service := NewService(fakeAccountPort{}, bonuses, rand.New(rand.NewSource(1)))

	result, err := service.OnboardNewUser(context.Background(), "user-1")
	if err != nil {
		t.Fatalf("OnboardNewUser returned error: %v", err)
	}
	if result.WelcomeBonusGranted {
		t.Fatal("Expected welcome bonus to be marked as already granted")
	}
}
