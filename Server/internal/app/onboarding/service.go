package onboarding

import (
	"context"
	"fmt"
	"math/rand"
	"time"

	"tienlen/internal/ports"
)

const (
	defaultWelcomeBonusGold = 10000
)

// Result captures non-fatal onboarding outcomes.
type Result struct {
	// ProfileUpdateErr is set when the profile update failed but onboarding continued.
	ProfileUpdateErr error
}

// Service handles post-auth onboarding for new users.
type Service struct {
	accounts ports.AccountPort
	economy  ports.EconomyPort
	rng      *rand.Rand
}

// NewService constructs an onboarding service with required ports.
// accounts/economy must be non-nil; rng may be nil to use a time-seeded default.
func NewService(accounts ports.AccountPort, economy ports.EconomyPort, rng *rand.Rand) *Service {
	if rng == nil {
		rng = rand.New(rand.NewSource(time.Now().UnixNano()))
	}
	return &Service{
		accounts: accounts,
		economy:  economy,
		rng:      rng,
	}
}

// OnboardNewUser initializes profile and wallet for a newly created account.
// userID identifies the new account to initialize.
// Returns a Result with any non-fatal issues and an error if the welcome bonus cannot be granted.
// Side effects: updates account profile and grants a wallet bonus.
func (s *Service) OnboardNewUser(ctx context.Context, userID string) (Result, error) {
	if s.accounts == nil || s.economy == nil {
		return Result{}, fmt.Errorf("onboarding service not configured")
	}

	result := Result{}
	displayName := s.generateFriendlyName()
	if err := s.accounts.UpdateProfile(ctx, userID, displayName, displayName); err != nil {
		// Profile updates are best-effort; wallet grants are more important.
		result.ProfileUpdateErr = err
	}

	updates := []ports.WalletUpdate{
		{
			UserID: userID,
			Amount: defaultWelcomeBonusGold,
			Metadata: map[string]interface{}{
				"reason": "welcome_bonus",
			},
		},
	}

	if err := s.economy.UpdateBalances(ctx, updates); err != nil {
		return result, fmt.Errorf("failed to grant welcome bonus: %w", err)
	}

	return result, nil
}

func (s *Service) generateFriendlyName() string {
	adjectives := []string{"Happy", "Shiny", "Brave", "Clever", "Swift", "Calm", "Mighty", "Witty", "Sly", "Wild"}
	nouns := []string{"Panda", "Tiger", "Eagle", "Dolphin", "Wolf", "Otter", "Falcon", "Bear", "Fox", "Lion"}

	adj := adjectives[s.rng.Intn(len(adjectives))]
	noun := nouns[s.rng.Intn(len(nouns))]
	num := s.rng.Intn(9000) + 1000

	return fmt.Sprintf("%s%s%d", adj, noun, num)
}
