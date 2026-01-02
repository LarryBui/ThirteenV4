using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace TienLen.Presentation.ErrorScreen
{
    /// <summary>
    /// View for the Error Screen.
    /// Strictly handles UI binding and events, delegating logic to ErrorPresenter.
    /// </summary>
    public sealed class ErrorView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button backButton;
        [SerializeField] private Image errorIcon;

        private ErrorPresenter _presenter;

        [Inject]
        public void Construct(ErrorPresenter presenter)
        {
            _presenter = presenter;
        }

        private void Start()
        {
            if (_presenter == null)
            {
                Debug.LogError("[ErrorView] ErrorPresenter not injected!");
                return;
            }

            // 1. Update UI from Presenter
            if (messageText != null)
            {
                messageText.text = string.IsNullOrEmpty(_presenter.ErrorMessage) 
                    ? "An unknown error occurred." 
                    : _presenter.ErrorMessage;
            }

            // 2. Bind Events
            if (backButton != null)
            {
                 Debug.LogError("[ErrorView] button hooked up");
                backButton.onClick.AddListener(_presenter.GoBack);
            }
        }

        public void dosomething()
        {
            Debug.Log("Doing something in ErrorView");
        }

        private void OnDestroy()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveListener(_presenter.GoBack);
            }
        }
    }
}
