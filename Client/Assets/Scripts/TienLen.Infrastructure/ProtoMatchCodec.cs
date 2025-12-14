using System;
using System.Collections.Generic;
using Google.Protobuf;
using Tienlen.V1;

namespace TienLen.Infrastructure
{
    /// <summary>
    /// Shared Nakama opcodes (must stay in sync with the server).
    /// </summary>
    public static class TienLenOpcodes
    {
        // Client -> Server
        public const long StartGame = 1;
        public const long PlayCards = 2;
        public const long PassTurn = 3;
        public const long RequestNewGame = 4;

        // Server -> Client events
        public const long PlayerJoined = 101;
        public const long PlayerLeft = 102;
        public const long GameStarted = 103;
        public const long HandDealt = 104;
        public const long CardPlayed = 105;
        public const long TurnPassed = 106;
        public const long GameEnded = 107;
    }

    /// <summary>
    /// Protobuf encoding/decoding helpers for Nakama match messages.
    /// </summary>
    public static class ProtoMatchCodec
    {
        private static readonly Dictionary<long, MessageParser> EventParsers = new()
        {
            { TienLenOpcodes.PlayerJoined, PlayerJoinedEvent.Parser },
            { TienLenOpcodes.PlayerLeft, PlayerLeftEvent.Parser },
            { TienLenOpcodes.GameStarted, GameStartedEvent.Parser },
            { TienLenOpcodes.HandDealt, HandDealtEvent.Parser },
            { TienLenOpcodes.CardPlayed, CardPlayedEvent.Parser },
            { TienLenOpcodes.TurnPassed, TurnPassedEvent.Parser },
            { TienLenOpcodes.GameEnded, GameEndedEvent.Parser },
        };

        // Encode client -> server requests
        public static ArraySegment<byte> EncodeStartGame() => Encode(new StartGameRequest());

        public static ArraySegment<byte> EncodePlayCards(IEnumerable<Card> cards)
        {
            var req = new PlayCardsRequest();
            req.Cards.AddRange(cards);
            return Encode(req);
        }

        public static ArraySegment<byte> EncodePassTurn() => Encode(new PassTurnRequest());

        public static ArraySegment<byte> EncodeRequestNewGame() => Encode(new RequestNewGameRequest());

        /// <summary>
        /// Try to decode a server -> client event for the given opcode.
        /// Returns false if the opcode is unknown or payload is invalid.
        /// </summary>
        public static bool TryDecodeEvent(long opcode, ArraySegment<byte> payload, out IMessage message)
        {
            message = null;
            if (!EventParsers.TryGetValue(opcode, out var parser))
            {
                return false;
            }

            try
            {
                // ParseFrom accepts byte[]; convert once.
                message = parser.ParseFrom(ToArray(payload));
                return true;
            }
            catch (InvalidProtocolBufferException)
            {
                return false;
            }
        }

        private static ArraySegment<byte> Encode(IMessage message)
        {
            var bytes = message.ToByteArray();
            return new ArraySegment<byte>(bytes, 0, bytes.Length);
        }

        private static byte[] ToArray(ArraySegment<byte> segment)
        {
            if (segment.Array == null)
            {
                return Array.Empty<byte>();
            }

            // Respect offset/count from Nakama payloads.
            return segment.AsSpan().ToArray();
        }
    }
}

