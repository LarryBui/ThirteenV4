package nakama

import (
	"encoding/base64"
	"encoding/json"
	"strings"
	"testing"
)

func TestGenerateVivoxToken(t *testing.T) {
	key := "test-secret"
	issuer := "test-issuer"
	userURI := "sip:.test-issuer.user123.@tla.vivox.com"

	// Test Login Token
	token, err := generateVivoxToken(key, issuer, "login", userURI, "", 90)
	if err != nil {
		t.Fatalf("Failed to generate login token: %v", err)
	}

	parts := strings.Split(token, ".")
	if len(parts) != 3 {
		t.Errorf("Token should have 3 parts, got %d", len(parts))
	}

	// Verify Header
	headerBytes, _ := base64.RawURLEncoding.DecodeString(parts[0])
	if string(headerBytes) != "{}" {
		t.Errorf("Header should be {}, got %s", string(headerBytes))
	}

	// Verify Payload
	payloadBytes, _ := base64.RawURLEncoding.DecodeString(parts[1])
	var payload map[string]interface{}
	json.Unmarshal(payloadBytes, &payload)

	if payload["iss"] != issuer {
		t.Errorf("Expected issuer %s, got %s", issuer, payload["iss"])
	}
	if payload["vxa"] != "login" {
		t.Errorf("Expected action login, got %s", payload["vxa"])
	}
	if payload["f"] != userURI {
		t.Errorf("Expected from %s, got %s", userURI, payload["f"])
	}
	if _, ok := payload["t"]; ok {
		t.Error("Login token should not have 't' claim")
	}

	// Test Join Token
	channelURI := "sip:confctl-g-.test-issuer.channel1.@tla.vivox.com"
	tokenJoin, err := generateVivoxToken(key, issuer, "join", userURI, channelURI, 90)
	if err != nil {
		t.Fatalf("Failed to generate join token: %v", err)
	}

	partsJoin := strings.Split(tokenJoin, ".")
	payloadJoinBytes, _ := base64.RawURLEncoding.DecodeString(partsJoin[1])
	var payloadJoin map[string]interface{}
	json.Unmarshal(payloadJoinBytes, &payloadJoin)

	if payloadJoin["vxa"] != "join" {
		t.Errorf("Expected action join, got %s", payloadJoin["vxa"])
	}
	if payloadJoin["t"] != channelURI {
		t.Errorf("Expected to %s, got %s", channelURI, payloadJoin["t"])
	}
}
