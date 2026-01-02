using System;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application;
using TienLen.Application.Errors;
using TienLen.Presentation.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace TienLen.Presentation.HomeScreen.Presenters
{
    /// <summary>
    /// Presenter for the Home Screen.
    /// Orchestrates interactions between the View (via events) and application services (Auth, MatchHandler).
    /// </summary>
    public sealed class HomePresenter : IDisposable
    {
        private readonly IAuthenticationService _authService;
        private readonly TienLenMatchHandler _matchHandler;
        private readonly ILogger<HomePresenter> _logger;
        private readonly LifetimeScope _scope;
        private readonly ErrorSceneState _errorSceneState;

        public event Action<bool> OnPlayInteractableChanged;
        public event Action<string> OnStatusTextChanged;
        public event Action OnHideViewRequested;
        public event Action OnShowViewRequested;

        public HomePresenter(
            IAuthenticationService authService,
            TienLenMatchHandler matchHandler,
            LifetimeScope scope,
            ErrorSceneState errorSceneState,
            ILogger<HomePresenter> logger)
        {
            _authService = authService;
            _matchHandler = matchHandler;
            _scope = scope;
            _errorSceneState = errorSceneState ?? throw new ArgumentNullException(nameof(errorSceneState));
            _logger = logger ?? NullLogger<HomePresenter>.Instance;

            SubscribeToServices();
        }

        private void SubscribeToServices()
        {
            if (_authService != null)
            {
                _authService.OnAuthenticated += HandleAuthComplete;
                _authService.OnAuthenticationFailed += HandleAuthFailed;
            }
        }

        public void Initialize()
        {
            bool isReady = _authService != null && _authService.IsAuthenticated;
            OnPlayInteractableChanged?.Invoke(isReady);
            OnStatusTextChanged?.Invoke(isReady ? "Connected." : "");
        }

        public void Dispose()
        {
            if (_authService != null)
            {
                _authService.OnAuthenticated -= HandleAuthComplete;
                _authService.OnAuthenticationFailed -= HandleAuthFailed;
            }
        }

        public async void JoinCasualMatch()
        {
            if (_matchHandler == null) return;

            OnPlayInteractableChanged?.Invoke(false);
            OnStatusTextChanged?.Invoke("Searching for Casual match...");

            try
            {
                await _matchHandler.FindAndJoinMatchAsync((int)global::Tienlen.V1.MatchType.Casual);
                
                // Load GameRoom Additively with Parent Scope
                using (LifetimeScope.EnqueueParent(_scope))
                {
                    await SceneManager.LoadSceneAsync("GameRoom", LoadSceneMode.Additive);
                }
                
                OnHideViewRequested?.Invoke();
            }
            catch (TienLenAppException ex)
            {
                await HandleAppExceptionAsync(ex, "Failed to find casual match.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find casual match.");
                OnStatusTextChanged?.Invoke("Error finding match.");
                OnPlayInteractableChanged?.Invoke(true);
            }
        }

        public async void JoinVipMatch()
        {
            
            if (_matchHandler == null) return;

            OnPlayInteractableChanged?.Invoke(false);
            OnStatusTextChanged?.Invoke("Creating VIP table...");

            try
            {
                await _matchHandler.FindAndJoinMatchAsync((int)global::Tienlen.V1.MatchType.Vip);
                
                // Load VIPGameRoom Additively with Parent Scope
                using (LifetimeScope.EnqueueParent(_scope))
                {
                    await SceneManager.LoadSceneAsync("VIPGameRoom", LoadSceneMode.Additive);
                }

                OnHideViewRequested?.Invoke();
            }
            catch (TienLenAppException ex)
            {
                _errorSceneState.Set(ex.Message, SceneManager.GetActiveScene().name);
                using (LifetimeScope.EnqueueParent(_scope))
                {
                    await SceneManager.LoadSceneAsync("ErrorScene", LoadSceneMode.Additive);
                }
                _logger.LogError(ex, " custom Exception. Failed to join VIP match.");
                // await HandleAppExceptionAsync(ex, "Failed to join VIP match.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to join VIP match.");
                OnPlayInteractableChanged?.Invoke(true);
            }
        }
        public void QuitGame()
        {
            _logger.LogInformation("Quit clicked.");
            UnityEngine.Application.Quit();
        }

        public async void OpenDailyScreen()
        {
            _logger.LogInformation("Opening Daily Screen...");
            
            // Load Daily Additively with Parent Scope
            using (LifetimeScope.EnqueueParent(_scope))
            {
                await SceneManager.LoadSceneAsync("Daily", LoadSceneMode.Additive);
            }
        }

        public void HandleSceneUnloaded(string sceneName)
        {
            if (sceneName == "GameRoom" || sceneName == "VIPGameRoom")
            {
                OnShowViewRequested?.Invoke();
                OnPlayInteractableChanged?.Invoke(true);
                OnStatusTextChanged?.Invoke("Connected.");
            }
        }

        private void HandleAuthComplete()
        {
            OnPlayInteractableChanged?.Invoke(true);
            OnStatusTextChanged?.Invoke("Connected.");
        }

        private void HandleAuthFailed(string message)
        {
            OnPlayInteractableChanged?.Invoke(false);
            OnStatusTextChanged?.Invoke($"Auth Failed: {message}");
        }

        private async UniTask HandleAppExceptionAsync(TienLenAppException ex, string logMessage)
        {
            if (ex == null) return;


            if (ex.Outcome == ErrorOutcome.ErrorScene)
            {
                 _errorSceneState.Set(ex.Message, SceneManager.GetActiveScene().name);
                using (LifetimeScope.EnqueueParent(_scope))
                {
                    _logger.LogWarning(logMessage);
                    await SceneManager.LoadSceneAsync("ErrorScene", LoadSceneMode.Additive);
                }
                // SceneManager.SetActiveScene(SceneManager.GetSceneByName("ErrorScene"));
                OnHideViewRequested?.Invoke();
                // OnPlayInteractableChanged?.Invoke(true);
                return;
            }

            OnStatusTextChanged?.Invoke(ex.Message);
            OnPlayInteractableChanged?.Invoke(true);
        }
    }
}
