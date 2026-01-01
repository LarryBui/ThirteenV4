package nakama

import (
	"context"
	"database/sql"
	"encoding/json"
	"fmt"
	"strings"
	"unicode"

	"tienlen/internal/app"
	"tienlen/internal/domain"
	pb "tienlen/proto"

	"github.com/heroiclabs/nakama-common/runtime"
)

var vivoxService *app.VivoxService

// RpcFindMatch searches for an available match with open seats.
// If an available match is found, it returns the Match ID.
// If no match is found, it creates a new match and returns its ID.
//
// Payload: JSON containing "type" (int32)
// Returns: String containing the Match ID.
func RpcFindMatch(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userId, _ := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
	const permissionDeniedCode = 7

	type findMatchReq struct {
		Type int32 `json:"type"`
	}
	var req findMatchReq
	if payload != "" {
		if err := json.Unmarshal([]byte(payload), &req); err != nil {
			logger.Error("RpcFindMatch [User:%s]: Failed to unmarshal payload: %v", userId, err)
			return "", err
		}
	}

	matchType := pb.MatchType(req.Type)
	if matchType == pb.MatchType_MATCH_TYPE_UNSPECIFIED {
		matchType = pb.MatchType_MATCH_TYPE_CASUAL
	}

	// 1. VIP Check
	if matchType == pb.MatchType_MATCH_TYPE_VIP {
		objects, err := nk.StorageRead(ctx, []*runtime.StorageRead{
			{
				Collection: "profiles",
				Key:        "vip_status",
				UserID:     userId,
			},
		})
		isVip := false
		if err == nil && len(objects) > 0 {
			var status struct {
				IsVip bool `json:"is_vip"`
			}
			if err := json.Unmarshal([]byte(objects[0].Value), &status); err == nil {
				isVip = status.IsVip
			}
		}

		if !isVip {
			type errorPayload struct {
				AppCode   int32 `json:"app_code"`
				Category  int32 `json:"category"`
				Retryable bool  `json:"retryable"`
			}

			payloadBytes, err := json.Marshal(errorPayload{
				AppCode:   int32(pb.ErrorCode_ERROR_CODE_MATCH_VIP_REQUIRED),
				Category:  int32(pb.ErrorCategory_ERROR_CATEGORY_ACCESS),
				Retryable: false,
			})
			if err != nil {
				payloadBytes = []byte("{}")
			}

			return "", runtime.NewError(string(payloadBytes), permissionDeniedCode)
		}
	}

	// 2. Search for matches with at least 1 open seat and matching type.
	limit := 1
	authoritative := true
	labelQuery := fmt.Sprintf("+label.%s:>=1 +label.%s:%d", MatchLabelKey_OpenSeats, MatchLabelKey_Type, int32(matchType))
	minSize := 0
	maxSize := 4

	matches, err := nk.MatchList(ctx, limit, authoritative, "", &minSize, &maxSize, labelQuery)
	if err != nil {
		logger.Error("RpcFindMatch [User:%s]: Failed to list matches: %v", userId, err)
		return "", err
	}

	// 3. If a match is found, return its ID.
	if len(matches) > 0 {
		matchId := matches[0].MatchId
		logger.Info("RpcFindMatch [User:%s]: Found existing match %s of type %d", userId, matchId, matchType)
		return fmt.Sprintf("%q", matchId), nil
	}

	// 4. If no match is found, create a new one.
	moduleName := MatchNameTienLen // Must match the name registered in InitModule
	params := map[string]interface{}{
		"type": int(matchType),
	}
	matchId, err := nk.MatchCreate(ctx, moduleName, params)
	if err != nil {
		logger.Error("RpcFindMatch [User:%s]: Failed to create match: %v", userId, err)
		return "", err
	}

	logger.Info("RpcFindMatch [User:%s]: Created new match %s of type %d", userId, matchId, matchType)
	return fmt.Sprintf("%q", matchId), nil
}

// RpcSetVip is for testing/dev to grant VIP status.
func RpcSetVip(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userId, _ := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)

	type setVipReq struct {
		IsVip bool `json:"is_vip"`
	}
	var req setVipReq
	if err := json.Unmarshal([]byte(payload), &req); err != nil {
		return "", err
	}

	value, _ := json.Marshal(map[string]bool{"is_vip": req.IsVip})
	_, err := nk.StorageWrite(ctx, []*runtime.StorageWrite{
		{
			Collection:      "profiles",
			Key:             "vip_status",
			UserID:          userId,
			Value:           string(value),
			PermissionRead:  2, // Owner Read
			PermissionWrite: 0, // No Write (Server only)
		},
	})
	if err != nil {
		return "", err
	}

	return "{}", nil
}

// RpcCreateMatchTest is for integration testing only. It always creates a fresh match.
func RpcCreateMatchTest(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	moduleName := MatchNameTienLen
	matchId, err := nk.MatchCreate(ctx, moduleName, nil)
	if err != nil {
		return "", err
	}

	return fmt.Sprintf("%q", matchId), nil
}

type riggedHand struct {
	Seat  int           `json:"seat"`
	Cards []domain.Card `json:"cards"`
}

// riggedHandPlan represents a rigged hand with fill behavior for missing cards.
type riggedHandPlan struct {
	Seat    int
	Cards   []domain.Card
	FillAll bool
}

// riggedHandText represents a raw card list string for a seat in a rigged deck request.
type riggedHandText struct {
	Seat  int    `json:"seat"`
	Cards string `json:"cards"`
}

// RpcStartGameTest is for integration testing only.
func RpcStartGameTest(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	type request struct {
		MatchId   string           `json:"match_id"`
		Hands     []riggedHand     `json:"hands"`
		HandsText []riggedHandText `json:"hands_text"`
	}
	var req request
	if err := json.Unmarshal([]byte(payload), &req); err != nil {
		logger.Error("RpcStartGameTest: Failed to unmarshal payload: %v", err)
		return "", err
	}

	var riggedPlans []riggedHandPlan
	defaultFillAll := true
	if len(req.HandsText) > 0 {
		parsedPlans, err := parseRiggedHandTexts(req.HandsText)
		if err != nil {
			logger.Error("RpcStartGameTest: Invalid rigged hand text: %v", err)
			return "", err
		}
		riggedPlans = parsedPlans
		defaultFillAll = false
	} else {
		riggedPlans = convertRiggedHands(req.Hands)
	}

	finalDeck, err := buildRiggedDeck(riggedPlans, defaultFillAll)
	if err != nil {
		logger.Error("RpcStartGameTest: Failed to build rigged deck: %v", err)
		return "", err
	}

	deckBytes, _ := json.Marshal(finalDeck)

	signalPayload := fmt.Sprintf(`{"op": "start_with_deck", "deck": %s}`, string(deckBytes))

	// Signal the match
	_, err = nk.MatchSignal(ctx, req.MatchId, signalPayload)
	if err != nil {
		logger.Error("RpcStartGameTest: Failed to signal match: %v", err)
		return "", err
	}

	logger.Info("RpcStartGameTest: Signaled match %s to start with rigged deck.", req.MatchId)
	return "{}", nil
}

// convertRiggedHands converts card-based rigged hands into plans with deterministic fill behavior.
func convertRiggedHands(hands []riggedHand) []riggedHandPlan {
	plans := make([]riggedHandPlan, 0, len(hands))
	for _, hand := range hands {
		plans = append(plans, riggedHandPlan{
			Seat:    hand.Seat,
			Cards:   hand.Cards,
			FillAll: true,
		})
	}
	return plans
}

// parseRiggedHandTexts converts raw card list text into rigged hand plans.
func parseRiggedHandTexts(handsText []riggedHandText) ([]riggedHandPlan, error) {
	seenSeats := make(map[int]bool)
	seenCards := make(map[domain.Card]bool)
	hands := make([]riggedHandPlan, 0, len(handsText))

	for _, handText := range handsText {
		if handText.Seat < 0 || handText.Seat > 3 {
			return nil, fmt.Errorf("seat %d is out of range", handText.Seat)
		}
		if seenSeats[handText.Seat] {
			return nil, fmt.Errorf("seat %d is listed more than once", handText.Seat)
		}
		seenSeats[handText.Seat] = true

		if strings.TrimSpace(handText.Cards) == "" {
			return nil, fmt.Errorf("seat %d has no card input", handText.Seat)
		}

		cards, fillAll, err := parseCardList(handText.Cards)
		if err != nil {
			return nil, fmt.Errorf("seat %d: %w", handText.Seat, err)
		}

		for _, card := range cards {
			if seenCards[card] {
				return nil, fmt.Errorf("duplicate card detected: %v", card)
			}
			seenCards[card] = true
		}

		hands = append(hands, riggedHandPlan{
			Seat:    handText.Seat,
			Cards:   cards,
			FillAll: fillAll,
		})
	}

	return hands, nil
}

// parseCardList parses a comma/space delimited list of cards, honoring ALL tokens.
func parseCardList(input string) ([]domain.Card, bool, error) {
	tokens := splitCardTokens(input)
	cards := make([]domain.Card, 0, len(tokens))
	sawAll := false

	for _, token := range tokens {
		if token == "" {
			continue
		}
		normalized := strings.ToUpper(strings.TrimSpace(token))
		if normalized == "ALL" {
			sawAll = true
			continue
		}

		card, err := parseCardToken(normalized)
		if err != nil {
			return nil, false, fmt.Errorf("invalid card token %q", token)
		}
		cards = append(cards, card)
	}

	if len(cards) == 0 && !sawAll {
		return nil, false, fmt.Errorf("no valid cards found")
	}

	return cards, sawAll, nil
}

// splitCardTokens splits a card list string on commas, semicolons, or whitespace.
func splitCardTokens(input string) []string {
	return strings.FieldsFunc(input, func(r rune) bool {
		return r == ',' || r == ';' || unicode.IsSpace(r)
	})
}

// parseCardToken parses a single card token like 3H or 10S.
func parseCardToken(token string) (domain.Card, error) {
	if len(token) < 2 {
		return domain.Card{}, fmt.Errorf("token too short")
	}

	rankText := token[:len(token)-1]
	suitText := token[len(token)-1:]

	rank, ok := parseCardRank(rankText)
	if !ok {
		return domain.Card{}, fmt.Errorf("invalid rank %q", rankText)
	}
	suit, ok := parseCardSuit(suitText)
	if !ok {
		return domain.Card{}, fmt.Errorf("invalid suit %q", suitText)
	}

	return domain.Card{Rank: rank, Suit: suit}, nil
}

// parseCardRank maps rank tokens to domain rank values.
func parseCardRank(rankText string) (int32, bool) {
	switch rankText {
	case "3":
		return 0, true
	case "4":
		return 1, true
	case "5":
		return 2, true
	case "6":
		return 3, true
	case "7":
		return 4, true
	case "8":
		return 5, true
	case "9":
		return 6, true
	case "10":
		return 7, true
	case "J":
		return 8, true
	case "Q":
		return 9, true
	case "K":
		return 10, true
	case "A":
		return 11, true
	case "2":
		return 12, true
	default:
		return 0, false
	}
}

// parseCardSuit maps suit tokens to domain suit values.
func parseCardSuit(suitText string) (int32, bool) {
	switch suitText {
	case "S":
		return 0, true
	case "C":
		return 1, true
	case "D":
		return 2, true
	case "H":
		return 3, true
	default:
		return 0, false
	}
}

// buildRiggedDeck builds a full 52-card deck honoring rigged plans.
func buildRiggedDeck(plans []riggedHandPlan, defaultFillAll bool) ([]domain.Card, error) {
	return buildRiggedDeckWithShuffler(plans, defaultFillAll, domain.ShuffleDeck)
}

func buildRiggedDeckWithShuffler(plans []riggedHandPlan, defaultFillAll bool, shuffle func([]domain.Card) []domain.Card) ([]domain.Card, error) {
	plansBySeat := make(map[int]riggedHandPlan, len(plans))
	usedCards := make(map[domain.Card]bool)

	for _, plan := range plans {
		if plan.Seat < 0 || plan.Seat > 3 {
			return nil, fmt.Errorf("seat %d is out of range", plan.Seat)
		}
		if len(plan.Cards) > 13 {
			return nil, fmt.Errorf("seat %d has too many cards", plan.Seat)
		}
		if _, exists := plansBySeat[plan.Seat]; exists {
			return nil, fmt.Errorf("seat %d is listed more than once", plan.Seat)
		}
		plansBySeat[plan.Seat] = plan

		for _, card := range plan.Cards {
			if usedCards[card] {
				return nil, fmt.Errorf("duplicate card detected: %v", card)
			}
			usedCards[card] = true
		}
	}

	fullDeck := domain.NewDeck()
	shuffledDeck := shuffle(fullDeck)
	orderedIdx := 0
	shuffledIdx := 0
	finalDeck := make([]domain.Card, 0, 52)

	for seat := 0; seat < 4; seat++ {
		plan, exists := plansBySeat[seat]
		if !exists {
			plan = riggedHandPlan{Seat: seat, FillAll: defaultFillAll}
		}

		finalDeck = append(finalDeck, plan.Cards...)

		needed := 13 - len(plan.Cards)
		for i := 0; i < needed; i++ {
			var (
				card domain.Card
				err  error
			)

			if plan.FillAll {
				card, err = nextUnusedCard(fullDeck, &orderedIdx, usedCards)
			} else {
				card, err = nextUnusedCard(shuffledDeck, &shuffledIdx, usedCards)
			}
			if err != nil {
				return nil, err
			}

			finalDeck = append(finalDeck, card)
			usedCards[card] = true
		}
	}

	return finalDeck, nil
}

func nextUnusedCard(deck []domain.Card, index *int, used map[domain.Card]bool) (domain.Card, error) {
	for *index < len(deck) {
		card := deck[*index]
		*index++
		if !used[card] {
			return card, nil
		}
	}
	return domain.Card{}, fmt.Errorf("not enough cards to fill rigged deck")
}

func RpcGetVivoxToken(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, ok := ctx.Value(runtime.RUNTIME_CTX_USER_ID).(string)
	if !ok {
		return "", fmt.Errorf("invalid context")
	}

	type request struct {
		Action  string `json:"action"`
		MatchID string `json:"match_id"`
	}
	var req request
	if err := json.Unmarshal([]byte(payload), &req); err != nil {
		return "", fmt.Errorf("failed to unmarshal payload: %w", err)
	}

	if vivoxService == nil {
		return "", fmt.Errorf("vivox service not initialized")
	}

	action := strings.ToLower(strings.TrimSpace(req.Action))
	if action == "" {
		action = app.VivoxTokenActionJoin
	}

	channelName := strings.TrimSpace(req.MatchID)
	if action == app.VivoxTokenActionJoin && channelName == "" {
		return "", fmt.Errorf("match_id is required for join tokens")
	}

	token, err := vivoxService.GenerateToken(userID, action, channelName)
	if err != nil {
		return "", fmt.Errorf("failed to generate vivox token: %w", err)
	}

	return fmt.Sprintf("{\"token\":\"%s\"}", token), nil
}
