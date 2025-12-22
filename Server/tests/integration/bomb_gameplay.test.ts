import { createTestUser, TestUser } from "./helpers";
import * as protobuf from "protobufjs";
import * as path from "path";

const OP_CODE_PLAY_CARDS = 2;
const OP_CODE_PASS_TURN = 3;

const OP_CODE_PLAYER_JOINED = 50;
const OP_CODE_GAME_STARTED = 100;
const OP_CODE_CARD_PLAYED = 102;
const OP_CODE_TURN_PASSED = 103;

describe("Tien Len Bomb Scenario Integration", () => {
    let users: TestUser[] = [];
    let root: protobuf.Root;
    let PlayCardsRequest: protobuf.Type;
    let GameStartedEvent: protobuf.Type;
    let CardPlayedEvent: protobuf.Type;
    let MatchStateSnapshot: protobuf.Type;

    beforeAll(async () => {
        root = await protobuf.load(path.join(__dirname, "../../../proto/tienlen.proto"));
        PlayCardsRequest = root.lookupType("tienlen.v1.PlayCardsRequest");
        GameStartedEvent = root.lookupType("tienlen.v1.GameStartedEvent");
        CardPlayedEvent = root.lookupType("tienlen.v1.CardPlayedEvent");
        MatchStateSnapshot = root.lookupType("tienlen.v1.MatchStateSnapshot");
    });

    afterEach(async () => {
        await Promise.all(users.map(u => u.close()));
        users = [];
    });

    test("Bomb Logic: 3-Pine chops 2 Hearts", async () => {
        // 1. Setup 4 players
        for (let i = 0; i < 4; i++) {
            users.push(await createTestUser());
        }

        const owner = users[0];
        
        // Create match
        const rpcResult = await owner.client.rpc(owner.session, "test_create_match", {});
        const matchId = (rpcResult.payload as unknown as string).replace(/"/g, ""); // Remove quotes if present

        console.log(`Joining match ${matchId}...`);
        
        const snapshotPromise = owner.waitForOpCode(OP_CODE_PLAYER_JOINED, 10000, (data) => {
            const snap = MatchStateSnapshot.decode(data) as any;
            return snap.players.length === 4;
        });
        
        await Promise.all(users.map(u => u.socket.joinMatch(matchId)));
        
        const snapshotData = await snapshotPromise;
        const snapshot = MatchStateSnapshot.decode(snapshotData) as any;
        
        const seatToUser = new Map<number, TestUser>();
        snapshot.players.forEach((p: any) => {
            const user = users.find(u => u.userId === p.userId);
            if (user) seatToUser.set(p.seat, user);
        });
        console.log(`Mapped 4 users to seats.`);

        // 2. Define Rigged Hands (Unique Cards)
        // P0 (Seat 0): 3 Spades (0,0), 2 Hearts (12,3)
        const p0_cards = [
            { rank: 0, suit: 0 },
            { rank: 12, suit: 3 }
        ];
        
        // P1 (Seat 1): 3-Pine
        // 3C, 3D (Rank 0; Suit 1, 2)
        // 4S, 4C (Rank 1; Suit 0, 1)
        // 5S, 5C (Rank 2; Suit 0, 1)
        const p1_cards = [
            { rank: 0, suit: 1 }, { rank: 0, suit: 2 },
            { rank: 1, suit: 0 }, { rank: 1, suit: 1 },
            { rank: 2, suit: 0 }, { rank: 2, suit: 1 }
        ];

        // 3. Start Game with Rigged Deck
        console.log("Starting game with rigged deck...");
        const payload = {
            match_id: matchId,
            hands: [
                { seat: 0, cards: p0_cards },
                { seat: 1, cards: p1_cards }
            ]
        };
        await owner.client.rpc(owner.session, "test_start_game", JSON.stringify(payload));

        // Wait for GameStarted
        await Promise.all(users.map(u => u.waitForOpCode(OP_CODE_GAME_STARTED)));
        console.log("Game Started.");

        // 4. P0 plays 3 Spades (Lowest)
        const p0 = seatToUser.get(0)!;
        const p1 = seatToUser.get(1)!;
        const p2 = seatToUser.get(2)!;
        const p3 = seatToUser.get(3)!;

        console.log("Turn 1: P0 plays 3 Spades");
        let playPayload = PlayCardsRequest.encode({ cards: [{ rank: 0, suit: 0 }] }).finish();
        await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
        await Promise.all(users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED)));

        // 5. Others Pass
        console.log("Turn 2: P1 Passes");
        await p1.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
        await Promise.all(users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED)));

        console.log("Turn 3: P2 Passes");
        await p2.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
        await Promise.all(users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED)));

        console.log("Turn 4: P3 Passes");
        await p3.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
        await Promise.all(users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED))); // New Round!

        // 6. P0 plays 2 Hearts
        console.log("Turn 5: P0 plays 2 Hearts (Pig)");
        playPayload = PlayCardsRequest.encode({ cards: [{ rank: 12, suit: 3 }] }).finish();
        await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
        const pigEvent = await p0.waitForOpCode(OP_CODE_CARD_PLAYED);
        const nextTurn = CardPlayedEvent.decode(pigEvent).toJSON().nextTurnSeat;
        
        if (nextTurn !== 1) throw new Error(`Expected P1 to have turn, got ${nextTurn}`);

        // 7. P1 plays 3-Pine (Chop!)
        console.log("Turn 6: P1 plays 3-Pine (Chop)");
        playPayload = PlayCardsRequest.encode({ cards: p1_cards }).finish();
        await p1.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
        await Promise.all(users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED)));

        console.log("Bomb logic verified!");

    }, 60000);
});
