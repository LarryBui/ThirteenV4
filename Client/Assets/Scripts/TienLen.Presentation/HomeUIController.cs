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

            if (_authService != null)
            {
                _authService.OnAuthenticated += OnAuthComplete;
                _authService.OnAuthenticationFailed += OnAuthFailed;
            }
        }

        private void Start()
        {
            // Initial state check
            bool isReady = _authService != null && _authService.IsAuthenticated;
            SetPlayInteractable(isReady);
            
            // Clear status text at start
            if (statusText) statusText.text = "";
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
            // No connectingOverlay, just update status text and button interactability
            if (statusText) statusText.text = message;
            
            // Disable buttons while searching
            if (playButton) playButton.interactable = !isSearching;
            if (quitButton) quitButton.interactable = !isSearching;
        }

        private void OnAuthComplete()
        {
            SetPlayInteractable(true);
        }

        private void OnAuthFailed(string error)
        {
            SetPlayInteractable(false);
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