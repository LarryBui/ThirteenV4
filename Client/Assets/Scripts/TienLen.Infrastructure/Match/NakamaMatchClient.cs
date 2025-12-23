using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nakama;
using TienLen.Application; // Updated for IMatchNetworkClient and PlayerAvatar
using TienLen.Domain.ValueObjects;
using TienLen.Infrastructure.Services;
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
        private readonly ILogger<NakamaMatchClient> _logger;
        private string _matchId;
        private ISocket _subscribedSocket;

        private readonly object _presenceLock = new();
        private readonly Dictionary<string, PresenceInfo> _presenceByUserId = new();

        // --- IMatchNetworkClient Events ---
        public event Action<int, List<Card>, int, bool, long> OnCardsPlayed; // seat, cards, nextTurnSeat, newRound, turnSecondsRemaining
        public event Action<string> OnPlayerSkippedTurn;
        public event Action<int, int, bool, long> OnTurnPassed; // seat, nextTurnSeat, newRound, turnSecondsRemaining
        public event Action<List<Card>, int, long> OnGameStarted; // hand, firstTurnSeat, turnSecondsRemaining
        /// <inheritdoc />
        public event Action<MatchStateSnapshotDto> OnPlayerJoinedOP;
        /// <inheritdoc />
        public event Action<int, string> OnPlayerLeft;
        public event Action<List<int>, Dictionary<int, List<Card>>> OnGameEnded;
        public event Action<int, string> OnGameError;
        public event Action<IReadOnlyList<PresenceChange>> OnMatchPresenceChanged;
        public event Action<string> OnPlayerFinished;

        /// <summary>
        /// Initializes the match client with required services.
        /// </summary>
        /// <param name="authService">Authentication service used for socket access.</param>
        /// <param name="logger">Logger used for structured match diagnostics.</param>
        public NakamaMatchClient(NakamaAuthenticationService authService, ILogger<NakamaMatchClient> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? NullLogger<NakamaMatchClient>.Instance;
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

            try
            {
                // The server returns a quoted JSON string (e.g. "uuid").
                // DeserializeObject<string> will unquote it correctly.
                var matchId = JsonConvert.DeserializeObject<string>(rpcResponse.Payload);
                return matchId;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse find_match RPC response: {Payload}", rpcResponse.Payload);
                throw;
            }
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

        /// <inheritdoc />
        public async UniTask SendLeaveMatchAsync()
        {
            var matchId = _matchId;

            // Clear local subscription filters immediately so late match-state frames are ignored.
            _matchId = null;
            ClearPresenceCache();

            if (string.IsNullOrWhiteSpace(matchId))
            {
                return;
            }

            if (Socket == null || !Socket.IsConnected)
            {
                return;
            }

            await Socket.LeaveMatchAsync(matchId);
        }

        public async UniTask SendStartGameAsync()
        {
            var request = new Proto.StartGameRequest();
            await SendAsync((long)Proto.OpCode.StartGame, request.ToByteArray());
        }

        public async UniTask SendPlayCardsAsync(List<Card> cards)
        {
            var request = new Proto.PlayCardsRequest();
            foreach (var card in cards)
            {
                request.Cards.Add(ToProto(card));
            }
            await SendAsync((long)Proto.OpCode.PlayCards, request.ToByteArray());
        }

        public async UniTask SendPassTurnAsync()
        {
            var request = new Proto.PassTurnRequest();
            await SendAsync((long)Proto.OpCode.PassTurn, request.ToByteArray());
        }

        public async UniTask SendRequestNewGameAsync()
        {
            var request = new Proto.RequestNewGameRequest();
            await SendAsync((long)Proto.OpCode.RequestNewGame, request.ToByteArray());
        }

        private static Proto.Card ToProto(Card card)
        {
            return new Proto.Card
            {
                Suit = (Proto.Suit)(int)card.Suit,
                Rank = (Proto.Rank)(int)card.Rank
            };
        }

        private static Card ToDomain(Proto.Card protoCard)
        {
            return new Card(
                (TienLen.Domain.Enums.Rank)(int)protoCard.Rank,
                (TienLen.Domain.Enums.Suit)(int)protoCard.Suit
            );
        }

        // --- Event Handlers ---

        private int GetAvatarIndex(string userId)
        {
            return 0;  // Placeholder for avatar selection logic
            // Simple deterministic avatar selection based on UserId hash
            // This assumes we have a pool of avatars to pick from (e.g., 0-3 for 4 avatars)
            // Need to know the total number of available avatars. For now, let's assume 4.
            // A better solution would be to get this from a configuration or server metadata.
            int hash = userId.GetHashCode();
            return Math.Abs(hash % 4); // Example: maps to indices 0, 1, 2, 3
        }

        private void HandleMatchState(IMatchState state)
        {
            HandleMatchStateMainThread(state).Forget();
        }

        private async UniTaskVoid HandleMatchStateMainThread(IMatchState state)
        {
            await UniTask.SwitchToMainThread();

            if (state.MatchId != _matchId) return;

            switch (state.OpCode)
            {
                case (long)Proto.OpCode.PlayerJoined: // 50
                    try
                    {
                        var payload = Proto.MatchStateSnapshot.Parser.ParseFrom(state.State);
                        var seats = new string[payload.Seats.Count];
                        payload.Seats.CopyTo(seats, 0);

                        var players = new List<PlayerStateDTO>();
                        foreach (var p in payload.Players)
                        {
                            players.Add(new PlayerStateDTO(
                                p.UserId,
                                p.Seat,
                                p.IsOwner,
                                p.CardsRemaining,
                                p.DisplayName,
                                p.AvatarIndex));
                        }

                        // Updated to use OwnerSeat (int) instead of OwnerId (string)
                        var snapshot = new MatchStateSnapshotDto(
                            seats,
                            payload.OwnerSeat,
                            payload.Tick,
                            payload.TurnSecondsRemaining,
                            players);
                        _logger?.LogInformation(
                            "Match join snapshot received. matchId={matchId} seatCount={seatCount} playerCount={playerCount} ownerSeat={ownerSeat}",
                            _matchId,
                            payload.Seats.Count,
                            players.Count,
                            payload.OwnerSeat);
                        OnPlayerJoinedOP?.Invoke(snapshot);
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(e, "MatchClient: Failed to parse MatchStateSnapshot. matchId={matchId}", _matchId);
                    }
                    break;

                case (long)Proto.OpCode.PlayerLeft: // 51
                    try
                    {
                        var payload = Proto.PlayerLeftEvent.Parser.ParseFrom(state.State);
                        OnPlayerLeft?.Invoke(payload.Seat, payload.UserId);
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(e, "MatchClient: Failed to parse PlayerLeftEvent. matchId={matchId}", _matchId);
                    }
                    break;

                case (long)Proto.OpCode.GameStarted: // 100
                    try
                    {
                        var payload = Proto.GameStartedEvent.Parser.ParseFrom(state.State);
                        
                        var hand = new List<Card>();
                        if (payload.Hand != null)
                        {
                            foreach (var c in payload.Hand) hand.Add(ToDomain(c));
                        }

                        OnGameStarted?.Invoke(hand, payload.FirstTurnSeat, payload.TurnSecondsRemaining);
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(e, "MatchClient: Failed to parse GameStartedEvent. matchId={matchId}", _matchId);
                    }
                    break;

                case (long)Proto.OpCode.CardPlayed: // 102
                    try
                    {
                        var payload = Proto.CardPlayedEvent.Parser.ParseFrom(state.State);
                        var cards = new List<Card>();
                        foreach (var c in payload.Cards) cards.Add(ToDomain(c));
                        // payload.Seat is int32, NextTurnSeat is int32
                        OnCardsPlayed?.Invoke(
                            payload.Seat,
                            cards,
                            payload.NextTurnSeat,
                            payload.NewRound,
                            payload.TurnSecondsRemaining);
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(e, "MatchClient: Failed to parse CardPlayedEvent. matchId={matchId}", _matchId);
                    }
                    break;

                case (long)Proto.OpCode.TurnPassed: // 103
                    try
                    {
                        var payload = Proto.TurnPassedEvent.Parser.ParseFrom(state.State);
                        OnTurnPassed?.Invoke(
                            payload.Seat,
                            payload.NextTurnSeat,
                            payload.NewRound,
                            payload.TurnSecondsRemaining);
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(e, "MatchClient: Failed to parse TurnPassedEvent. matchId={matchId}", _matchId);
                    }
                    break;

                case (long)Proto.OpCode.GameEnded: // 104
                    try
                    {
                        var payload = Proto.GameEndedEvent.Parser.ParseFrom(state.State);
                        var finishOrder = new List<int>(payload.FinishOrderSeats);
                        
                        var remainingHands = new Dictionary<int, List<Card>>();
                        if (payload.RemainingHands != null)
                        {
                            foreach (var entry in payload.RemainingHands)
                            {
                                var cards = new List<Card>();
                                if (entry.Value != null && entry.Value.Cards != null)
                                {
                                    foreach (var c in entry.Value.Cards)
                                    {
                                        cards.Add(ToDomain(c));
                                    }
                                }
                                remainingHands.Add(entry.Key, cards);
                            }
                        }

                        OnGameEnded?.Invoke(finishOrder, remainingHands);
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(e, "MatchClient: Failed to parse GameEndedEvent. matchId={matchId}", _matchId);
                    }
                    break;

                case (long)Proto.OpCode.GameError: // 105
                    try
                    {
                        var payload = Proto.GameErrorEvent.Parser.ParseFrom(state.State);
                        OnGameError?.Invoke(payload.Code, payload.Message);
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(e, "MatchClient: Failed to parse GameErrorEvent. matchId={matchId}", _matchId);
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
            if (string.IsNullOrWhiteSpace(_matchId)) return;

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

        private void HandleMatchPresence(IMatchPresenceEvent presenceEvent)
        {
            HandleMatchPresenceMainThread(presenceEvent).Forget();
        }

        private async UniTaskVoid HandleMatchPresenceMainThread(IMatchPresenceEvent presenceEvent)
        {
            await UniTask.SwitchToMainThread();

            if (presenceEvent == null) return;
            if (presenceEvent.MatchId != _matchId) return;

            var changes = new List<PresenceChange>();

            if (presenceEvent.Joins != null)
            {
                foreach (var presence in presenceEvent.Joins)
                {
                    UpsertPresence(presence, isInMatch: true);
                    if (presence == null || string.IsNullOrWhiteSpace(presence.UserId)) continue;
                    _logger?.LogInformation(
                        "Match presence joined. matchId={matchId} userId={userId} username={username}",
                        _matchId,
                        presence.UserId,
                        presence.Username);
                    changes.Add(new PresenceChange(presence.UserId, presence.Username, joined: true));
                }
            }

            if (presenceEvent.Leaves != null)
            {
                foreach (var presence in presenceEvent.Leaves)
                {
                    UpsertPresence(presence, isInMatch: false);
                    if (presence == null || string.IsNullOrWhiteSpace(presence.UserId)) continue;
                    changes.Add(new PresenceChange(presence.UserId, presence.Username, joined: false));
                }
            }

            if (changes.Count > 0)
            {
                OnMatchPresenceChanged?.Invoke(changes);
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
