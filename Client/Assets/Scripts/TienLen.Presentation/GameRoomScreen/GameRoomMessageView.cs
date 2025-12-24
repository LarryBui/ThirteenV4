using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace TienLen.Presentation.GameRoomScreen
{
    /// <summary>
    /// Displays transient game messages and fades them out after a delay.
    /// </summary>
    public sealed class GameRoomMessageView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text _messageText;

        [Header("Timing")]
        [SerializeField] private float _minVisibleSeconds = 2f;
        [SerializeField] private float _fadeSeconds = 2f;

        private int _messageToken;
        private float _lastShownAt;

        /// <summary>
        /// Shows an error message to the player.
        /// </summary>
        /// <param name="message">Message to display.</param>
        public void ShowError(string message)
        {
            ShowMessage(message);
        }

        /// <summary>
        /// Shows a general informational message to the player.
        /// </summary>
        /// <param name="message">Message to display.</param>
        public void ShowInfo(string message)
        {
            ShowMessage(message);
        }

        /// <summary>
        /// Clears the current message after respecting the minimum visible duration.
        /// </summary>
        public void RequestClear()
        {
            _messageToken++;
            var token = _messageToken;
            ClearAfterMinimumAsync(token).Forget();
        }

        private void ShowMessage(string message)
        {
            if (_messageText == null) return;

            _messageToken++;
            var token = _messageToken;

            _messageText.text = message ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_messageText.text))
            {
                SetMessageVisible(false);
                SetMessageAlpha(0f);
                return;
            }

            SetMessageVisible(true);
            SetMessageAlpha(1f);
            _lastShownAt = Time.time;
        }

        private async UniTaskVoid ClearAfterMinimumAsync(int token)
        {
            var elapsed = Time.time - _lastShownAt;
            var waitSeconds = Mathf.Max(0f, _minVisibleSeconds - elapsed);
            if (waitSeconds > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(waitSeconds));
            }

            if (_messageToken != token) return;

            await FadeOutAsync(token);
        }

        private async UniTask FadeOutAsync(int token)
        {
            if (_messageText == null) return;

            var duration = Mathf.Max(0f, _fadeSeconds);
            if (duration <= 0f)
            {
                ClearImmediate();
                return;
            }

            var startAlpha = _messageText.color.a;
            var startTime = Time.time;

            while (_messageToken == token && Time.time < startTime + duration)
            {
                var t = (Time.time - startTime) / duration;
                SetMessageAlpha(Mathf.Lerp(startAlpha, 0f, t));
                await UniTask.Yield();
            }

            if (_messageToken != token) return;

            ClearImmediate();
        }

        private void ClearImmediate()
        {
            _messageToken++;
            SetMessageVisible(false);
            SetMessageAlpha(0f);
        }

        private void SetMessageAlpha(float alpha)
        {
            if (_messageText == null) return;

            var color = _messageText.color;
            color.a = alpha;
            _messageText.color = color;
        }

        private void SetMessageVisible(bool visible)
        {
            if (_messageText == null) return;
            _messageText.gameObject.SetActive(visible);
        }
    }
}
