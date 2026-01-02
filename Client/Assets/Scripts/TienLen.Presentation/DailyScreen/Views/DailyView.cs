using System;
using TienLen.Presentation.DailyScreen.Presenters;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace TienLen.Presentation.DailyScreen.Views
{
    public class DailyView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button[] adButtons;
        [SerializeField] private TMPro.TextMeshProUGUI[] buttonTexts;

        private DailyPresenter _presenter;

        [Inject]
        public void Construct(DailyPresenter presenter)
        {
            _presenter = presenter;
        }

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            closeButton?.onClick.AddListener(() => _presenter?.HandleClose());

            for (int i = 0; i < adButtons.Length; i++)
            {
                int index = i;
                adButtons[i]?.onClick.AddListener(() => 
                {
                    _presenter?.HandleAdClicked(index, (interactable, label) => 
                    {
                        SetButtonState(index, interactable, label);
                    });
                });
            }
        }

        public void SetButtonState(int index, bool interactable, string label)
        {
            if (index < 0 || index >= adButtons.Length) return;

            if (adButtons[index] != null)
            {
                adButtons[index].interactable = interactable;
            }

            if (buttonTexts != null && index < buttonTexts.Length && buttonTexts[index] != null)
            {
                buttonTexts[index].text = label;
            }
        }

        private void OnDestroy()
        {
            closeButton?.onClick.RemoveAllListeners();
            foreach (var btn in adButtons)
            {
                btn?.onClick.RemoveAllListeners();
            }
        }
    }
}