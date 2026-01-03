using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using TMPro;
using TienLen.Presentation.HomeScreen.Presenters;

namespace TienLen.Presentation.HomeScreen.Views
{
    /// <summary>
    /// Passive View for the Home screen.
    /// Handles only UI rendering and input forwarding. Logic is delegated to HomePresenter.
    /// </summary>
    public sealed class HomeView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button createVipTableButton;
        [SerializeField] private Button powerUpButton;
        [SerializeField] private Button dailyButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject contentRoot;

        private HomePresenter _presenter;

        [Inject]
        public void Construct(HomePresenter presenter)
        {
            _presenter = presenter;
        }

        private void Awake()
        {
            playButton?.onClick.AddListener(() => _presenter?.JoinCasualMatch());
            createVipTableButton?.onClick.AddListener(() => _presenter?.JoinVipMatch());
            quitButton?.onClick.AddListener(() => _presenter?.QuitGame());
            dailyButton?.onClick.AddListener(() => _presenter?.OpenDailyScreen());
            
            // PowerUp placeholder
            powerUpButton?.onClick.AddListener(() => Debug.Log("PowerUp Clicked"));

            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void Start()
        {
            if (_presenter == null)
            {
                Debug.LogError("[HomeView] HomePresenter not injected.");
                return;
            }

            // Subscribe to Presenter Events
            _presenter.OnPlayInteractableChanged += SetPlayInteractable;
            _presenter.OnHideViewRequested += Hide;
            _presenter.OnShowViewRequested += Show;

            // Initialize Presenter State
            _presenter.Initialize();
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            
            if (_presenter != null)
            {
                _presenter.OnPlayInteractableChanged -= SetPlayInteractable;
                _presenter.OnHideViewRequested -= Hide;
                _presenter.OnShowViewRequested -= Show;
                _presenter.Dispose();
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            _presenter?.HandleSceneUnloaded(scene.name);
        }

        // --- UI Manipulation Methods ---

        private void SetPlayInteractable(bool interactable)
        {
            if (playButton) playButton.interactable = interactable;
            if (createVipTableButton) createVipTableButton.interactable = interactable;
        }

        private void Hide()
        {
            if (contentRoot) contentRoot.SetActive(false);
        }

        private void Show()
        {
            if (contentRoot) contentRoot.SetActive(true);
        }
    }
}