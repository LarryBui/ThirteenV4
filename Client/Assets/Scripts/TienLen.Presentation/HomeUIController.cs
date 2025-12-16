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
        private ISceneNavigator _sceneNavigator;

        [Inject]
        public void Construct(IAuthenticationService authService, TienLenMatchHandler matchHandler, ISceneNavigator sceneNavigator)
        {
            _authService = authService;
            _matchHandler = matchHandler;
            _sceneNavigator = sceneNavigator;
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
            // By the time Home loads, authentication should be complete via BootstrapFlow.
            if (_authService != null && _authService.IsAuthenticated)
            {
                SetConnecting(false, "");
                if (playButton) playButton.interactable = true;
            }
            else
            {
                // Fallback or Error state
                Debug.LogWarning("HomeUIController: Auth service not ready or not authenticated.");
                if (playButton) playButton.interactable = false;
                SetConnecting(false, "Auth Failed");
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
            _connectStartUtc = DateTime.UtcNow;
            if (playButton) playButton.interactable = false;

            try
            {
                await _matchHandler.FindAndJoinMatchAsync();
                SetConnecting(false, "Match Found!");
                
                // Use SceneNavigator to load GameRoom additively
                await _sceneNavigator.LoadGameRoomAsync();
            }
            catch (Exception ex)
            {
                SetConnecting(false, $"Failed to find match: {ex.Message}");
                Debug.LogError($"Failed to find and join match: {ex.Message}");
                if (playButton) playButton.interactable = true;
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
            // Optional: Handle re-auth if connection lost/regained
            if (playButton) playButton.interactable = true;
        }

        public void OnAuthFailed(string error)
        {
            if (playButton) playButton.interactable = false;
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

        public void SetHomeUIVisibility(bool isVisible)
        {
            if (gameObject.activeSelf != isVisible)
            {
                gameObject.SetActive(isVisible);
            }
        }
    }
}