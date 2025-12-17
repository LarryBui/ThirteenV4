using System;
using System.Collections;
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
        private static readonly JsonSerializerSettings DebugJsonSettings = new()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly NakamaAuthenticationService _authService;
        private string _matchId;
        private ISocket _subscribedSocket;

        private readonly object _presenceLock = new();
        private readonly Dictionary<string, PresenceInfo> _presenceByUserId = new();

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
            if (string.IsNullOrWhiteSpace(matchId)) throw new ArgumentException("Match id is required.", nameof(matchId));

            var previousMatchId = _matchId;
            try
            {
                // Subscribe before awaiting JoinMatchAsync to avoid missing early state broadcasts.
                EnsureSocketEventSubscriptions(Socket);

                _matchId = matchId;
                ClearPresenceCache();

                // Join the match on Nakama.
                var match = await Socket.JoinMatchAsync(matchId);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("MatchClient: JoinMatchAsync response: " + TrySerializeForDebug(match));
#endif

                // Use returned id when available; otherwise fall back to the requested match id.
                _matchId = string.IsNullOrEmpty(match?.Id) ? matchId : match.Id;

                SeedPresenceCacheFromJoinMatchResponse(match);
            }
            catch
            {
                _matchId = previousMatchId;
                throw;
            }
        }

        public async UniTask SendStartGameAsync()
        {
            var request = new Proto.StartGameRequest();
            await SendAsync((long)Proto.OpCode.StartGame, request.ToByteArray());
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("MatchClient: Received MatchPresenceEvent: " + TrySerializeForDebug(presenceEvent));
#endif

            if (presenceEvent.MatchId != _matchId) return;

            foreach (var joiner in presenceEvent.Joins)
            {
                UpsertPresence(joiner, isInMatch: true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("MatchClient: Joiner presence: " + TrySerializeForDebug(joiner));
#endif

                // Extract display name and create a PlayerAvatar
                string displayName = string.IsNullOrEmpty(joiner.Username) ? $"Player {joiner.UserId.Substring(0, 4)}" : joiner.Username;
                int avatarIndex = GetAvatarIndex(joiner.UserId); // Deterministic avatar selection

                var playerAvatar = new PlayerAvatar(joiner.UserId, displayName, avatarIndex);
                OnPlayerJoined?.Invoke(playerAvatar); // Invoke with rich data

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("MatchClient: Raised OnPlayerJoined with PlayerAvatar: " + TrySerializeForDebug(playerAvatar));
#endif
            }

            foreach (var leaver in presenceEvent.Leaves)
            {
                UpsertPresence(leaver, isInMatch: false);
            }
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

                        Debug.Log("MatchClient opcode Matchstate... " + TrySerializeForDebug(state));

            if (state.MatchId != _matchId) return;

            switch (state.OpCode)
            {
                case (long)Proto.OpCode.PlayerJoined:
                    try
                    {
                        var payload = Proto.MatchStateSnapshot.Parser.ParseFrom(state.State);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log("MatchClient Opcode50: MatchStateSnapshot payload: " + TrySerializeForDebug(payload));
#endif
                        var seats = new string[payload.Seats.Count];
                        payload.Seats.CopyTo(seats, 0);
                        var snapshot = new MatchStateSnapshot(seats, payload.OwnerId, payload.Tick);
                        OnPlayerJoinedOP?.Invoke(snapshot);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log("MatchClient Opcode50: Raised OnPlayerJoinedOP with snapshot: " + TrySerializeForDebug(snapshot));
#endif
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"MatchClient opcode50: Failed to parse match state payload: {e}");
                    }
                    break;
                case (long)Proto.OpCode.GameStarted:
                        Debug.LogError($"MatchClient opcode GameStarted: {TrySerializeForDebug(state)}  ");

                    try
                    {
                        var payload = Proto.GameStartedEvent.Parser.ParseFrom(state.State);
                        OnGameStarted?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error parsing GameStartedEvent: {e}");
                    }
                    break;
            }
        }

        private static string TrySerializeForDebug(object value)
        {
            if (value == null) return "null";

            try
            {
                return JsonConvert.SerializeObject(value, DebugJsonSettings);
            }
            catch (Exception ex)
            {
                return $"<json-serialize-failed: {ex.GetType().Name}: {ex.Message}>";
            }
        }

        private async UniTask SendAsync(long opcode, byte[] payload)
        {
            if (Socket == null || !Socket.IsConnected) return;
            await Socket.SendMatchStateAsync(_matchId, opcode, payload);
        }

        /// <summary>
        /// Subscribes to socket match events once per socket instance.
        /// This prevents losing early broadcasts (e.g., lobby snapshot after join) and avoids duplicate handlers.
        /// </summary>
        /// <param name="socket">Socket instance used for realtime communication.</param>
        private void EnsureSocketEventSubscriptions(ISocket socket)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));

            if (ReferenceEquals(_subscribedSocket, socket))
            {
                return;
            }

            if (_subscribedSocket != null)
            {
                _subscribedSocket.ReceivedMatchState -= HandleMatchState;
                _subscribedSocket.ReceivedMatchPresence -= HandleMatchPresence;
            }

            socket.ReceivedMatchState += HandleMatchState;
            socket.ReceivedMatchPresence += HandleMatchPresence;
            _subscribedSocket = socket;
        }

        /// <summary>
        /// Clears all cached presence information for the current match.
        /// </summary>
        private void ClearPresenceCache()
        {
            lock (_presenceLock)
            {
                _presenceByUserId.Clear();
            }
        }

        /// <summary>
        /// Seeds the presence cache from the JoinMatchAsync response to capture existing players immediately.
        /// </summary>
        private void SeedPresenceCacheFromJoinMatchResponse(object match)
        {
            if (match == null) return;

            // Property names are taken from Nakama's IMatch shape, but we read via reflection
            // to keep the client resilient to SDK changes (e.g., missing properties or different names).
            var self = TryGetUserPresenceProperty(match, "Self") ?? TryGetUserPresenceProperty(match, "SelfPresence");
            UpsertPresence(self, isInMatch: true);

            foreach (var presence in TryGetUserPresenceEnumerableProperty(match, "Presences"))
            {
                UpsertPresence(presence, isInMatch: true);
            }
        }

        private void UpsertPresence(IUserPresence presence, bool isInMatch)
        {
            if (presence == null) return;
            if (string.IsNullOrWhiteSpace(presence.UserId)) return;

            var now = DateTimeOffset.UtcNow;
            var sessionId = TryGetStringProperty(presence, "SessionId");
            var node = TryGetStringProperty(presence, "Node");
            var status = TryGetStringProperty(presence, "Status");

            lock (_presenceLock)
            {
                if (!_presenceByUserId.TryGetValue(presence.UserId, out var info))
                {
                    info = new PresenceInfo(presence.UserId);
                    _presenceByUserId.Add(presence.UserId, info);
                }

                info.Update(
                    username: presence.Username,
                    sessionId: sessionId,
                    node: node,
                    status: status,
                    isInMatch: isInMatch,
                    updatedUtc: now);
            }
        }

        private static IUserPresence TryGetUserPresenceProperty(object instance, string propertyName)
        {
            var propertyInfo = instance.GetType().GetProperty(propertyName);
            if (propertyInfo == null) return null;
            return propertyInfo.GetValue(instance) as IUserPresence;
        }

        private static IEnumerable<IUserPresence> TryGetUserPresenceEnumerableProperty(object instance, string propertyName)
        {
            var propertyInfo = instance.GetType().GetProperty(propertyName);
            if (propertyInfo == null) yield break;

            if (propertyInfo.GetValue(instance) is not IEnumerable enumerable) yield break;

            foreach (var item in enumerable)
            {
                if (item is IUserPresence presence)
                {
                    yield return presence;
                }
            }
        }

        private static string TryGetStringProperty(object instance, string propertyName)
        {
            if (instance == null) return null;
            var propertyInfo = instance.GetType().GetProperty(propertyName);
            if (propertyInfo == null) return null;
            if (propertyInfo.PropertyType != typeof(string)) return null;
            return propertyInfo.GetValue(instance) as string;
        }

        /// <summary>
        /// Captures best-effort metadata about a user presence for lobby synchronization and debugging.
        /// </summary>
        private sealed class PresenceInfo
        {
            public string UserId { get; }
            public string Username { get; private set; }
            public string SessionId { get; private set; }
            public string Node { get; private set; }
            public string Status { get; private set; }
            public DateTimeOffset LastUpdatedUtc { get; private set; }
            public bool IsInMatch { get; private set; }

            public PresenceInfo(string userId)
            {
                UserId = userId;
            }

            public void Update(string username, string sessionId, string node, string status, bool isInMatch, DateTimeOffset updatedUtc)
            {
                if (!string.IsNullOrWhiteSpace(username)) Username = username;
                if (!string.IsNullOrWhiteSpace(sessionId)) SessionId = sessionId;
                if (!string.IsNullOrWhiteSpace(node)) Node = node;
                if (status != null) Status = status;
                IsInMatch = isInMatch;
                LastUpdatedUtc = updatedUtc;
            }
        }
    }
}
