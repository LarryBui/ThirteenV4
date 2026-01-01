using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace TienLen.Presentation.BootstrapScreen
{
    /// <summary>
    /// View for the Bootstrap screen.
    /// Handles the initial loading sequence and transition to the Home scene.
    /// </summary>
    public class BootstrapView : MonoBehaviour
    {
        private IAuthenticationService _authService;
        private ILogger<BootstrapView> _logger;

        [Inject]
        public void Construct(
            IAuthenticationService authService,
            ILogger<BootstrapView> logger)
        {
            _authService = authService;
            _logger = logger ?? NullLogger<BootstrapView>.Instance;
        }

        private async void Start()
        {
            _logger.LogInformation("Bootstrap started.");

            try
            {
                // 1. Initial Authentication
                await _authService.LoginAsync();
                _logger.LogInformation("Authentication successful. UserId: {UserId}", _authService.CurrentUserId);

                // 2. Load Home Scene
                SceneManager.LoadScene("Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bootstrap failed during authentication.");
                // In a real app, show error UI here
            }
        }
    }
}
