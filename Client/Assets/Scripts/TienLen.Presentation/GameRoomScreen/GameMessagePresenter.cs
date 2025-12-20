using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace TienLen.Presentation.GameRoomScreen
{
    /// <summary>
    /// Displays transient game messages and fades them out after a delay.
    /// </summary>
    public sealed class GameMessagePresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text _messageText;

        [Header("Timing")]
        [SerializeField] private float _autoHideSeconds = 2.5f;
        [SerializeField] private float _fadeSeconds = 2f;

        private int _messageToken;

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
        /// Clears the current message and hides the label.
        /// </summary>
        public void Clear()
        {
            _messageToken++;
            SetMessageVisible(false);
            SetMessageAlpha(0f);
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

            AutoHideAsync(token).Forget();
        }

        private async UniTaskVoid AutoHideAsync(int token)
        {
            if (_autoHideSeconds > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_autoHideSeconds));
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
                Clear();
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

            Clear();
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
