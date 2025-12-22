import { createTestUser, TestUser } from "./helpers";
import * as protobuf from "protobufjs";
import * as path from "path";

const OP_CODE_PLAY_CARDS = 2;
const OP_CODE_PASS_TURN = 3;

const OP_CODE_PLAYER_JOINED = 50;
const OP_CODE_GAME_STARTED = 100;
const OP_CODE_CARD_PLAYED = 102;
const OP_CODE_TURN_PASSED = 103;
const OP_CODE_MATCH_STATE_SNAPSHOT = 105;

describe("Tien Len Pig Chopping Scenarios", () => {
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

    // Helper to setup match with specific hands
    async function setupMatchWithHands(p0_cards: any[], p1_cards: any[]) {
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
                { seat: 1, cards: p1_cards }
            ]
        };

        const gameStartPromises = users.map(u => u.waitForOpCode(OP_CODE_GAME_STARTED));
        await owner.client.rpc(owner.session, "test_start_game", payload);
        await Promise.all(gameStartPromises);

        return { matchId, seatToUser };
    }

    test("1. 3-Pine chops Single 2", async () => {
        // P0: 3 Spades (0,0), 2 Hearts (12,3)
        const p0_cards = [{ rank: 0, suit: 0 }, { rank: 12, suit: 3 }];
        // P1: 3-Pine (3,4,5)
        const p1_cards = [
            { rank: 0, suit: 1 }, { rank: 0, suit: 2 }, // 3s
            { rank: 1, suit: 0 }, { rank: 1, suit: 1 }, // 4s
            { rank: 2, suit: 0 }, { rank: 2, suit: 1 }  // 5s
        ];

        const { matchId, seatToUser } = await setupMatchWithHands(p0_cards, p1_cards);
        const p0 = seatToUser.get(0)!;
        const p1 = seatToUser.get(1)!;

        // P0 plays 3 Spades to start
        let playWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
        let playPayload = PlayCardsRequest.encode({ cards: [{ rank: 0, suit: 0 }] }).finish();
        await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
        await Promise.all(playWaiters);

        // P1 Passes
        let passWaiters = users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED));
        await p1.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
        await Promise.all(passWaiters);

        // Others Pass -> New Round
        for(let i=2; i<=3; i++) {
             let loopPassWaiters = users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED));
             await seatToUser.get(i)!.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
             await Promise.all(loopPassWaiters);
        }

        // P0 plays 2 Hearts (Pig)
        let pigWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
        playPayload = PlayCardsRequest.encode({ cards: [{ rank: 12, suit: 3 }] }).finish();
        await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
        await Promise.all(pigWaiters);

        // P1 Chops with 3-Pine
        let chopWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
        playPayload = PlayCardsRequest.encode({ cards: p1_cards }).finish();
        await p1.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
        
        const events = await Promise.all(chopWaiters);
        const lastPlay = CardPlayedEvent.decode(events[0]) as any;
        
        // P1 should successfully play
        expect(lastPlay.seat).toBe(1);
        expect(lastPlay.cards.length).toBe(6);
        console.log("3-Pine successfully chopped Single 2");
    }, 60000);

    test("2. 4-Pine chops Pair 2", async () => {
        // P0: 3 Spades (0,0), Pair 2 (12,0; 12,1)
        const p0_cards = [{ rank: 0, suit: 0 }, { rank: 12, suit: 0 }, { rank: 12, suit: 1 }];
        // P1: 4-Pine (3,4,5,6)
        const p1_cards = [
            { rank: 0, suit: 1 }, { rank: 0, suit: 2 }, // 3s
            { rank: 1, suit: 0 }, { rank: 1, suit: 1 }, // 4s
            { rank: 2, suit: 0 }, { rank: 2, suit: 1 }, // 5s
            { rank: 3, suit: 0 }, { rank: 3, suit: 1 }  // 6s
        ];

        const { matchId, seatToUser } = await setupMatchWithHands(p0_cards, p1_cards);
        const p0 = seatToUser.get(0)!;
        const p1 = seatToUser.get(1)!;

        // P0 plays 3 Spades
        let playWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
        let playPayload = PlayCardsRequest.encode({ cards: [{ rank: 0, suit: 0 }] }).finish();
        await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
        await Promise.all(playWaiters);

        // Force new round (P1, P2, P3 Pass)
        for(let i=1; i<=3; i++) {
            let passWaiters = users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED));
            await seatToUser.get(i)!.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
            await Promise.all(passWaiters);
        }

        // P0 plays Pair 2s
        let pigWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
        playPayload = PlayCardsRequest.encode({ cards: [{ rank: 12, suit: 0 }, { rank: 12, suit: 1 }] }).finish();
        await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
        await Promise.all(pigWaiters);

        // P1 Chops with 4-Pine
        let chopWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
        playPayload = PlayCardsRequest.encode({ cards: p1_cards }).finish();
        await p1.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
        
        const events = await Promise.all(chopWaiters);
        const lastPlay = CardPlayedEvent.decode(events[0]) as any;
        expect(lastPlay.seat).toBe(1);
        expect(lastPlay.cards.length).toBe(8);
        console.log("4-Pine successfully chopped Pair 2");
    }, 60000);

    test("3. Quad chops Single 2", async () => {
        // P0: 3 Spades (0,0), 2 Hearts (12,3)
        const p0_cards = [{ rank: 0, suit: 0 }, { rank: 12, suit: 3 }];
        // P1: Quad 5s
        const p1_cards = [
            { rank: 2, suit: 0 }, { rank: 2, suit: 1 }, { rank: 2, suit: 2 }, { rank: 2, suit: 3 },
            { rank: 0, suit: 1 } // Extra card to play first
        ];

        const { matchId, seatToUser } = await setupMatchWithHands(p0_cards, p1_cards);
        const p0 = seatToUser.get(0)!;
        const p1 = seatToUser.get(1)!;

        // P0 plays 3 Spades
        let playWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
        let playPayload = PlayCardsRequest.encode({ cards: [{ rank: 0, suit: 0 }] }).finish();
        await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
        await Promise.all(playWaiters);

        // Force new round
        for(let i=1; i<=3; i++) {
            let passWaiters = users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED));
            await seatToUser.get(i)!.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
            await Promise.all(passWaiters);
        }

        // P0 plays 2 Hearts
        let pigWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
        playPayload = PlayCardsRequest.encode({ cards: [{ rank: 12, suit: 3 }] }).finish();
        await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
        await Promise.all(pigWaiters);

        // P1 Chops with Quad 5s
        const quad = p1_cards.slice(0, 4);
        let chopWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
        playPayload = PlayCardsRequest.encode({ cards: quad }).finish();
        await p1.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
        
        const events = await Promise.all(chopWaiters);
        const lastPlay = CardPlayedEvent.decode(events[0]) as any;
        expect(lastPlay.seat).toBe(1);
        expect(lastPlay.cards.length).toBe(4);
        console.log("Quad successfully chopped Single 2");
    }, 60000);

    test("4. Quad chops Pair 2 (Valid in this rule set)", async () => {
         // P0: 3 Spades (0,0), Pair 2 (12,0; 12,1)
         const p0_cards = [{ rank: 0, suit: 0 }, { rank: 12, suit: 0 }, { rank: 12, suit: 1 }];
         // P1: Quad Aces (11)
         const p1_cards = [
             { rank: 11, suit: 0 }, { rank: 11, suit: 1 }, { rank: 11, suit: 2 }, { rank: 11, suit: 3 },
             { rank: 0, suit: 1 } 
         ];
 
         const { matchId, seatToUser } = await setupMatchWithHands(p0_cards, p1_cards);
         const p0 = seatToUser.get(0)!;
         const p1 = seatToUser.get(1)!;
 
         // P0 plays 3 Spades
         let playWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
         let playPayload = PlayCardsRequest.encode({ cards: [{ rank: 0, suit: 0 }] }).finish();
         await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
         await Promise.all(playWaiters);
 
         // Force new round
         for(let i=1; i<=3; i++) {
             let passWaiters = users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED));
             await seatToUser.get(i)!.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
             await Promise.all(passWaiters);
         }
 
         // P0 plays Pair 2s
         let pigWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
         playPayload = PlayCardsRequest.encode({ cards: [{ rank: 12, suit: 0 }, { rank: 12, suit: 1 }] }).finish();
         await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
         await Promise.all(pigWaiters);
 
         // P1 Chops with Quad Aces
         const quad = p1_cards.slice(0, 4);
         let chopWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
         playPayload = PlayCardsRequest.encode({ cards: quad }).finish();
         await p1.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
         
         const events = await Promise.all(chopWaiters);
         const lastPlay = CardPlayedEvent.decode(events[0]) as any;
         expect(lastPlay.seat).toBe(1);
         expect(lastPlay.cards.length).toBe(4);
         console.log("Quad successfully chopped Pair 2");
    }, 60000);

    test("5. 4-Pine chops Quad", async () => {
         // P0: 3 Spades, Quad 5s
         const p0_cards = [
             { rank: 0, suit: 0 },
             { rank: 2, suit: 0 }, { rank: 2, suit: 1 }, { rank: 2, suit: 2 }, { rank: 2, suit: 3 }
         ];
         // P1: 4-Pine (6,7,8,9)
         const p1_cards = [
             { rank: 3, suit: 0 }, { rank: 3, suit: 1 }, 
             { rank: 4, suit: 0 }, { rank: 4, suit: 1 }, 
             { rank: 5, suit: 0 }, { rank: 5, suit: 1 },
             { rank: 6, suit: 0 }, { rank: 6, suit: 1 },
             { rank: 0, suit: 1 } 
         ];
 
         const { matchId, seatToUser } = await setupMatchWithHands(p0_cards, p1_cards);
         const p0 = seatToUser.get(0)!;
         const p1 = seatToUser.get(1)!;
 
         // P0 plays 3 Spades
         let playWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
         let playPayload = PlayCardsRequest.encode({ cards: [{ rank: 0, suit: 0 }] }).finish();
         await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
         await Promise.all(playWaiters);
 
         // Force new round
         for(let i=1; i<=3; i++) {
             let passWaiters = users.map(u => u.waitForOpCode(OP_CODE_TURN_PASSED));
             await seatToUser.get(i)!.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));
             await Promise.all(passWaiters);
         }
 
         // P0 plays Quad 5s
         const quad = p0_cards.slice(1);
         let pigWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
         playPayload = PlayCardsRequest.encode({ cards: quad }).finish();
         await p0.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
         await Promise.all(pigWaiters);
 
         // P1 Chops with 4-Pine
         const fourPine = p1_cards.slice(0, 8);
         let chopWaiters = users.map(u => u.waitForOpCode(OP_CODE_CARD_PLAYED));
         playPayload = PlayCardsRequest.encode({ cards: fourPine }).finish();
         await p1.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, new Uint8Array(playPayload));
         
         const events = await Promise.all(chopWaiters);
         const lastPlay = CardPlayedEvent.decode(events[0]) as any;
         expect(lastPlay.seat).toBe(1);
         expect(lastPlay.cards.length).toBe(8);
         console.log("4-Pine successfully chopped Quad");
    }, 60000);
});
