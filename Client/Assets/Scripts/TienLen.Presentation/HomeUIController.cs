using System;
using Cysharp.Threading.Tasks;
using TienLen.Domain.Services;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace TienLen.Presentation
{
    /// <summary>
    /// Handles Home screen UX: Play/ Quit buttons and connecting overlay.
    /// Performs startup authentication via the injected auth service and unlocks controls when ready.
    /// Wire Play to your Nakama quickmatch flow by injecting an async callback.
    /// </summary>
    public sealed class HomeUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject connectingOverlay;
        [SerializeField] private Text statusText;
        [SerializeField] private Slider progressBar;

        /// <summary>
        /// Assign this from a bootstrapper to perform the quickmatch/connect flow.
        /// It should return true on success, false on failure.
        /// </summary>
        public Func<UniTask<bool>> OnPlayAsync { get; set; }

        private bool _isConnecting;
        private DateTime _connectStartUtc;
        private const float MinimumConnectSeconds = 5f;
        private IAuthenticationService _authService;

        [Inject]
        public void Construct(IAuthenticationService authService)
        {
            _authService = authService;
        }

        private void Awake()
        {
            playButton?.onClick.AddListener(HandlePlayClicked);
            quitButton?.onClick.AddListener(HandleQuitClicked);

            if (_authService != null)
            {
                _authService.OnAuthenticated += OnAuthComplete;
                _authService.OnAuthenticationFailed += OnAuthFailed;
            }
        }

        private void Start()
        {
            // Initial state: connecting. GameStartup will drive the actual logic.
            SetConnecting(true, "Connecting...");
            SetProgress(0.1f);

            // Check if already authenticated (race condition handling)
            if (_authService != null && _authService.IsAuthenticated)
            {
                OnAuthComplete();
            }
        }

        private void OnDestroy()
        {
            if (_authService != null)
            {
                _authService.OnAuthenticated -= OnAuthComplete;
                _authService.OnAuthenticationFailed -= OnAuthFailed;
            }
        }

        private async void HandlePlayClicked()
        {
            if (OnPlayAsync != null)
            {
                playButton.interactable = false;
                bool success = await OnPlayAsync();
                if (!success)
                {
                    playButton.interactable = true;
                }
            }
        }

        private void HandleQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetConnecting(bool connecting, string message)
        {
            _isConnecting = connecting;
            if (playButton) playButton.interactable = !connecting;
            if (quitButton) quitButton.interactable = !connecting;
            if (connectingOverlay) connectingOverlay.SetActive(connecting);
            if (statusText) statusText.text = message ?? "";
        }

        private void SetProgress(float value)
        {
            if (progressBar)
            {
                progressBar.gameObject.SetActive(_isConnecting);
                progressBar.value = Mathf.Clamp01(value);
            }
        }

        // ---- External control for auth/bootstrap phases ----
        public void ShowAuthProgress(float progress, string message = null)
        {
            SetConnecting(true, message ?? statusText?.text ?? "Connecting...");
            SetProgress(progress);
        }

        public void OnAuthComplete()
        {
            SetProgress(1f);
            HideConnectingAfterMinimumAsync(true, "").Forget();
        }

        public void OnAuthFailed(string error)
        {
            SetProgress(0f);
            HideConnectingAfterMinimumAsync(false, error).Forget();
        }

        private async UniTask HideConnectingAfterMinimumAsync(bool success, string message)
        {
            var elapsedSeconds = (float)(DateTime.UtcNow - _connectStartUtc).TotalSeconds;
            if (elapsedSeconds < MinimumConnectSeconds)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(MinimumConnectSeconds - elapsedSeconds));
            }

            _isConnecting = false;
            SetProgress(0f);
            if (connectingOverlay) connectingOverlay.SetActive(false);
            if (statusText) statusText.text = message ?? "";
            if (playButton) playButton.interactable = success;
            if (quitButton) quitButton.interactable = true;
        }
    }
}
