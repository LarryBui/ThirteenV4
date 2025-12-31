using System;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application; // Updated
using UnityEngine;
using UnityEngine.SceneManagement; // Direct scene management
using UnityEngine.UI;
using VContainer;
using VContainer.Unity; // Required for LifetimeScope
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
        [SerializeField] private Button createVipTableButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject contentRoot; // Mandatory: Used to hide/show the entire Home UI
        
        private IAuthenticationService _authService;
        private TienLenMatchHandler _matchHandler;
        private LifetimeScope _currentScope; // Inject the current scope (HomeLifetimeScope)
        private ILogger<HomeUIController> _logger;

        /// <summary>
        /// Injects required services for the Home screen.
        /// </summary>
        /// <param name="authService">Authentication service used to check readiness.</param>
        /// <param name="matchHandler">Match handler used to create/join matches.</param>
        /// <param name="currentScope">Lifetime scope for the Home scene.</param>
        /// <param name="logger">Logger for Home screen diagnostics.</param>
        [Inject]
        public void Construct(
            IAuthenticationService authService,
            TienLenMatchHandler matchHandler,
            LifetimeScope currentScope,
            ILogger<HomeUIController> logger)
        {
            _authService = authService;
            _matchHandler = matchHandler;
            _currentScope = currentScope;
            _logger = logger ?? NullLogger<HomeUIController>.Instance;
        }

        private void Awake()
        {
            playButton?.onClick.AddListener(HandlePlayClicked);
            createVipTableButton?.onClick.AddListener(HandleCreateVipTableClicked);
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
                _logger.LogError("Match handler not initialized.");
                return;
            }

            SetMatchmakingState(true, "Finding Match...");

            try
            {
                await _matchHandler.FindAndJoinMatchAsync();
                
                SetMatchmakingState(false, "Match Found!");
                
                // Load GameRoom Additively, parenting its scope to the current (Home) scope
                // This allows GameRoom to inherit services from Home (and transitively from Game/Global)
                using (LifetimeScope.EnqueueParent(_currentScope))
                {
                    await SceneManager.LoadSceneAsync("GameRoom", LoadSceneMode.Additive);
                }
                
                // Hide Home Screen UI
                SetHomeUIVisibility(false);
            }
            catch (Exception ex)
            {
                SetMatchmakingState(false, "");
                _logger.LogError(ex, "Failed to find match.");
                if (statusText) statusText.text = "Match failed. Try again.";
            }
        }

        private async void HandleCreateVipTableClicked()
        {

            //todo: this is for testing
            using (LifetimeScope.EnqueueParent(_currentScope))
                {
                    await SceneManager.LoadSceneAsync("VIPGameRoom", LoadSceneMode.Additive);
                }

                return;
            if (_matchHandler == null)
            {
                _logger.LogError("Match handler not initialized.");
                return;
            }

            SetMatchmakingState(true, "Creating VIP Table...");

            try
            {
                await _matchHandler.FindAndJoinMatchAsync();
                
                SetMatchmakingState(false, "VIP Table Created!");
                
                using (LifetimeScope.EnqueueParent(_currentScope))
                {
                    await SceneManager.LoadSceneAsync("VIPGameRoom", LoadSceneMode.Additive);
                }
                
                SetHomeUIVisibility(false);
            }
            catch (Exception ex)
            {
                SetMatchmakingState(false, "");
                _logger.LogError(ex, "Failed to create VIP table.");
                if (statusText) statusText.text = "VIP table creation failed. Try again.";
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == "GameRoom" || scene.name == "VIPGameRoom")
            {
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
            UnityEngine.Application.Quit();
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
            _logger.LogError("Authentication failed on Home screen. error={error}", error);
            SetPlayInteractable(false);
            if (statusText) statusText.text = "Authentication failed. Please restart.";
        }

        private void SetPlayInteractable(bool interactable)
        {
            if (playButton) playButton.interactable = interactable;
        }

        /// <summary>
        /// Sets the visibility of the Home UI. Requires 'contentRoot' to be assigned in the Inspector.
        /// </summary>
        /// <param name="isVisible">True to show the UI, false to hide.</param>
        public void SetHomeUIVisibility(bool isVisible)
        {
            if (contentRoot == null)
            {
                _logger.LogError("HomeUIController: contentRoot is not assigned. Cannot set UI visibility.");
                return;
            }
            contentRoot.SetActive(isVisible);
        }
    }
}
