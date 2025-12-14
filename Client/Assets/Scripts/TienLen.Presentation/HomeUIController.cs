using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TienLen.Presentation
{
    /// <summary>
    /// Handles Home screen UX: Play/ Quit buttons and connecting overlay.
    /// Wire Play to your Nakama quickmatch flow by injecting an async callback.
    /// </summary>
    public sealed class HomeUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject connectingOverlay;
        [SerializeField] private Text statusText;
        [SerializeField] private Slider progressBar;

        /// <summary>
        /// Assign this from a bootstrapper to perform the quickmatch/connect flow.
        /// It should return true on success, false on failure.
        /// </summary>
        public Func<Task<bool>> OnPlayAsync { get; set; }

        private bool _isConnecting;

        private void Awake()
        {
            playButton?.onClick.AddListener(HandlePlayClicked);
            quitButton?.onClick.AddListener(HandleQuitClicked);
            // Initial state: pretend we're connecting for 2 seconds.
            SetConnecting(true, "Connecting…");
            SetProgress(0f);
            StartCoroutine(InitialUnlock());
        }

        private async void HandlePlayClicked()
        {
            if (_isConnecting)
                return;

            SetConnecting(true, "Connecting…");
            SetProgress(0.15f);

            var ok = false;
            try
            {
                if (OnPlayAsync != null)
                {
                    ok = await OnPlayAsync.Invoke();
                }
                else
                {
                    statusText.text = "No OnPlayAsync handler assigned.";
                }
                SetProgress(ok ? 1f : 0f);
            }
            catch (Exception ex)
            {
                statusText.text = $"Error: {ex.Message}";
                SetProgress(0f);
            }
            finally
            {
                if (!ok)
                {
                    SetConnecting(false, ok ? "" : statusText.text);
                }
            }
        }

        private void HandleQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetConnecting(bool connecting, string message)
        {
            _isConnecting = connecting;
            if (playButton) playButton.interactable = !connecting;
            if (quitButton) quitButton.interactable = !connecting;
            if (connectingOverlay) connectingOverlay.SetActive(connecting);
            if (statusText) statusText.text = message ?? "";
        }

        private void SetProgress(float value)
        {
            if (progressBar)
            {
            progressBar.gameObject.SetActive(_isConnecting);
            progressBar.value = Mathf.Clamp01(value);
            }
        }

        private System.Collections.IEnumerator InitialUnlock()
        {
            // Simple splash/connecting delay.
            const float delaySeconds = 2f;
            var elapsed = 0f;
            while (elapsed < delaySeconds)
            {
                elapsed += Time.deltaTime;
                SetProgress(Mathf.Clamp01(elapsed / delaySeconds));
                yield return null;
            }

            SetProgress(1f);
            SetConnecting(false, "");
        }
    }
}
