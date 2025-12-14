using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Nakama;
using Tienlen.V1;

namespace TienLen.Infra.Nakama.Match
{
    /// <summary>
    /// Thin Nakama client wrapper to send/receive Tien Len match messages using protobuf.
    /// </summary>
    public sealed class TienLenMatchClient
    {
        private readonly ISocket _socket;
        private readonly string _matchId;

        public TienLenMatchClient(ISocket socket, string matchId)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _matchId = matchId ?? throw new ArgumentNullException(nameof(matchId));
        }

        // --- Send helpers ---

        public Task SendStartGameAsync()
            => SendAsync(TienLenOpcodes.StartGame, ProtoMatchCodec.EncodeStartGame());

        public Task SendPlayCardsAsync(IEnumerable<Card> cards)
            => SendAsync(TienLenOpcodes.PlayCards, ProtoMatchCodec.EncodePlayCards(cards));

        public Task SendPassTurnAsync()
            => SendAsync(TienLenOpcodes.PassTurn, ProtoMatchCodec.EncodePassTurn());

        public Task SendRequestNewGameAsync()
            => SendAsync(TienLenOpcodes.RequestNewGame, ProtoMatchCodec.EncodeRequestNewGame());

        // --- Receive helper ---

        /// <summary>
        /// Tries to parse an incoming match data message into the expected protobuf type based on opcode.
        /// Returns null if the opcode is unknown or payload is invalid.
        /// </summary>
        public IMessage TryDecode(IMatchData matchData)
        {
            if (matchData == null) return null;

            var payloadSegment = new ArraySegment<byte>(matchData.Data, 0, matchData.Data.Length);
            ProtoMatchCodec.TryDecodeEvent(matchData.OpCode, payloadSegment, out var message);
            return message;
        }

        // --- Internals ---

        private Task SendAsync(long opcode, ArraySegment<byte> payload)
        {
            var content = payload.Array;
            var length = payload.Count;
            var offset = payload.Offset;

            // Trim to exact slice expected by Nakama socket.
            var buffer = payload.Array != null && (offset != 0 || length != payload.Array.Length)
                ? payload.AsSpan().ToArray()
                : content ?? Array.Empty<byte>();

            return _socket.SendMatchDataAsync(_matchId, opcode, buffer);
        }
    }
}
