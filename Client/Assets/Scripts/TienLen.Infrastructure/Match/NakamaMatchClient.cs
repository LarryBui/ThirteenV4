using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nakama;
using TienLen.Domain.Services;
using TienLen.Domain.ValueObjects;
using TienLen.Infrastructure.Services;
using UnityEngine;

namespace TienLen.Infrastructure.Match
{
    /// <summary>
    /// Nakama implementation of IMatchNetworkClient.
    /// </summary>
    public sealed class NakamaMatchClient : IMatchNetworkClient
    {
        private readonly NakamaAuthenticationService _authService;
        private string _matchId;
        private UniTaskCompletionSource<string> _matchmakerCompletionSource;
        private string _matchmakerTicket;

        // --- IMatchNetworkClient Events ---
        public event Action<string> OnPlayerJoined;
        public event Action<string, List<Card>> OnCardsPlayed;
        public event Action<string> OnPlayerSkippedTurn;
        public event Action OnMatchStarted;
        public event Action<string> OnPlayerFinished;

        public NakamaMatchClient(NakamaAuthenticationService authService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            // Subscribe to Nakama's matchmaker matched event
            _authService.Socket.ReceivedMatchmakerMatched += HandleMatchmakerMatched;
        }

        private ISocket Socket => _authService.Socket;

        // --- IMatchNetworkClient Implementation ---

        public async UniTask<string> FindMatchAsync()
        {
            if (Socket == null) throw new InvalidOperationException("Not connected to Nakama socket.");
            if (!Socket.IsConnected) throw new InvalidOperationException("Nakama socket is not connected.");

            _matchmakerCompletionSource = new UniTaskCompletionSource<string>();

            // Add self to matchmaker (2 to 4 players for now)
            var matched = await Socket.AddMatchmakerAsync("*", 2, 4);
            _matchmakerTicket = matched.Ticket;

            // Wait for a match to be found via the HandleMatchmakerMatched event
            var matchId = await _matchmakerCompletionSource.Task;

            // Remove ticket from matchmaker (important for cleanup)
            await Socket.RemoveMatchmakerAsync(_matchmakerTicket);
            _matchmakerTicket = null;
            _matchmakerCompletionSource = null;

            return matchId;
        }

        public async UniTask SendJoinMatchAsync(string matchId)
        {
            if (Socket == null) throw new InvalidOperationException("Not connected to Nakama socket.");
            if (!Socket.IsConnected) throw new InvalidOperationException("Nakama socket is not connected.");

            // Join the match on Nakama
            var match = await Socket.JoinMatchAsync(matchId);
            _matchId = match.Id;
            
            // Subscribe to events specific to this match
            Socket.ReceivedMatchState += HandleMatchState;
            Socket.ReceivedMatchPresence += HandleMatchPresence;

            Debug.Log($"Joined match: {_matchId}");
        }

        public UniTask SendStartGameAsync()
        {
            // return SendAsync(TienLenOpcodes.StartGame, ProtoMatchCodec.EncodeStartGame());
            throw new NotImplementedException("ProtoMatchCodec is removed.");
        }

        public UniTask SendPlayCardsAsync(List<Card> cards)
        {
            // return SendAsync(TienLenOpcodes.PlayCards, ProtoMatchCodec.EncodePlayCards(cards));
            throw new NotImplementedException("ProtoMatchCodec is removed.");
        }

        public UniTask SendPassTurnAsync()
        {
             // return SendAsync(TienLenOpcodes.PassTurn, ProtoMatchCodec.EncodePassTurn());
             throw new NotImplementedException("ProtoMatchCodec is removed.");
        }

        public UniTask SendRequestNewGameAsync()
        {
            // return SendAsync(TienLenOpcodes.RequestNewGame, ProtoMatchCodec.EncodeRequestNewGame());
            throw new NotImplementedException("ProtoMatchCodec is removed.");
        }

        // --- Event Handlers ---

        private void HandleMatchmakerMatched(IMatchmakerMatched matched)
        {
            if (matched?.MatchId != null && _matchmakerCompletionSource != null)
            {
                _matchmakerCompletionSource.TrySetResult(matched.MatchId);
            }
            else if (matched?.MatchId == null && matched?.Token != null && _matchmakerCompletionSource != null)
            {
                // Optionally handle match tokens if not directly receiving MatchId
                // For now, we expect MatchId directly
                Debug.LogWarning("Received matchmaker token, but expected MatchId. Check Nakama server setup.");
                _matchmakerCompletionSource.TrySetException(new Exception("Received matchmaker token, but expected MatchId."));
            }
            else
            {
                Debug.LogWarning("Received matchmaker matched event, but no active matchmaker request or matchId was null.");
            }
        }

        private void HandleMatchPresence(IMatchPresenceEvent presenceEvent)
        {
            if (presenceEvent.MatchId != _matchId) return;

            foreach (var joiner in presenceEvent.Joins)
            {
                OnPlayerJoined?.Invoke(joiner.UserId);
            }
        }

        private void HandleMatchState(IMatchState state)
        {
            if (state.MatchId != _matchId) return;

            // Decode logic would go here.
            // Example:
            // if (state.OpCode == TienLenOpcodes.PlayCards) {
            //    var cards = ProtoMatchCodec.DecodePlayCards(state.State);
            //    OnCardsPlayed?.Invoke(state.UserPresence.UserId, cards);
            // }

            // For now, nothing happens because Codec is missing.
        }

        private async UniTask SendAsync(long opcode, byte[] payload)
        {
            if (Socket == null || !Socket.IsConnected) return;
            await Socket.SendMatchStateAsync(_matchId, opcode, payload);
        }
    }
}
