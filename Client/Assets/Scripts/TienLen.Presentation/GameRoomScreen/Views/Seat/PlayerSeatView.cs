using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TienLen.Presentation.GameRoomScreen.Views
{
    /// <summary>
    /// Manages the UI for a single player seat (Local or Opponent).
    /// Handles the display of player metadata and turn-based visuals like the countdown timer.
    /// </summary>
    public sealed class PlayerSeatView : MonoBehaviour
    {
        [Header("Profile References")]
        [SerializeField] private TMP_Text _displayNameText;
        [SerializeField] private Image _avatarImage;
        [SerializeField] private TMP_Text _balanceText;
        [SerializeField] private GameObject _ownerIndicator;

        [Header("Turn Visuals")]
        [SerializeField] private GameObject _turnIndicator;
        [SerializeField] private GameObject _countdownRoot;
        [SerializeField] private TMP_Text _countdownText;
        [SerializeField] private Image _countdownProgressImage; // Optional radial fill

        [Header("Anchors")]
        [Tooltip("The point from which cards fly when this player plays.")]
        [SerializeField] private RectTransform _cardSourceAnchor;

        private int _seatIndex = -1;
        public int SeatIndex => _seatIndex;
        public RectTransform CardSourceAnchor => _cardSourceAnchor != null ? _cardSourceAnchor : (RectTransform)transform;

        private CancellationTokenSource _countdownCts;

        public void SetActive(bool active) => gameObject.SetActive(active);

        /// <summary>
        /// Updates the profile data displayed for this seat.
        /// </summary>
        public void SetProfile(string displayName, Sprite avatar, int seatIndex, bool isOwner)
        {
            _seatIndex = seatIndex;
            if (_displayNameText != null) _displayNameText.text = displayName;
            if (_ownerIndicator != null) _ownerIndicator.SetActive(isOwner);
            if (_avatarImage != null) _avatarImage.sprite = avatar;
            
            SetActive(true);
        }

        public void SetBalance(long balance)
        {
            if (_balanceText != null) _balanceText.text = balance.ToString("N0");
        }

        public void ClearProfile()
        {
            _seatIndex = -1;
            if (_displayNameText != null) _displayNameText.text = string.Empty;
            if (_balanceText != null) _balanceText.text = string.Empty;
            if (_ownerIndicator != null) _ownerIndicator.SetActive(false);
            
            StopCountdown();
            SetTurnActive(false);
        }

        public void SetTurnActive(bool active)
        {
            if (_turnIndicator != null) _turnIndicator.SetActive(active);
        }

        /// <summary>
        /// Starts a visual countdown for the player's turn.
        /// </summary>
        public void StartCountdown(int seconds)
        {
            StopCountdown();
            if (seconds <= 0) return;

            if (_countdownRoot != null) _countdownRoot.SetActive(true);
            
            _countdownCts = new CancellationTokenSource();
            RunCountdownAsync(seconds, _countdownCts.Token).Forget();
        }

        public void StopCountdown()
        {
            _countdownCts?.Cancel();
            _countdownCts?.Dispose();
            _countdownCts = null;

            if (_countdownRoot != null) _countdownRoot.SetActive(false);
        }

        private async UniTaskVoid RunCountdownAsync(int startSeconds, CancellationToken token)
        {
            int remaining = startSeconds;
            while (remaining >= 0 && !token.IsCancellationRequested)
            {
                if (_countdownText != null) _countdownText.text = remaining.ToString();
                if (_countdownProgressImage != null) 
                {
                    _countdownProgressImage.fillAmount = (float)remaining / startSeconds;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: true, cancellationToken: token);
                remaining--;
            }

            if (!token.IsCancellationRequested)
            {
                if (_countdownRoot != null) _countdownRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            StopCountdown();
        }
    }
}
