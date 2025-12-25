using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace TienLen.Presentation.GameRoomScreen.Views
{
    /// <summary>
    /// Manages the visual lifecycle of a single speech bubble.
    /// Handles fading and auto-hiding.
    /// </summary>
    public sealed class SpeechBubbleView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Settings")]
        [SerializeField] private float _displayDuration = 3f;
        [SerializeField] private float _fadeDuration = 0.3f;

        private CancellationTokenSource _hideCts;

        private void Awake()
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 0;
            gameObject.SetActive(false);
        }

        public void Show(string text)
        {
            StopAllCoroutines(); // Reset any existing sequences
            _hideCts?.Cancel();
            _hideCts?.Dispose();
            _hideCts = new CancellationTokenSource();

            if (_messageText != null) _messageText.text = text;
            
            gameObject.SetActive(true);
            RunShowSequence(_hideCts.Token).Forget();
        }

        private async UniTaskVoid RunShowSequence(CancellationToken token)
        {
            // Fade In
            await Fade(0, 1, _fadeDuration, token);

            // Wait
            await UniTask.Delay(TimeSpan.FromSeconds(_displayDuration), cancellationToken: token);

            // Fade Out
            await Fade(1, 0, _fadeDuration, token);

            gameObject.SetActive(false);
        }

        private async UniTask Fade(float from, float to, float duration, CancellationToken token)
        {
            if (_canvasGroup == null) return;

            float elapsed = 0;
            while (elapsed < duration && !token.IsCancellationRequested)
            {
                _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                elapsed += Time.deltaTime;
                await UniTask.Yield(token);
            }
            _canvasGroup.alpha = to;
        }

        private void OnDestroy()
        {
            _hideCts?.Cancel();
            _hideCts?.Dispose();
        }
    }
}