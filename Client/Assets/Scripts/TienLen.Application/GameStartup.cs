using System.Threading.Tasks;
using TienLen.Domain.Services;
using VContainer.Unity;
using UnityEngine;

namespace TienLen.Application
{
    public class GameStartup : IStartable
    {
        private readonly IAuthenticationService _authService;

        public GameStartup(IAuthenticationService authService)
        {
            _authService = authService;
        }

        public void Start()
        {
            // Fire and forget the startup sequence
            _ = StartAsync();
        }

        private Task StartAsync()
        {
            Debug.Log("GameStartup: Starting application flow...");
            
            // This is the "Main" entry point.
            // await _authService.AuthenticateAndConnectAsync();
            return null;
            Debug.Log("GameStartup: Auth complete. Ready to load next scene or enable UI.");
        }
    }
}

