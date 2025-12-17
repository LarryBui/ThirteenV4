using System;
using Cysharp.Threading.Tasks;
using TienLen.Application; // Updated
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity; // Required for LifetimeScope

namespace TienLen.Presentation.BootstrapScreen
{
    public class BootstrapUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject loadingScreenRoot;
        [SerializeField] private Slider progressBar;

        private IAuthenticationService _authService;
        private LifetimeScope _parentLifetimeScope; // Inject the current scope

        [Inject]
        public void Construct(IAuthenticationService authService, LifetimeScope parentLifetimeScope)
        {
            _authService = authService;
            _parentLifetimeScope = parentLifetimeScope; // Store the parent scope
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
                Debug.LogError($"Bootstrap: Login failed: {ex.Message}");
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
