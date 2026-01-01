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

func TestRpcGetVivoxToken_GeneratesLoginToken(t *testing.T) {
    t.Cleanup(func() { vivoxService = nil })

    vivoxService = app.NewVivoxService("test-secret", "issuer", "example.com")

    ctx := context.WithValue(context.Background(), runtime.RUNTIME_CTX_USER_ID, "user123")
    payload := `{"action":"login"}`

    raw, err := RpcGetVivoxToken(ctx, noopLogger{}, nil, nil, payload)
    if err != nil {
        t.Fatalf("RpcGetVivoxToken error: %v", err)
    }

    var resp vivoxTokenResponse
    if err := json.Unmarshal([]byte(raw), &resp); err != nil {
        t.Fatalf("unmarshal response: %v", err)
    }
    if resp.Token == "" {
        t.Fatal("expected token in response")
    }

    claims := parseVivoxClaims(t, resp.Token, "test-secret")

    if got := stringClaim(t, claims, "iss"); got != "issuer" {
        t.Fatalf("iss = %s, want issuer", got)
    }
    if got := stringClaim(t, claims, "vxa"); got != app.VivoxTokenActionLogin {
        t.Fatalf("vxa = %s, want %s", got, app.VivoxTokenActionLogin)
    }
    if got := stringClaim(t, claims, "sub"); got != "user123" {
        t.Fatalf("sub = %s, want user123", got)
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
