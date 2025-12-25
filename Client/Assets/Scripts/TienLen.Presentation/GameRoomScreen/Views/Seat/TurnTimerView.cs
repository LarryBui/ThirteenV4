using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TienLen.Presentation.GameRoomScreen.Views
{
    /// <summary>
    /// A standalone visual component for the turn timer.
    /// Handles the countdown logic and UI updates locally.
    /// </summary>
    public sealed class TurnTimerView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _root;
        [SerializeField] private GameObject _countdownRoot;
        [SerializeField] private TMP_Text _countdownText;
        [SerializeField] private Image _progressImage;

        private CancellationTokenSource _cts;

        public void Play(float duration)
        {
            Stop();
            
            if (_root != null) _root.SetActive(true);
            if (_countdownRoot != null) _countdownRoot.SetActive(true);
            
            _cts = new CancellationTokenSource();
            RunTimerAsync(duration, _cts.Token).Forget();
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (_root != null) _root.SetActive(false);
            if (_countdownRoot != null) _countdownRoot.SetActive(false);
        }

        private async UniTaskVoid RunTimerAsync(float duration, CancellationToken token)
        {
            float remaining = duration;
            while (remaining >= 0 && !token.IsCancellationRequested)
            {
                if (_countdownText != null) 
                    _countdownText.text = Mathf.CeilToInt(remaining).ToString();
                
                if (_progressImage != null)
                    _progressImage.fillAmount = remaining / duration;

                await UniTask.Yield(PlayerLoopTiming.Update, token);
                remaining -= Time.deltaTime;
            }

            if (!token.IsCancellationRequested)
            {
                Stop();
            }
        }

        private void OnDestroy()
        {
            Stop();
        }
    }
}
