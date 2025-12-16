using System;
using Cysharp.Threading.Tasks;
using TienLen.Application;
using TienLen.Domain.Services;
using UnityEngine;
using UnityEngine.SceneManagement; // Direct scene management
using UnityEngine.UI;
using VContainer;
using TMPro;

namespace TienLen.Presentation
{
    /// <summary>
    /// Handles Home screen UX: Play/ Quit buttons and matchmaking status.
    /// Assumes user is already authenticated (via Bootstrap).
    /// Manages transition to GameRoom directly.
    /// </summary>
    public sealed class HomeUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject contentRoot; // Optional: To hide everything but keep script running
        
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

            // Listen for scene unloads to show Home again when GameRoom closes
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void Start()
        {
            bool isReady = _authService != null && _authService.IsAuthenticated;
            SetPlayInteractable(isReady);
            if (statusText) statusText.text = "";
        }

        private void OnDestroy()
        {
            if (_authService != null)
            {
                _authService.OnAuthenticated -= OnAuthComplete;
                _authService.OnAuthenticationFailed -= OnAuthFailed;
            }
            
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
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
                
                // Load GameRoom Additively
                await SceneManager.LoadSceneAsync("GameRoom", LoadSceneMode.Additive);
                
                // Hide Home Screen
                SetHomeUIVisibility(false);
            }
            catch (Exception ex)
            {
                SetMatchmakingState(false, "");
                Debug.LogError($"Failed to find match: {ex.Message}");
                if (statusText) statusText.text = "Match failed. Try again.";
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == "GameRoom")
            {
                Debug.Log("HomeUIController: GameRoom unloaded. Showing Home.");
                SetHomeUIVisibility(true);
                // Reset status
                if (statusText) statusText.text = "";
                SetPlayInteractable(true);
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
            if (contentRoot != null)
            {
                contentRoot.SetActive(isVisible);
            }
            else
            {
                // Fallback: If contentRoot is not assigned, disable/enable all direct children.
                // Note: Disabling the main GameObject of the HomeUIController would prevent
                // this script from receiving scene events (like OnSceneUnloaded),
                // which is why using a contentRoot or iterating children is necessary.
                foreach (Transform child in transform)
                {
                    child.gameObject.SetActive(isVisible);
                }
            }
        }
    }
}
