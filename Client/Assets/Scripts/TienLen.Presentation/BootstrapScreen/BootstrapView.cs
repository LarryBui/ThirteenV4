using System;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application; // Updated
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity; // Required for LifetimeScope

namespace TienLen.Presentation.BootstrapScreen
{
    public class BootstrapView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject loadingScreenRoot;
        [SerializeField] private Slider progressBar;

        private IAuthenticationService _authService;
        private LifetimeScope _parentLifetimeScope; // Inject the current scope
        private ILogger<BootstrapView> _logger;

        /// <summary>
        /// Injects required services for bootstrap initialization.
        /// </summary>
        /// <param name="authService">Authentication service used to login.</param>
        /// <param name="parentLifetimeScope">Parent lifetime scope for scene loading.</param>
        /// <param name="logger">Logger for bootstrap diagnostics.</param>
        [Inject]
        public void Construct(
            IAuthenticationService authService,
            LifetimeScope parentLifetimeScope,
            ILogger<BootstrapView> logger)
        {
            _authService = authService;
            _parentLifetimeScope = parentLifetimeScope; // Store the parent scope
            _logger = logger ?? NullLogger<BootstrapView>.Instance;
        }

        private void Start()
        {
            if (loadingScreenRoot) loadingScreenRoot.SetActive(true);
            InitializeGameAsync().Forget();
        }

        private async UniTask InitializeGameAsync()
        {
            UpdateProgress(0.1f);

            // 1. Authenticate
            try
            {
                UpdateProgress(0.3f);
                
                await _authService.LoginAsync();
                
                UpdateProgress(0.8f);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bootstrap: Login failed.");
                // In a real app, handle retry here
                return;
            }

            // 2. Load Home Scene
            // Explicitly parent the new Home scene's LifetimeScope to the current (Game) scope
            using (LifetimeScope.EnqueueParent(_parentLifetimeScope))
            {
                await SceneManager.LoadSceneAsync("Home", LoadSceneMode.Additive);
            }

            
            UpdateProgress(1.0f);

            // 3. Hide Loading Screen
            await UniTask.Delay(500);
            
            if (loadingScreenRoot) loadingScreenRoot.SetActive(false);
        }

        private void UpdateProgress(float progress)
        {
            if (progressBar)
            {
                progressBar.value = Mathf.Clamp01(progress);
            }
        }
    }
}
