using System;
using Cysharp.Threading.Tasks;
using TienLen.Domain.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace TienLen.Presentation
{
    public class BootstrapUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject loadingScreenRoot;
        [SerializeField] private Slider progressBar;

        private IAuthenticationService _authService;

        [Inject]
        public void Construct(IAuthenticationService authService)
        {
            _authService = authService;
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
                Debug.Log("Bootstrap: Logging in...");
                UpdateProgress(0.3f);
                
                await _authService.LoginAsync();
                
                Debug.Log("Bootstrap: Login successful.");
                UpdateProgress(0.8f);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Bootstrap: Login failed: {ex.Message}");
                // In a real app, handle retry here
                return;
            }

            // 2. Load Home Scene
            Debug.Log("Bootstrap: Loading Home scene...");
            await SceneManager.LoadSceneAsync("Home", LoadSceneMode.Additive);
            UpdateProgress(1.0f);

            // 3. Hide Loading Screen
            await UniTask.Delay(500);
            
            if (loadingScreenRoot) loadingScreenRoot.SetActive(false);
            
            Debug.Log("Bootstrap: Initialization complete.");
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