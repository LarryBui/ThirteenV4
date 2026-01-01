using System;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application;
using UnityEngine;

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

        public event Action<bool> OnPlayInteractableChanged;
        public event Action<string> OnStatusTextChanged;
        public event Action OnHideViewRequested;
        public event Action OnShowViewRequested;

        public HomePresenter(
            IAuthenticationService authService,
            TienLenMatchHandler matchHandler,
            ILogger<HomePresenter> logger)
        {
            _authService = authService;
            _matchHandler = matchHandler;
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
                OnHideViewRequested?.Invoke();
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
                OnHideViewRequested?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to join VIP match.");
                OnStatusTextChanged?.Invoke("VIP Access Required or Error.");
                OnPlayInteractableChanged?.Invoke(true);
            }
        }

        public void QuitGame()
        {
            _logger.LogInformation("Quit clicked.");
            UnityEngine.Application.Quit();
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
    }
}
