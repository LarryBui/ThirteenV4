using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Nakama;
using TienLen.Domain.ValueObjects;

namespace TienLen.Infrastructure.Match
{
    /// <summary>
    /// Thin Nakama client wrapper to send/receive Tien Len match messages using protobuf.
    /// </summary>
    public sealed class TienLenMatchClient
    {
        private readonly ISocket _socket;
        private readonly string _matchId;
        private readonly Action<IMessage> _onMessage;

        /// <summary>
        /// Create a match client and subscribe to socket match data for this match.
        /// </summary>
        public TienLenMatchClient(ISocket socket, string matchId, Action<IMessage> onMessage)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _matchId = matchId ?? throw new ArgumentNullException(nameof(matchId));
            _onMessage = onMessage ?? (_ => { });

            _socket.ReceivedMatchState += HandleMatchState;
        }

        // --- Send helpers ---

        public Task SendStartGameAsync()
            // => SendAsync(TienLenOpcodes.StartGame, ProtoMatchCodec.EncodeStartGame());
            => throw new NotImplementedException("ProtoMatchCodec is removed.");

        public Task SendPlayCardsAsync(IEnumerable<Card> cards)
            // => SendAsync(TienLenOpcodes.PlayCards, ProtoMatchCodec.EncodePlayCards(cards));
            => throw new NotImplementedException("ProtoMatchCodec is removed.");

        public Task SendPassTurnAsync()
            // => SendAsync(TienLenOpcodes.PassTurn, ProtoMatchCodec.EncodePassTurn());
            => throw new NotImplementedException("ProtoMatchCodec is removed.");

        public Task SendRequestNewGameAsync()
            // => SendAsync(TienLenOpcodes.RequestNewGame, ProtoMatchCodec.EncodeRequestNewGame());
            => throw new NotImplementedException("ProtoMatchCodec is removed.");

        // --- Receive helper ---

        /// <summary>
        /// Tries to parse an incoming match state message into the expected protobuf type based on opcode.
        /// Returns null if the opcode is unknown or payload is invalid.
        /// </summary>
        public IMessage TryDecode(IMatchState matchState)
        {
            if (matchState == null) return null;

            var payloadSegment = new ArraySegment<byte>(matchState.State, 0, matchState.State.Length);
            // ProtoMatchCodec.TryDecodeEvent(matchState.OpCode, payloadSegment, out var message);
            // return message;
            throw new NotImplementedException("ProtoMatchCodec is removed.");
        }

        private void HandleMatchState(IMatchState state)
        {
            if (state == null || state.MatchId != _matchId) return;

            var decoded = TryDecode(state);
            if (decoded != null)
            {
                _onMessage(decoded);
            }
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

            return _socket.SendMatchStateAsync(_matchId, opcode, buffer);
        }
    }
}
