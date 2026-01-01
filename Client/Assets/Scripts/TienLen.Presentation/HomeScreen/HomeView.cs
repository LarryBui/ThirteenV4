using System;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using TMPro;

namespace TienLen.Presentation
{
    /// <summary>
    /// View for the Home screen.
    /// Handles UI references and events, communicating with services.
    /// </summary>
    public sealed class HomeView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button createVipTableButton;
        [SerializeField] private Button powerUpButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TMP_Text statusText;

        [SerializeField] private GameObject contentRoot; 
        
        private IAuthenticationService _authService;
        private TienLenMatchHandler _matchHandler;
        private LifetimeScope _currentScope; 
        private ILogger<HomeView> _logger;

        [Inject]
        public void Construct(
            IAuthenticationService authService,
            TienLenMatchHandler matchHandler,
            LifetimeScope currentScope,
            ILogger<HomeView> logger)
        {
            _authService = authService;
            _matchHandler = matchHandler;
            _currentScope = currentScope;
            _logger = logger ?? NullLogger<HomeView>.Instance;
        }

        private void Awake()
        {
            playButton?.onClick.AddListener(HandlePlayClicked);
            createVipTableButton?.onClick.AddListener(HandleCreateVipTableClicked);
            powerUpButton?.onClick.AddListener(HandlePowerUpClicked);
            quitButton?.onClick.AddListener(HandleQuitClicked);

            if (_authService != null)
            {
                _authService.OnAuthenticated += OnAuthComplete;
                _authService.OnAuthenticationFailed += OnAuthFailed;
            }

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

            SetPlayInteractable(false);
            if (statusText) statusText.text = "Searching for Casual match...";

            try
            {
                // Explicitly request Casual match
                await _matchHandler.FindAndJoinMatchAsync((int)Tienlen.V1.MatchType.Casual);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find casual match.");
                if (statusText) statusText.text = "Error finding match.";
                SetPlayInteractable(true);
            }
        }

        private async void HandleCreateVipTableClicked()
        {
            if (_matchHandler == null) return;

            SetPlayInteractable(false);
            if (statusText) statusText.text = "Creating VIP table...";

            try
            {
                // Explicitly request VIP match
                await _matchHandler.FindAndJoinMatchAsync((int)Tienlen.V1.MatchType.Vip);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to join VIP match.");
                if (statusText) statusText.text = "VIP Access Required or Error.";
                SetPlayInteractable(true);
            }
        }

        private void HandlePowerUpClicked()
        {
            _logger.LogInformation("Power-up clicked.");
        }

        private void HandleQuitClicked()
        {
            _logger.LogInformation("Quit clicked.");
            UnityEngine.Application.Quit();
        }

        private void OnAuthComplete()
        {
            SetPlayInteractable(true);
            if (statusText) statusText.text = "Connected.";
        }

        private void OnAuthFailed(string message)
        {
            SetPlayInteractable(false);
            if (statusText) statusText.text = $"Auth Failed: {message}";
        }

        private void SetPlayInteractable(bool interactable)
        {
            if (playButton) playButton.interactable = interactable;
            if (createVipTableButton) createVipTableButton.interactable = interactable;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == "GameRoom" || scene.name == "VIPGameRoom")
            {
                Show();
                SetPlayInteractable(true);
                if (statusText) statusText.text = "";
            }
        }

        public void Hide()
        {
            if (contentRoot)
            {
                contentRoot.SetActive(false);
            }
            else
            {
                _logger.LogError("HomeView: contentRoot is not assigned. Cannot set UI visibility.");
            }
        }

        public void Show()
        {
            if (contentRoot) contentRoot.SetActive(true);
        }
    }
}