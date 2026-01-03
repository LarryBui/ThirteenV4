using System;
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
        private readonly GlobalMessageHandler _globalMessageHandler;
        private Action _retryAction;

        public event Action<bool> OnPlayInteractableChanged;
        public event Action<string> OnStatusTextChanged;
        public event Action OnHideViewRequested;
        public event Action OnShowViewRequested;

        public HomePresenter(
            IAuthenticationService authService,
            TienLenMatchHandler matchHandler,
            LifetimeScope scope,
            GlobalMessageHandler globalMessageHandler,
            ILogger<HomePresenter> logger)
        {
            _authService = authService;
            _matchHandler = matchHandler;
            _scope = scope;
            _globalMessageHandler = globalMessageHandler ?? throw new ArgumentNullException(nameof(globalMessageHandler));
            _logger = logger ?? NullLogger<HomePresenter>.Instance;

            _globalMessageHandler.RetryRequested += HandleRetryRequested;
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

            _globalMessageHandler.RetryRequested -= HandleRetryRequested;
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

                _retryAction = null;
                OnHideViewRequested?.Invoke();
            }
            catch (TienLenAppException ex)
            {
                HandleAppException(ex, "Matchmaking", JoinCasualMatch);
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

                _retryAction = null;
                OnHideViewRequested?.Invoke();
            }
            catch (TienLenAppException ex)
            {
                HandleAppException(ex, "VIP Match", JoinVipMatch);
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

        private void HandleRetryRequested(UiNotification _)
        {
            if (_retryAction == null) return;
            var action = _retryAction;
            _retryAction = null;
            action.Invoke();
        }

        private void HandleAppException(TienLenAppException ex, string title, Action retryAction)
        {
            if (ex == null) return;

            _retryAction = retryAction;
            _logger.LogWarning(ex, "{Title} failed with app error.", title);
            var notification = UiNotificationRouter.FromAppException(ex, title);
            _globalMessageHandler.Publish(notification);
            OnPlayInteractableChanged?.Invoke(true);
        }
    }
}
