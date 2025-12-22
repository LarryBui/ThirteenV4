import { createTestUser, TestUser } from "./helpers";
import * as protobuf from "protobufjs";
import * as path from "path";

const OP_CODE_PLAY_CARDS = 2;
const OP_CODE_PASS_TURN = 3;

const OP_CODE_PLAYER_JOINED = 50;
const OP_CODE_GAME_STARTED = 100;
const OP_CODE_CARD_PLAYED = 102;
const OP_CODE_TURN_PASSED = 103;
const OP_CODE_GAME_ERROR = 105; // Correct OpCode from proto

describe("Tien Len Invalid Move Scenarios", () => {
    let users: TestUser[] = [];
    let root: protobuf.Root;
    let PlayCardsRequest: protobuf.Type;
    let GameStartedEvent: protobuf.Type;
    let CardPlayedEvent: protobuf.Type;
    let MatchStateSnapshot: protobuf.Type;
    let GameErrorEvent: protobuf.Type;

    beforeAll(async () => {
        root = await protobuf.load(path.join(__dirname, "../../../proto/tienlen.proto"));
        PlayCardsRequest = root.lookupType("tienlen.v1.PlayCardsRequest");
        GameStartedEvent = root.lookupType("tienlen.v1.GameStartedEvent");
        CardPlayedEvent = root.lookupType("tienlen.v1.CardPlayedEvent");
        MatchStateSnapshot = root.lookupType("tienlen.v1.MatchStateSnapshot");
        GameErrorEvent = root.lookupType("tienlen.v1.GameErrorEvent");
    });

    afterEach(async () => {
        await Promise.all(users.map(u => u.close()));
        users = [];
    });

    // Helper to setup match with specific hands
    async function setupMatchWithHands(p0_cards: any[], p1_cards: any[], p2_cards: any[], p3_cards: any[]) {
        // 1. Setup 4 players
        for (let i = 0; i < 4; i++) {
            users.push(await createTestUser());
        }

        const owner = users[0];
        
        // Create match
        const rpcResult = await owner.client.rpc(owner.session, "test_create_match", {});
        const matchId = (rpcResult.payload as unknown as string).replace(/"/g, "");

        // Join Match
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

        // Start Game with Rigged Deck
        const payload = {
            match_id: matchId,
            hands: [
                { seat: 0, cards: p0_cards },
                { seat: 1, cards: p1_cards },
                { seat: 2, cards: p2_cards },
                { seat: 3, cards: p3_cards }
            ]
        };

        const gameStartPromises = users.map(u => u.waitForOpCode(OP_CODE_GAME_STARTED));
        await owner.client.rpc(owner.session, "test_start_game", payload);
        await Promise.all(gameStartPromises);

        return { matchId, seatToUser };
    }

    test("1. Play After Pass: P1 passes, then tries to play later in round (Should Fail)", async () => {
        // P0: 3 Spades
        const p0_cards = [{ rank: 0, suit: 0 }];
        // P1: 4 Spades, 5 Spades
        const p1_cards = [{ rank: 1, suit: 0 }, { rank: 2, suit: 0 }];
        // P2: 6 Spades
        const p2_cards = [{ rank: 3, suit: 0 }];
        // P3: 7 Spades
        const p3_cards = [{ rank: 4, suit: 0 }];

        const { matchId, seatToUser } = await setupMatchWithHands(p0_cards, p1_cards, p2_cards, p3_cards);
        const p0 = seatToUser.get(0)!;
        const p1 = seatToUser.get(1)!;
        const p2 = seatToUser.get(2)!;

        // P0 plays 3 Spades
        let playWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
        let playPayload = PlayCardsRequest.encode({ cards: [{ rank: 0, suit: 0 }] }).finish();
        await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
        await Promise.all(playWaiters);

        // P1 Passes
        let passWaiters = users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED));
        await p1.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
        await Promise.all(passWaiters);

        // P2 Plays 6 Spades
        playWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
        playPayload = PlayCardsRequest.encode({ cards: [{ rank: 3, suit: 0 }] }).finish();
        await p2.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
        await Promise.all(playWaiters);

        // P3 Passes
        passWaiters = users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED));
        await seatToUser.get(3)!.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
        await Promise.all(passWaiters);

        // P0 Passes
        passWaiters = users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED));
        await p0.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
        await Promise.all(passWaiters);

        // Current Turn should be P1 (but skipped because passed?), No, P1 passed, P2 is last owner.
        // Wait, standard rules: if P1 passes on a card, they are locked out of the round.
        // Current highest is P2. P3 passed. P0 passed.
        // If logic is correct, it should skip P1 and go to P2 (Round End) or wait for P1?
        // Actually, if P1 tries to play now, it should be rejected.

        // P1 tries to play 5 Spades
        const errorPromise = p1.waitForOpCode(OP_CODE_GAME_ERROR);
        playPayload = PlayCardsRequest.encode({ cards: [{ rank: 2, suit: 0 }] }).finish();
        await p1.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
        
        const errorEvent = await errorPromise;
        const error = GameErrorEvent.decode(errorEvent) as any;
        console.log(`Received Expected Error: ${error.message}`);
        expect(error.code).toBe(400); // Bad Request
    }, 60000);

    test("2. Chop After Pass: P1 passes on Pig, then tries to Chop later (Should Fail)", async () => {
         // P0: 3 Spades, 2 Hearts (Pig)
         const p0_cards = [{ rank: 0, suit: 0 }, { rank: 12, suit: 3 }];
         // P1: 3-Pine
         const p1_cards = [
             { rank: 0, suit: 1 }, { rank: 0, suit: 2 },
             { rank: 1, suit: 0 }, { rank: 1, suit: 1 },
             { rank: 2, suit: 0 }, { rank: 2, suit: 1 }
         ];
         // P2, P3 dummy
         const p2_cards = [{rank: 5, suit: 0}];
         const p3_cards = [{rank: 6, suit: 0}];
 
         const { matchId, seatToUser } = await setupMatchWithHands(p0_cards, p1_cards, p2_cards, p3_cards);
         const p0 = seatToUser.get(0)!;
         const p1 = seatToUser.get(1)!;
         const p2 = seatToUser.get(2)!;
 
         // P0 plays 3 Spades
         let playWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
         let playPayload = PlayCardsRequest.encode({ cards: [{ rank: 0, suit: 0 }] }).finish();
         await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
         await Promise.all(playWaiters);

         // Everyone passes -> New Round
         for(let i=1; i<=3; i++) {
            let passWaiters = users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED));
            await seatToUser.get(i)!.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
            await Promise.all(passWaiters);
         }

         // P0 Plays 2 Hearts (Pig)
         playWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
         playPayload = PlayCardsRequest.encode({ cards: [{ rank: 12, suit: 3 }] }).finish();
         await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
         await Promise.all(playWaiters);

         // P1 Passes (Crucial Step!)
         let passWaiters = users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED));
         await p1.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
         await Promise.all(passWaiters);

         // P2 Passes
         passWaiters = users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED));
         await p2.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
         await Promise.all(passWaiters);

         // Now P1 regrets and tries to Chop with 3-Pine
         const errorPromise = p1.waitForOpCode(OP_CODE_GAME_ERROR);
         playPayload = PlayCardsRequest.encode({ cards: p1_cards }).finish();
         await p1.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
         
         const errorEvent = await errorPromise;
         const error = GameErrorEvent.decode(errorEvent) as any;
         console.log(`Received Expected Error: ${error.message}`);
         expect(error.code).toBe(400);
    }, 60000);

    test("3. Out of Turn: P2 tries to play when it's P1's turn (Should Fail)", async () => {
         // P0: 3 Spades
         const p0_cards = [{ rank: 0, suit: 0 }];
         // P1: 4 Spades
         const p1_cards = [{ rank: 1, suit: 0 }];
         // P2: 5 Spades
         const p2_cards = [{ rank: 2, suit: 0 }];
         const p3_cards = [{ rank: 3, suit: 0 }];

         const { matchId, seatToUser } = await setupMatchWithHands(p0_cards, p1_cards, p2_cards, p3_cards);
         const p0 = seatToUser.get(0)!;
         const p2 = seatToUser.get(2)!;

         // P0 plays 3 Spades
         let playWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
         let playPayload = PlayCardsRequest.encode({ cards: [{ rank: 0, suit: 0 }] }).finish();
         await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
         await Promise.all(playWaiters);

         // Turn should be P1. P2 tries to play.
         const errorPromise = p2.waitForOpCode(OP_CODE_GAME_ERROR);
         playPayload = PlayCardsRequest.encode({ cards: [{ rank: 2, suit: 0 }] }).finish();
         await p2.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));

         const errorEvent = await errorPromise;
         const error = GameErrorEvent.decode(errorEvent) as any;
         console.log(`Received Expected Error: ${error.message}`);
         expect(error.code).toBe(400);
    }, 60000);

    test("4. Invalid Chop: 3-Pine tries to chop Pair of 2s (Should Fail)", async () => {
         // P0: 3 Spades, Pair 2s
         const p0_cards = [{ rank: 0, suit: 0 }, { rank: 12, suit: 0 }, { rank: 12, suit: 1 }];
         // P1: 3-Pine (Too weak for Pair 2s)
         const p1_cards = [
             { rank: 0, suit: 1 }, { rank: 0, suit: 2 },
             { rank: 1, suit: 0 }, { rank: 1, suit: 1 },
             { rank: 2, suit: 0 }, { rank: 2, suit: 1 }
         ];
         const p2_cards = [{rank: 5, suit: 0}];
         const p3_cards = [{rank: 6, suit: 0}];

         const { matchId, seatToUser } = await setupMatchWithHands(p0_cards, p1_cards, p2_cards, p3_cards);
         const p0 = seatToUser.get(0)!;
         const p1 = seatToUser.get(1)!;

         // P0 plays 3 Spades
         let playWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
         let playPayload = PlayCardsRequest.encode({ cards: [{ rank: 0, suit: 0 }] }).finish();
         await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
         await Promise.all(playWaiters);

         // Round Reset
         for(let i=1; i<=3; i++) {
             let passWaiters = users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED));
             await seatToUser.get(i)!.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
             await Promise.all(passWaiters);
         }

         // P0 plays Pair 2s
         playWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
         playPayload = PlayCardsRequest.encode({ cards: [{ rank: 12, suit: 0 }, { rank: 12, suit: 1 }] }).finish();
         await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
         await Promise.all(playWaiters);

         // P1 tries to Chop with 3-Pine
         const errorPromise = p1.waitForOpCode(OP_CODE_GAME_ERROR);
         playPayload = PlayCardsRequest.encode({ cards: p1_cards }).finish();
         await p1.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));

         const errorEvent = await errorPromise;
         const error = GameErrorEvent.decode(errorEvent) as any;
         console.log(`Received Expected Error: ${error.message}`);
         expect(error.code).toBe(400);
    }, 60000);
});
