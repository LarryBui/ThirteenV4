using System;
using UnityEngine;
using VContainer;
using TienLen.Application; // Updated
using Cysharp.Threading.Tasks;

namespace TienLen.Presentation
{
    public class GameRoomController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private CardDealer _cardDealer;

        private IMatchNetworkClient _matchClient;

        [Inject]
        public void Construct(IMatchNetworkClient matchClient)
        {
            _matchClient = matchClient;
        }

        private void Start()
        {
            if (_matchClient != null)
            {
                _matchClient.OnGameStarted += HandleGameStarted;
                _matchClient.OnPlayerJoined += HandlePlayerJoined; // Subscribed to player joined event
            }
            else
            {
                Debug.LogWarning("GameRoomController: IMatchNetworkClient not injected.");
            }
        }

        private void OnDestroy()
        {
            if (_matchClient != null)
            {
                _matchClient.OnGameStarted -= HandleGameStarted;
                _matchClient.OnPlayerJoined -= HandlePlayerJoined; // Unsubscribed
            }
        }

        private void HandleGameStarted()
        {
            Debug.Log("GameRoomController: Game Started! Triggering Deal Animation.");
            // 52 cards, 2.0 seconds duration
            _cardDealer.AnimateDeal(52, 2.0f).Forget();
        }

        private void HandlePlayerJoined(PlayerAvatar playerAvatar)
        {
            Debug.Log($"GameRoomController: Player {playerAvatar.DisplayName} (ID: {playerAvatar.UserId}, Avatar: {playerAvatar.AvatarIndex}) joined the match.");
            // Later: Spawn/update UI for player avatar
        }

        public void OnStartGameClicked()
        {
            if (_matchClient != null)
            {
                Debug.Log("GameRoomController: Requesting Start Game...");
                _matchClient.SendStartGameAsync().Forget();
            }
            else
            {
                Debug.LogError("GameRoomController: Cannot start game, Match Client is null.");
            }
        }
    }
}
