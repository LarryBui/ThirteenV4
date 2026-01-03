package nakama

import (
	"context"
	"encoding/json"
	"fmt"
	"testing"

	"tienlen/internal/app"

	"github.com/form3tech-oss/jwt-go"
	"github.com/heroiclabs/nakama-common/runtime"
)

type vivoxTokenResponse struct {
	Token string `json:"token"`
}

func TestRpcGetVivoxToken_GeneratesValidClaims(t *testing.T) {
	t.Cleanup(func() { vivoxService = nil })

	vivoxService = app.NewVivoxService("test-secret", "issuer", "example.com")

	ctx := context.WithValue(context.Background(), runtime.RUNTIME_CTX_USER_ID, "user123")
	payload := `{"action":"login"}`

	// 1. Generate Token 1
	raw1, err := RpcGetVivoxToken(ctx, noopLogger{}, nil, nil, payload)
	if err != nil {
		t.Fatalf("RpcGetVivoxToken error: %v", err)
	}
	token1 := parseToken(t, raw1)

	// 2. Generate Token 2 (to check uniqueness)
	raw2, err := RpcGetVivoxToken(ctx, noopLogger{}, nil, nil, payload)
	if err != nil {
		t.Fatalf("RpcGetVivoxToken error: %v", err)
	}
	token2 := parseToken(t, raw2)

	// 3. Validate Claims
	claims1 := parseVivoxClaims(t, token1, "test-secret")
	claims2 := parseVivoxClaims(t, token2, "test-secret")

	// Standard Claims
	assertClaim(t, claims1, "iss", "issuer")
	assertClaim(t, claims1, "sub", "user123")
	assertClaim(t, claims1, "vxa", app.VivoxTokenActionLogin)
	assertClaim(t, claims1, "f", "sip:.issuer.user123.@example.com")
	
	// Check for forbidden/legacy claims
	if _, ok := claims1["from"]; ok {
		t.Errorf("token contains deprecated/invalid 'from' claim")
	}

	// Check VXI uniqueness (Nonce)
	vxi1, ok1 := claims1["vxi"]
	vxi2, ok2 := claims2["vxi"]
	if !ok1 || !ok2 {
		t.Fatal("vxi claim missing")
	}
	if vxi1 == vxi2 {
		t.Errorf("vxi claim must be unique per token. Got %v for both.", vxi1)
	}
}

func parseToken(t *testing.T, jsonRaw string) string {
	var resp vivoxTokenResponse
	if err := json.Unmarshal([]byte(jsonRaw), &resp); err != nil {
		t.Fatalf("unmarshal response: %v", err)
	}
	if resp.Token == "" {
		t.Fatal("expected token in response")
	}
	return resp.Token
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

func assertClaim(t *testing.T, claims jwt.MapClaims, key, expected string) {
	t.Helper()
	val, ok := claims[key]
	if !ok {
		t.Errorf("missing claim: %s", key)
		return
	}
	str, ok := val.(string)
	if !ok {
		t.Errorf("claim %s is not a string: %v", key, val)
		return
	}
	if str != expected {
		t.Errorf("claim %s = %s, want %s", key, str, expected)
	}
}
