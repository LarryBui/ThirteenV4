using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TienLen.Domain.Aggregates;
using TienLen.Domain.Services;
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

        public Match CurrentMatch { get; private set; }

        public TienLenMatchHandler(IMatchNetworkClient networkClient, IAuthenticationService authService)
        {
            _networkClient = networkClient;
            _authService = authService;
            SubscribeToNetworkEvents();
        }

        public void Dispose()
        {
            UnsubscribeFromNetworkEvents();
        }

        // --- Use Cases ---

        public async UniTask JoinMatchAsync(string matchId)
        {
            Debug.Log($"Handler: Joining match {matchId}...");
            
            // 1. Send request to network
            await _networkClient.SendJoinMatchAsync(matchId);

            // 2. Initialize Domain state
            // Note: Ideally, the server response would confirm success and give initial state.
            // For now, we assume success if no exception.
            CurrentMatch = new Match(Guid.Parse(matchId)); // Assuming matchId is Guid, or change Match ctor
            
            // Add self to match
            var selfPlayer = new Player 
            { 
                UserID = _authService.CurrentUserId,
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
            _networkClient.OnMatchStarted += HandleMatchStarted;
            // ... other events
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _networkClient.OnPlayerJoined -= HandlePlayerJoined;
            _networkClient.OnCardsPlayed -= HandleCardsPlayed;
            _networkClient.OnMatchStarted -= HandleMatchStarted;
        }

        private void HandlePlayerJoined(string userId)
        {
            if (CurrentMatch == null) return;
            Debug.Log($"Handler: Player {userId} joined.");

            // In a real scenario, we need to know the Seat # from the server.
            // For now, simple sequential seat assignment for demo.
            int nextSeat = 0;
            for(int i=0; i<CurrentMatch.Seats.Length; i++) {
                if(string.IsNullOrEmpty(CurrentMatch.Seats[i])) {
                    nextSeat = i + 1;
                    break;
                }
            }

            if (nextSeat > 0)
            {
                var newPlayer = new Player { UserID = userId, Seat = nextSeat };
                CurrentMatch.RegisterPlayer(newPlayer);
            }
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
    }
}
