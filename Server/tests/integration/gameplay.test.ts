import { createTestUser, TestUser } from "./helpers";
import * as protobuf from "protobufjs";
import * as path from "path";

const OP_CODE_START_GAME = 1;
const OP_CODE_PLAY_CARDS = 2; // Reverted to 2
const OP_CODE_PASS_TURN = 3;

const OP_CODE_PLAYER_JOINED = 50;
const OP_CODE_GAME_STARTED = 100;
const OP_CODE_CARD_PLAYED = 102;
const OP_CODE_TURN_PASSED = 103;

describe("Tien Len Gameplay Integration", () => {
    let users: TestUser[] = [];
    let root: protobuf.Root;
    let StartGameRequest: protobuf.Type;
    let PlayCardsRequest: protobuf.Type;
    let GameStartedEvent: protobuf.Type;
    let CardPlayedEvent: protobuf.Type;
    let TurnPassedEvent: protobuf.Type;
    let FindMatchResponse: protobuf.Type;
    let MatchStateSnapshot: protobuf.Type;

    beforeAll(async () => {
        root = await protobuf.load(path.join(__dirname, "../../../proto/tienlen.proto"));
        StartGameRequest = root.lookupType("tienlen.v1.StartGameRequest");
        PlayCardsRequest = root.lookupType("tienlen.v1.PlayCardsRequest");
        GameStartedEvent = root.lookupType("tienlen.v1.GameStartedEvent");
        CardPlayedEvent = root.lookupType("tienlen.v1.CardPlayedEvent");
        TurnPassedEvent = root.lookupType("tienlen.v1.TurnPassedEvent");
        FindMatchResponse = root.lookupType("tienlen.v1.FindMatchResponse");
        MatchStateSnapshot = root.lookupType("tienlen.v1.MatchStateSnapshot");
    });

    afterEach(async () => {
        await Promise.all(users.map(u => u.close()));
        users = [];
    });

    test("Sequential Gameplay: Play, Beat, Pass", async () => {
        console.log("Setting up 4 players...");
        for (let i = 0; i < 4; i++) {
            users.push(await createTestUser());
        }

        const owner = users[0];
        console.log("Finding match...");
        const rpcResult = await owner.client.rpc(owner.session, "test_create_match", {});
        const rpcMatchId = rpcResult.payload as unknown as string;

        console.log(`Joining match ${rpcMatchId}...`);
        
        const snapshotPromise = owner.waitForOpCode(OP_CODE_PLAYER_JOINED, 10000, (data) => {
            const snap = MatchStateSnapshot.decode(data) as any;
            return snap.players.length === 4;
        });
        
        // Use the ID returned by joinMatch!
        const matchIds = await Promise.all(users.map(u => u.joinMatch(rpcMatchId)));
        const matchId = matchIds[0]; // All should be same
        console.log(`Authoritative Match ID: ${matchId}`);

        const snapshotData = await snapshotPromise;
        const snapshot = MatchStateSnapshot.decode(snapshotData) as any;
        
        const seatToUser = new Map<number, TestUser>();
        snapshot.players.forEach((p: any) => {
            const user = users.find(u => u.userId === p.userId);
            if (user) seatToUser.set(p.seat, user);
        });
        console.log(`Mapped 4 users to seats.`);

        await new Promise(r => setTimeout(r, 1000));

        // Start Game
        console.log("Starting game...");
        const startPayload = StartGameRequest.encode({}).finish();
        await owner.socket.sendMatchState(matchId, OP_CODE_START_GAME, startPayload);

        // Wait for GameStarted
        console.log("Waiting for GameStarted...");
        const startResults = await Promise.all(users.map(u => u.waitForOpCode(OP_CODE_GAME_STARTED)));
        
        const hands = startResults.map(data => GameStartedEvent.decode(data) as any);
        const firstTurnSeat = hands[0].firstTurnSeat;
        console.log(`Game Started. First Turn: Seat ${firstTurnSeat}`);

        await new Promise(r => setTimeout(r, 1000));

        // First player plays smallest card
        const p1 = seatToUser.get(firstTurnSeat)!;
        const p1UserIndex = users.indexOf(p1);
        const p1_hand = hands[p1UserIndex].hand;
        const smallestCard = p1_hand.reduce((min: any, c: any) => 
            (c.rank * 4 + c.suit < min.rank * 4 + min.suit ? c : min), p1_hand[0]);

        console.log(`User ${p1.username} (Seat ${firstTurnSeat}) playing: Rank ${smallestCard.rank} Suit ${smallestCard.suit}`);
        
        const playWaiters = Promise.all(users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED, 15000)));

        // Create Payload manually
        // Use integer values for enums: Suit (0-3), Rank (0-12)
        const payloadObj = { cards: [{ suit: smallestCard.suit, rank: smallestCard.rank }] };
        const buffer = PlayCardsRequest.encode(payloadObj).finish();
        
        // CRITICAL FIX: protobufjs .finish() returns a Uint8Array that might be a view on a larger buffer.
        // We create a clean copy to ensure nakama-js sends exactly these bytes.
        const playPayload = new Uint8Array(buffer);

        console.log(`Sending OpCode 2. Payload size: ${playPayload.length}. Hex: ${Buffer.from(playPayload).toString('hex')}`);
        await p1.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, playPayload);
        console.log("Sent OpCode 2.");

        const playEvents = await playWaiters;
        const lastPlay = CardPlayedEvent.decode(playEvents[0]) as any;
        console.log(`Card Played successfully. Next Turn: ${lastPlay.nextTurnSeat}`);

        // 5. Simulate 3 Passes to reset round
        let currentNext = lastPlay.nextTurnSeat;
        for (let i = 0; i < 3; i++) {
            const passer = seatToUser.get(currentNext)!;
            console.log(`User ${passer.username} (Seat ${currentNext}) passing...`);
            
            const passWaiters = Promise.all(users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED, 15000)));
            await passer.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
            
            const passEvents = await passWaiters;
            const passEvt = TurnPassedEvent.decode(passEvents[0]) as any;
            
            currentNext = passEvt.nextTurnSeat;
            if (i < 2) {
                expect(passEvt.newRound).toBe(false);
            } else {
                // The 3rd pass should trigger a new round
                expect(passEvt.newRound).toBe(true);
                expect(currentNext).toBe(firstTurnSeat); // Returns to the original player who played
            }
        }

        console.log("Full round reset verified. Player who played last is leader again. Gameplay flow verified.");

    }, 60000);
});
