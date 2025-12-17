using System;
using UnityEngine;
using VContainer;
using TienLen.Application;
using Cysharp.Threading.Tasks;

namespace TienLen.Presentation.GameRoomScreen
{
    public class GameRoomController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private CardDealer _cardDealer;
        [SerializeField] private PlayerProfileUI[] playerProfileSlots; // Assign these in Inspector (e.g., 4 slots)

        private TienLenMatchHandler _matchHandler;

        [Inject]
        public void Construct(TienLenMatchHandler matchHandler)
        {
            _matchHandler = matchHandler;
        }

        private void Start()
        {
            ClearAllPlayerProfiles(); // Clear profiles on start to ensure clean state

            if (_matchHandler == null)
            {
                Debug.LogWarning("GameRoomController: TienLenMatchHandler not injected.");
            }
        }

        private void OnDestroy()
        {
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

            // Assign to the first available player profile slot
            for (int i = 0; i < playerProfileSlots.Length; i++)
            {
                if (playerProfileSlots[i] != null && !playerProfileSlots[i].gameObject.activeSelf)
                {
                    playerProfileSlots[i].SetProfile(playerAvatar.DisplayName, playerAvatar.AvatarIndex);
                    playerProfileSlots[i].SetActive(true);
                    return;
                }
            }
            Debug.LogWarning($"GameRoomController: No available player profile slots for {playerAvatar.DisplayName}.");
        }

        private void ClearAllPlayerProfiles()
        {
            foreach (var slot in playerProfileSlots)
            {
                if (slot != null)
                {
                    slot.ClearProfile();
                    slot.SetActive(false); // Hide the slot
                }
            }
        }

        public void OnStartGameClicked()
        {
            if (_matchHandler != null)
            {
                Debug.Log("GameRoomController: Requesting Start Game...");
                _matchHandler.StartGameAsync().Forget();
            }
            else
            {
                Debug.LogError("GameRoomController: Cannot start game, Match Handler is null.");
            }
        }
    }
}
