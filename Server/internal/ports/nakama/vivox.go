package nakama

import (
	"context"
	"crypto/hmac"
	"crypto/sha256"
	"database/sql"
	"encoding/base64"
	"encoding/json"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

// GenerateVivoxToken generates a Vivox Access Token.
// Based on: https://docs.vivox.com/v5/general/unity/5_15_0/en-us/access-token-guide/generate-tokens-unity.htm
// Payload claims:
// iss: issuer
// exp: expiration time
// vxa: action (login, join)
// vxi: unique ID (we use time)
// f: from user URI
// t: to channel URI (optional, for join)
func generateVivoxToken(key string, issuer string, action string, userURI string, channelURI string, expirationSeconds int) (string, error) {
	header := map[string]interface{}{} // Empty header
	headerBytes, _ := json.Marshal(header)
	headerEncoded := base64.RawURLEncoding.EncodeToString(headerBytes) // Should be "e30"

	// Payload
	now := time.Now().Unix()
	exp := now + int64(expirationSeconds)
	vxi := now // Using timestamp as unique ID for simplicity, or a random number

	payload := map[string]interface{}{
		"iss": issuer,
		"exp": exp,
		"vxa": action,
		"vxi": vxi,
		"f":   userURI,
	}

	if channelURI != "" {
		payload["t"] = channelURI
	}

	payloadBytes, err := json.Marshal(payload)
	if err != nil {
		return "", err
	}
	payloadEncoded := base64.RawURLEncoding.EncodeToString(payloadBytes)

	// Signature
	toSign := headerEncoded + "." + payloadEncoded
	mac := hmac.New(sha256.New, []byte(key))
	mac.Write([]byte(toSign))
	signatureEncoded := base64.RawURLEncoding.EncodeToString(mac.Sum(nil))

	return toSign + "." + signatureEncoded, nil
}

// RpcGenerateVivoxToken handles the RPC call from the client to generate a Vivox token.
// Payload: {"action": "login" | "join", "userUri": "...", "channelUri": "..."}
func RpcGenerateVivoxToken(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	// userId, _ := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)

	var req struct {
		Action     string `json:"action"`
		UserURI    string `json:"userUri"`
		ChannelURI string `json:"channelUri"`
	}
	if err := json.Unmarshal([]byte(payload), &req); err != nil {
		return "", runtime.NewError("Invalid payload", 3) // INVALID_ARGUMENT
	}

	// Environment variables for Vivox credentials
	env := ctx.Value(runtime.RUNTIME_CTX_ENV).(map[string]string)
	issuer := env["vivox_issuer"]
	key := env["vivox_secret"]
	// domain := env["vivox_domain"] // Not needed if URIs are provided

	if issuer == "" || key == "" {
		issuer = "test-issuer"
		key = "test-secret"
		logger.Warn("Vivox credentials missing from env, using test defaults.")
	}

	// If URIs are provided by the client (e.g. from Vivox SDK), use them.
	// Otherwise we could construct them, but the SDK knows best what it wants signed.
	if req.UserURI == "" {
		return "", runtime.NewError("User URI required", 3)
	}

	if req.Action == "join" && req.ChannelURI == "" {
		return "", runtime.NewError("Channel URI required for join", 3)
	}

	token, err := generateVivoxToken(key, issuer, req.Action, req.UserURI, req.ChannelURI, 90)
	if err != nil {
		logger.Error("Failed to generate Vivox token: %v", err)
		return "", runtime.NewError("Internal error", 13) // INTERNAL
	}

	res := map[string]string{
		"token": token,
	}
	resBytes, _ := json.Marshal(res)
	return string(resBytes), nil
}
