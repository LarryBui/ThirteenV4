using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nakama;
using TienLen.Application; // Updated for IMatchNetworkClient and PlayerAvatar
using TienLen.Domain.ValueObjects;
using TienLen.Infrastructure.Services;
using UnityEngine;
using Google.Protobuf;
using Proto = Tienlen.V1;
using Newtonsoft.Json;


namespace TienLen.Infrastructure.Match
{
    /// <summary>
    /// Nakama implementation of IMatchNetworkClient.
    /// </summary>
    public sealed class NakamaMatchClient : IMatchNetworkClient
    {
        private readonly NakamaAuthenticationService _authService;
        private string _matchId;

        // --- IMatchNetworkClient Events ---
        public event Action<PlayerAvatar> OnPlayerJoined; // Updated
        public event Action<string, List<Card>> OnCardsPlayed;
        public event Action<string> OnPlayerSkippedTurn;
        public event Action OnGameStarted;
        /// <inheritdoc />
        public event Action<MatchStateSnapshot> OnPlayerJoinedOP;
        public event Action<string> OnPlayerFinished;

        public NakamaMatchClient(NakamaAuthenticationService authService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        private ISocket Socket => _authService.Socket;
        private IClient Client => _authService.Client;

        // --- IMatchNetworkClient Implementation ---

        public async UniTask<string> FindMatchAsync()
        {
            if (Socket == null) throw new InvalidOperationException("Not connected to Nakama socket.");
            if (!Socket.IsConnected) throw new InvalidOperationException("Nakama socket is not connected.");

            // 1. Call server-side RPC to find or create a match
            var rpcId = "find_match";
            var rpcResponse = await Client.RpcAsync(_authService.Session, rpcId);

            if (rpcResponse == null || string.IsNullOrEmpty(rpcResponse.Payload))
            {
                throw new InvalidOperationException($"RPC '{rpcId}' returned no match ID.");
            }

            var matchId = rpcResponse.Payload;

            return matchId;
        }

        public async UniTask SendJoinMatchAsync(string matchId)
        {
            if (Socket == null) throw new InvalidOperationException("Not connected to Nakama socket.");
            if (!Socket.IsConnected) throw new InvalidOperationException("Nakama socket is not connected.");

            // Join the match on Nakama
            var match = await Socket.JoinMatchAsync(matchId);
            Debug.Log("MatchClient: Await Joined match: " + JsonConvert.SerializeObject(match));
            _matchId = match.Id;

            // Subscribe to events specific to this match
            Socket.ReceivedMatchState += HandleMatchState;
            Socket.ReceivedMatchPresence += HandleMatchPresence;

            Debug.Log($"MatchClient: Joined match: {_matchId}");
        }

        public async UniTask SendStartGameAsync()
        {
            var request = new Proto.StartGameRequest();
            await SendAsync((long)Proto.OpCode.StartGame, request.ToByteArray());
            Debug.Log("MatchClient: Sent StartGameRequest.");
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

        /// <summary>
        /// Handles match presence events (players joining/leaving).
        /// This is only used for setting up new player flag. use onMatchState for more rich data
        /// </summary>
        /// <param name="presenceEvent"></param>
        private void HandleMatchPresence(IMatchPresenceEvent presenceEvent)
        {
            Debug.Log("MatchClient: Received Match Join/Leave Event." + JsonConvert.SerializeObject(presenceEvent));

            if (presenceEvent.MatchId != _matchId) return;

            foreach (var joiner in presenceEvent.Joins)
            {
                // Extract display name and create a PlayerAvatar
                string displayName = string.IsNullOrEmpty(joiner.Username) ? $"Player {joiner.UserId.Substring(0, 4)}" : joiner.Username;
                int avatarIndex = GetAvatarIndex(joiner.UserId); // Deterministic avatar selection

                var playerAvatar = new PlayerAvatar(joiner.UserId, displayName, avatarIndex);
                OnPlayerJoined?.Invoke(playerAvatar); // Invoke with rich data
            }

            // Handle leaves if necessary
        }

        private int GetAvatarIndex(string userId)
        {
            // Simple deterministic avatar selection based on UserId hash
            // This assumes we have a pool of avatars to pick from (e.g., 0-3 for 4 avatars)
            // Need to know the total number of available avatars. For now, let's assume 4.
            // A better solution would be to get this from a configuration or server metadata.
            int hash = userId.GetHashCode();
            return Math.Abs(hash % 4); // Example: maps to indices 0, 1, 2, 3
        }

        private void HandleMatchState(IMatchState state)
        {
            if (state.MatchId != _matchId) return;

            switch (state.OpCode)
            {
                case (long)Proto.OpCode.PlayerJoined:
                    try
                    {
                        var payload = Proto.MatchStateSnapshot.Parser.ParseFrom(state.State);
                        var seats = new string[payload.Seats.Count];
                        payload.Seats.CopyTo(seats, 0);
                        var snapshot = new MatchStateSnapshot(seats, payload.OwnerId, payload.Tick);
                        OnPlayerJoinedOP?.Invoke(snapshot);
                        Debug.Log($"MatchClient: Match state updated. Owner: {snapshot.OwnerId}, Tick: {snapshot.Tick}, Seats: [{string.Join(", ", snapshot.Seats)}]");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"MatchClient: Failed to parse match state payload: {e}");
                    }
                    break;
                case (long)Proto.OpCode.GameStarted:
                    try
                    {
                        var payload = Proto.GameStartedEvent.Parser.ParseFrom(state.State);
                        Debug.Log($"MatchClient: Game Started! Phase: {payload.Phase}, First Turn: {payload.FirstTurnUserId}");
                        OnGameStarted?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error parsing GameStartedEvent: {e}");
                    }
                    break;
            }
        }

        private async UniTask SendAsync(long opcode, byte[] payload)
        {
            if (Socket == null || !Socket.IsConnected) return;
            await Socket.SendMatchStateAsync(_matchId, opcode, payload);
        }
    }
}
