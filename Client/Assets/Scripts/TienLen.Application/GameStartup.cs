using System.Threading.Tasks;
using TienLen.Domain.Services;
using VContainer.Unity;
using UnityEngine;

namespace TienLen.Application
{
    /// <summary>
    /// Application entry point responsible for bootstrapping backend authentication on load.
    /// </summary>
    public class GameStartup : IStartable
    {
        private readonly IAuthenticationService _authService;

        public GameStartup(IAuthenticationService authService)
        {
            _authService = authService;
        }

        public void Start()
        {
            _ = StartAsync();
        }

        private async Task StartAsync()
        {
            Debug.Log("GameStartup: Starting application flow...");

            try
            {
                await _authService.LoginAsync();
                Debug.Log("GameStartup: Auth complete. Ready to load next scene or enable UI.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"GameStartup: Authentication failed - {ex.Message}");
            }
        }
    }
}
