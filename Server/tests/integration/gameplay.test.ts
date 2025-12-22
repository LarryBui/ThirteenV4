import { createTestUser, TestUser } from "./helpers";
import * as protobuf from "protobufjs";
import * as path from "path";

const OP_CODE_START_GAME = 1;
const OP_CODE_PLAY_CARDS = 2;
const OP_CODE_PASS_TURN = 3;

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

    beforeAll(async () => {
        // Load Proto
        root = await protobuf.load(path.join(__dirname, "../../../proto/tienlen.proto"));
        StartGameRequest = root.lookupType("tienlen.v1.StartGameRequest");
        PlayCardsRequest = root.lookupType("tienlen.v1.PlayCardsRequest");
        GameStartedEvent = root.lookupType("tienlen.v1.GameStartedEvent");
        CardPlayedEvent = root.lookupType("tienlen.v1.CardPlayedEvent");
    });

    afterEach(async () => {
        await Promise.all(users.map(u => u.close()));
        users = [];
    });

        const rpcResult = await owner.client.rpc(owner.session, "test_create_match", {});
        const payload = root.lookupType("tienlen.v1.FindMatchResponse").decode(Buffer.from(rpcResult.payload as unknown as string, 'utf-8')) as any;
        const matchId = payload.matchId;

        await Promise.all(users.map(u => u.socket.joinMatch(matchId)));
        await new Promise(r => setTimeout(r, 1000)); // Sync presences

        // 2. Start Game
        const startPayload = StartGameRequest.encode({}).finish();
        await owner.socket.sendMatchState(matchId, OP_CODE_START_GAME, startPayload);

        // 3. Wait for GameStarted and capture hands
        const hands: any[][] = new Array(4);
        let firstTurnSeat = -1;

        const startPromises = users.map((u, i) => {
            return new Promise<void>((resolve) => {
                u.socket.onmatchdata = (msg) => {
                    if (msg.op_code === OP_CODE_GAME_STARTED) {
                        const evt = GameStartedEvent.decode(msg.data) as any;
                        hands[i] = evt.hand;
                        if (i === 0) firstTurnSeat = evt.firstTurnSeat;
                        resolve();
                    }
                };
            });
        });

        await Promise.all(startPromises);
        console.log(`Game Started. First Turn: Seat ${firstTurnSeat}`);

        // 4. Execute a round
        // Let's identify who goes first and have them play their smallest card
        const p1_idx = firstTurnSeat;
        const p1 = users[p1_idx];
        
        // Find smallest card in p1's hand
        const p1_hand = hands[p1_idx];
        const smallestCard = p1_hand.reduce((min, c) => (c.rank * 4 + c.suit < min.rank * 4 + min.suit ? c : min), p1_hand[0]);

        console.log(`Player ${p1_idx} playing smallest card: Rank ${smallestCard.rank} Suit ${smallestCard.suit}`);

        // Setup listeners for CardPlayed
        const playPromises = users.map(u => new Promise<any>((resolve) => {
            u.socket.onmatchdata = (msg) => {
                if (msg.op_code === OP_CODE_CARD_PLAYED) {
                    resolve(CardPlayedEvent.decode(msg.data));
                }
            };
        }));

        const playPayload = PlayCardsRequest.encode({ cards: [smallestCard] }).finish();
        await p1.socket.sendMatchState(matchId, OP_CODE_PLAY_CARDS, playPayload);

        const playEvt = await playPromises[0];
        expect(playEvt.seat).toBe(p1_idx);
        expect(playEvt.cards).toHaveLength(1);
        
        const nextTurn = playEvt.nextTurnSeat;
        console.log(`Card Played. Next Turn: Seat ${nextTurn}`);

        // 5. Next player passes
        const p2 = users[nextTurn];
        const passPromises = users.map(u => new Promise<void>((resolve) => {
            u.socket.onmatchdata = (msg) => {
                if (msg.op_code === OP_CODE_TURN_PASSED) resolve();
            };
        }));

        console.log(`Player ${nextTurn} passing...`);
        await p2.socket.sendMatchState(matchId, OP_CODE_PASS_TURN, new Uint8Array(0));

        await Promise.all(passPromises);
        console.log("Turn passed successfully.");

    }, 20000);
});
