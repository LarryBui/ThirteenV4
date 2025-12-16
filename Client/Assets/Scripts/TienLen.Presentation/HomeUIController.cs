using System;
using Cysharp.Threading.Tasks;
using TienLen.Application;
using TienLen.Domain.Services;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;

namespace TienLen.Presentation
{
    /// <summary>
    /// Handles Home screen UX: Play/ Quit buttons and matchmaking status.
    /// Assumes user is already authenticated (via Bootstrap).
    /// </summary>
    public sealed class HomeUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TMP_Text statusText; // Kept to display matchmaking status
        
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

            // Subscribe to authentication events for resilience.
            // Even though initial authentication happens in Bootstrap,
            // these events handle cases like session expiry or network disconnection
            // while on the Home screen, allowing the UI to react by enabling/disabling
            // the play button accordingly.
            if (_authService != null)
            {
                _authService.OnAuthenticated += OnAuthComplete;
                _authService.OnAuthenticationFailed += OnAuthFailed;
            }
        }

        private void Start()
        {
            // Initial state check: Assumes authentication is complete from Bootstrap.
            bool isReady = _authService != null && _authService.IsAuthenticated;
            SetPlayInteractable(isReady);
            
            // Clear status text at start
            if (statusText) statusText.text = "";
        }

        private void OnDestroy()
        {
            // Unsubscribe from events to prevent memory leaks.
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

            SetMatchmakingState(true, "Finding Match...");

            try
            {
                await _matchHandler.FindAndJoinMatchAsync();
                
                SetMatchmakingState(false, "Match Found!");
                await _sceneNavigator.LoadGameRoomAsync();
            }
            catch (Exception ex)
            {
                SetMatchmakingState(false, "");
                Debug.LogError($"Failed to find match: {ex.Message}");
                if (statusText) statusText.text = "Match failed. Try again.";
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

        private void SetMatchmakingState(bool isSearching, string message)
        {
            if (statusText) statusText.text = message;
            
            // Disable buttons while searching
            if (playButton) playButton.interactable = !isSearching;
            if (quitButton) quitButton.interactable = !isSearching;
        }

        /// <summary>
        /// Called when authentication is successfully completed (e.g., initial login or re-authentication after disconnect).
        /// Enables the play button to allow matchmaking.
        /// </summary>
        private void OnAuthComplete()
        {
            SetPlayInteractable(true);
        }

        /// <summary>
        /// Called when authentication fails (e.g., session expired, network issues).
        /// Disables the play button to prevent attempting matchmaking when unauthenticated.
        /// </summary>
        /// <param name="error">The error message from the authentication failure.</param>
        private void OnAuthFailed(string error)
        {
            Debug.LogError($"Authentication failed on Home screen: {error}");
            SetPlayInteractable(false);
            if (statusText) statusText.text = "Authentication failed. Please restart.";
        }

        private void SetPlayInteractable(bool interactable)
        {
            if (playButton) playButton.interactable = interactable;
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
