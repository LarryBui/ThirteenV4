package app

import (
	"time"

	"github.com/form3tech-oss/jwt-go"
)

type VivoxService struct {
	vivoxSecret string
	vivoxIssuer string
	vivoxDomain string
}

func NewVivoxService(secret, issuer, domain string) *VivoxService {
	return &VivoxService{
		vivoxSecret: secret,
		vivoxIssuer: issuer,
		vivoxDomain: domain,
	}
}

func (s *VivoxService) GenerateToken(user, matchID string) (string, error) {
	claims := jwt.MapClaims{
		"iss":  s.vivoxIssuer,
		"sub":  user,
		"exp":  time.Now().Add(time.Hour * 1).Unix(),
		"vxa":  "join",
		"vxi":  1,
		"f":    "sip:." + s.vivoxIssuer + "." + user + ".@" + s.vivoxDomain,
		"t":    "sip:confctl-g-" + matchID + "@" + s.vivoxDomain,
		"from": user,
	}

	token := jwt.NewWithClaims(jwt.SigningMethodHS256, claims)
	return token.SignedString([]byte(s.vivoxSecret))
}
