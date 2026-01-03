package app

import (
	"fmt"
	"math/rand"
	"time"

	"github.com/form3tech-oss/jwt-go"
)

type VivoxService struct {
	vivoxSecret string
	vivoxIssuer string
	vivoxDomain string
}

const (
	VivoxTokenActionLogin = "login"
	VivoxTokenActionJoin  = "join"
)

func NewVivoxService(secret, issuer, domain string) *VivoxService {
	return &VivoxService{
		vivoxSecret: secret,
		vivoxIssuer: issuer,
		vivoxDomain: domain,
	}
}

func (s *VivoxService) GenerateToken(user, action, channelName string) (string, error) {
	if s == nil {
		return "", fmt.Errorf("vivox service is nil")
	}
	if user == "" {
		return "", fmt.Errorf("user is required")
	}
	if s.vivoxSecret == "" || s.vivoxIssuer == "" || s.vivoxDomain == "" {
		return "", fmt.Errorf("vivox config is incomplete")
	}

	userURI := s.userURI(user)
	targetURI, err := s.targetURI(action, channelName, userURI)
	if err != nil {
		return "", err
	}

	claims := jwt.MapClaims{
		"iss": s.vivoxIssuer,
		"sub": user,
		"exp": time.Now().Add(time.Hour * 1).Unix(),
		"vxa": action,
		"vxi": fmt.Sprintf("%d-%d", time.Now().UnixNano(), rand.Int63()),
		"f":   userURI,
		"t":   targetURI,
	}

	token := jwt.NewWithClaims(jwt.SigningMethodHS256, claims)
	return token.SignedString([]byte(s.vivoxSecret))
}

func (s *VivoxService) userURI(user string) string {
	return "sip:." + s.vivoxIssuer + "." + user + ".@" + s.vivoxDomain
}

func (s *VivoxService) channelURI(channelName string) string {
	return "sip:confctl-g-" + channelName + "@" + s.vivoxDomain
}

func (s *VivoxService) targetURI(action, channelName, userURI string) (string, error) {
	switch action {
	case VivoxTokenActionLogin:
		return userURI, nil
	case VivoxTokenActionJoin:
		if channelName == "" {
			return "", fmt.Errorf("channel name is required for join tokens")
		}
		return s.channelURI(channelName), nil
	default:
		return "", fmt.Errorf("unsupported vivox action: %s", action)
	}
}
