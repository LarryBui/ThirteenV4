using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TienLen.Application.Session;
using TienLen.Domain.Aggregates;
using TienLen.Domain.ValueObjects;
using UnityEngine;

namespace TienLen.Application
{
    /// <summary>
    /// Application service (Use Case Controller) that orchestrates the match flow.
    /// It bridges the UI (Presentation), the Game Logic (Domain), and the Network (Infrastructure).
    /// </summary>
    public class TienLenMatchHandler : IDisposable
    {
        private readonly IMatchNetworkClient _networkClient;
        private readonly IAuthenticationService _authService;
        private readonly IGameSessionContext _gameSessionContext; // Injected

        public Match CurrentMatch { get; private set; }

        public TienLenMatchHandler(IMatchNetworkClient networkClient, IAuthenticationService authService, IGameSessionContext gameSessionContext)
        {
            _networkClient = networkClient;
            _authService = authService;
            _gameSessionContext = gameSessionContext ?? throw new ArgumentNullException(nameof(gameSessionContext));
            SubscribeToNetworkEvents();
        }

        public void Dispose()
        {
            UnsubscribeFromNetworkEvents();
        }

        // --- Use Cases ---

        public async UniTask FindAndJoinMatchAsync()
        {
            Debug.Log("Handler: Finding an available match...");
            string matchId = await _networkClient.FindMatchAsync();
            Debug.Log($"Handler: Successfully found match {matchId}.");            
            await JoinMatchAsync(matchId);
            Debug.Log($"Handler: Successfully joined match {matchId}.");
        }

        public async UniTask JoinMatchAsync(string matchId)
        {
            Debug.Log($"Handler: Joining match {matchId}...");
            
            // 1. Send request to network
            await _networkClient.SendJoinMatchAsync(matchId);

            // 2. Initialize Domain state
            // Note: Ideally, the server response would confirm success and give initial state.
            // For now, we assume success if no exception.
            CurrentMatch = new Match(matchId); // Assuming matchId is Guid, or change Match ctor
            
            // Update Session Context
            // Seat is unknown (-1) until confirmed by server/logic
            _gameSessionContext.SetMatch(matchId, -1); 

            // Add self to match
            // This will be superseded by the first OnPlayerJoined event for the local player
            // But we need a player object in CurrentMatch for now.
            var selfPlayer = new Player 
            { 
                UserID = _authService.CurrentUserId,
                DisplayName = _authService.CurrentUserDisplayName, // Assuming this is available
                AvatarIndex = _authService.CurrentUserAvatarIndex, // Assuming this is available
                Seat = 1 // Logic to determine seat needed from server state
            };
            CurrentMatch.RegisterPlayer(selfPlayer);
            
            Debug.Log("Handler: Joined match locally.");
        }

        public async UniTask StartGameAsync()
        {
            if (CurrentMatch == null) throw new InvalidOperationException("No active match.");
            await _networkClient.SendStartGameAsync();
            // Domain update happens on OnMatchStarted event
        }

        public async UniTask PlayCardsAsync(List<Card> cards)
        {
            if (CurrentMatch == null) throw new InvalidOperationException("No active match.");
            
            // 1. Optimistic local validation (optional but good for UX)
            // CurrentMatch.PlayTurn(_authService.CurrentUserId, cards); 
            
            // 2. Send to network
            await _networkClient.SendPlayCardsAsync(cards);
        }

        // --- Event Handling (Network -> Domain) ---

        private void SubscribeToNetworkEvents()
        {
            _networkClient.OnPlayerJoined += HandlePlayerJoined;
            _networkClient.OnCardsPlayed += HandleCardsPlayed;
            _networkClient.OnGameStarted += HandleMatchStarted;
            _networkClient.OnPlayerJoinedOP += HandleMatchStateUpdated;
            // ... other events
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _networkClient.OnPlayerJoined -= HandlePlayerJoined;
            _networkClient.OnCardsPlayed -= HandleCardsPlayed;
            _networkClient.OnGameStarted -= HandleMatchStarted;
            _networkClient.OnPlayerJoinedOP -= HandleMatchStateUpdated;
        }

        private void HandlePlayerJoined(PlayerAvatar playerAvatar)
        {
            if (CurrentMatch == null) return;
            Debug.Log($"Handler: Player {playerAvatar.DisplayName} ({playerAvatar.UserId}) joined.");
            // should use onMatchState for full data sync
        }

        private void HandleCardsPlayed(string userId, List<Card> cards)
        {
            if (CurrentMatch == null) return;
            Debug.Log($"Handler: Player {userId} played {cards.Count} cards.");
            
            try
            {
                CurrentMatch.PlayTurn(userId, cards);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Handler: Error applying PlayTurn: {ex.Message}");
                // Sync error! Request full state?
            }
        }

        private void HandleMatchStarted()
        {
            if (CurrentMatch == null) return;
            Debug.Log("Handler: Match started.");
            CurrentMatch.DealCards();
        }

        private void HandleMatchStateUpdated(MatchStateSnapshot snapshot)
        {
            if (CurrentMatch == null) return;
            Debug.Log($"Handler: Match state updated. Owner: {snapshot.OwnerId}, Tick: {snapshot.Tick}");

            Array.Clear(CurrentMatch.Seats, 0, CurrentMatch.Seats.Length);
            var seatsToCopy = Math.Min(snapshot.Seats.Length, CurrentMatch.Seats.Length);
            Array.Copy(snapshot.Seats, CurrentMatch.Seats, seatsToCopy);
            CurrentMatch.OwnerUserID = snapshot.OwnerId;
        }
    }
}
