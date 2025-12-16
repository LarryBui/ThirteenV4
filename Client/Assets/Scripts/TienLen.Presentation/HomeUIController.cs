using System;
using Cysharp.Threading.Tasks;
using TienLen.Application;
using TienLen.Domain.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using TMPro; // Added for TextMeshPro components

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
        [SerializeField] private Button playButton; // Reverted to standard Button
        [SerializeField] private Button quitButton; // Reverted to standard Button
        [SerializeField] private GameObject connectingOverlay;
        [SerializeField] private TMP_Text statusText; // Kept as TMP_Text
        [SerializeField] private Slider progressBar;

        private bool _isConnecting;
        private DateTime _connectStartUtc;
        private const float MinimumConnectSeconds = 2f; 
        
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
            SetConnecting(true, "Connecting...");
            SetProgress(0.1f);
            _connectStartUtc = DateTime.UtcNow;

            if (_authService != null && _authService.IsAuthenticated)
            {
                OnAuthComplete();
            }
            else if (_authService != null)
            {
                // Auto-login logic
                LoginAndInitialize().Forget();
            }
            else
            {
                if (playButton) playButton.interactable = false;
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

        private async UniTask LoginAndInitialize()
        {
            try
            {
                await _authService.LoginAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"HomeUIController: Login failed - {ex.Message}");
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
            _connectStartUtc = DateTime.UtcNow;
            playButton.interactable = false;

            try
            {
                await _matchHandler.FindAndJoinMatchAsync();
                SetConnecting(false, "Match Found!");
                
                // TODO: Revisit this in Step 4 for SceneNavigator
                SceneManager.LoadScene("GameRoom", LoadSceneMode.Additive); 
            }
            catch (Exception ex)
            {
                SetConnecting(false, $"Failed to find match: {ex.Message}");
                Debug.LogError($"Failed to find and join match: {ex.Message}");
                playButton.interactable = true;
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
            if (playButton) playButton.interactable = !connecting && (_authService?.IsAuthenticated ?? false);
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

        public void OnAuthComplete()
        {
            SetProgress(1f);
            HideConnectingAfterMinimumAsync(true, "").Forget();
            playButton.interactable = true;
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
            if (quitButton) quitButton.interactable = true;
        }
    }
}