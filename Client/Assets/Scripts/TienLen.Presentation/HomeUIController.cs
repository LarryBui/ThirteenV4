using System;
using Cysharp.Threading.Tasks;
using TienLen.Application;
using TienLen.Domain.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        private bool _isConnecting;
        private DateTime _connectStartUtc;
        private const float MinimumConnectSeconds = 2f; // Reduced for quicker feedback
        
        private IAuthenticationService _authService;
        private TienLenMatchHandler _matchHandler;

        [Inject]
        public void Construct(IAuthenticationService authService, TienLenMatchHandler matchHandler)
        {
            _authService = authService;
            _matchHandler = matchHandler;
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
            _connectStartUtc = DateTime.UtcNow; // Initialize start time

            // Check if already authenticated (race condition handling)
            if (_authService != null && _authService.IsAuthenticated)
            {
                OnAuthComplete();
            }
            else
            {
                // Ensure buttons are disabled if not authenticated yet
                playButton.interactable = false;
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
            if (_matchHandler == null)
            {
                Debug.LogError("Match Handler not initialized!");
                return;
            }

            SetConnecting(true, "Finding Match...");
            _connectStartUtc = DateTime.UtcNow; // Reset for match connection
            playButton.interactable = false; // Disable button immediately

            try
            {
                await _matchHandler.FindAndJoinMatchAsync();
                SetConnecting(false, "Match Found!");
                // Load GameRoom scene upon successful match join
                SceneManager.LoadScene("GameRoom"); 
            }
            catch (Exception ex)
            {
                SetConnecting(false, $"Failed to find match: {ex.Message}");
                Debug.LogError($"Failed to find and join match: {ex.Message}");
                playButton.interactable = true; // Re-enable button on failure
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
            if (playButton) playButton.interactable = !connecting && _authService.IsAuthenticated; // Only enable if authenticated
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
            playButton.interactable = true; // Enable play button after successful auth
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
            // playButton.interactable = success; // This line should not control play button state directly, OnAuthComplete/OnAuthFailed should
            if (quitButton) quitButton.interactable = true;
        }
    }
}
