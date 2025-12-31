package app

import (
	"fmt"
	"testing"

	"github.com/form3tech-oss/jwt-go"
)

func TestVivoxServiceGenerateLoginToken(t *testing.T) {
	secret := "test-secret"
	issuer := "issuer"
	domain := "example.com"
	user := "user123"

	svc := NewVivoxService(secret, issuer, domain)
	tokenString, err := svc.GenerateToken(user, VivoxTokenActionLogin, "")
	if err != nil {
		t.Fatalf("generate login token error: %v", err)
	}

	claims := parseVivoxClaims(t, tokenString, secret)
	userURI := fmt.Sprintf("sip:.%s.%s.@%s", issuer, user, domain)

	if got := stringClaim(t, claims, "vxa"); got != VivoxTokenActionLogin {
		t.Fatalf("vxa = %s, want %s", got, VivoxTokenActionLogin)
	}
	if got := stringClaim(t, claims, "f"); got != userURI {
		t.Fatalf("f = %s, want %s", got, userURI)
	}
	if got := stringClaim(t, claims, "t"); got != userURI {
		t.Fatalf("t = %s, want %s", got, userURI)
	}
	if got := stringClaim(t, claims, "sub"); got != user {
		t.Fatalf("sub = %s, want %s", got, user)
	}
	if got := stringClaim(t, claims, "from"); got != user {
		t.Fatalf("from = %s, want %s", got, user)
	}
}

func TestVivoxServiceGenerateJoinToken(t *testing.T) {
	secret := "test-secret"
	issuer := "issuer"
	domain := "example.com"
	user := "user123"
	channel := "match-456"

	svc := NewVivoxService(secret, issuer, domain)
	tokenString, err := svc.GenerateToken(user, VivoxTokenActionJoin, channel)
	if err != nil {
		t.Fatalf("generate join token error: %v", err)
	}

	claims := parseVivoxClaims(t, tokenString, secret)
	userURI := fmt.Sprintf("sip:.%s.%s.@%s", issuer, user, domain)
	channelURI := fmt.Sprintf("sip:confctl-g-%s@%s", channel, domain)

	if got := stringClaim(t, claims, "vxa"); got != VivoxTokenActionJoin {
		t.Fatalf("vxa = %s, want %s", got, VivoxTokenActionJoin)
	}
	if got := stringClaim(t, claims, "f"); got != userURI {
		t.Fatalf("f = %s, want %s", got, userURI)
	}
	if got := stringClaim(t, claims, "t"); got != channelURI {
		t.Fatalf("t = %s, want %s", got, channelURI)
	}
}

func TestVivoxServiceGenerateTokenRejectsUnknownAction(t *testing.T) {
	svc := NewVivoxService("secret", "issuer", "example.com")
	if _, err := svc.GenerateToken("user", "unknown", ""); err == nil {
		t.Fatal("expected error for unsupported action")
	}
}

func TestVivoxServiceGenerateJoinTokenRequiresChannel(t *testing.T) {
	svc := NewVivoxService("secret", "issuer", "example.com")
	if _, err := svc.GenerateToken("user", VivoxTokenActionJoin, ""); err == nil {
		t.Fatal("expected error for empty channel name")
	}
}

func TestVivoxServiceGenerateTokenRequiresConfig(t *testing.T) {
	svc := NewVivoxService("", "issuer", "example.com")
	if _, err := svc.GenerateToken("user", VivoxTokenActionLogin, ""); err == nil {
		t.Fatal("expected error for missing vivox config")
	}
}

func parseVivoxClaims(t *testing.T, tokenString, secret string) jwt.MapClaims {
	t.Helper()

	token, err := jwt.Parse(tokenString, func(token *jwt.Token) (interface{}, error) {
		if token.Method != jwt.SigningMethodHS256 {
			return nil, fmt.Errorf("unexpected signing method: %v", token.Header["alg"])
		}
		return []byte(secret), nil
	})
	if err != nil {
		t.Fatalf("parse token error: %v", err)
	}
	if !token.Valid {
		t.Fatal("token is invalid")
	}

	claims, ok := token.Claims.(jwt.MapClaims)
	if !ok {
		t.Fatal("claims are not map claims")
	}
	return claims
}

func stringClaim(t *testing.T, claims jwt.MapClaims, name string) string {
	t.Helper()
	value, ok := claims[name]
	if !ok {
		t.Fatalf("missing %s claim", name)
	}
	str, ok := value.(string)
	if !ok {
		t.Fatalf("%s claim is not a string", name)
	}
	return str
}
