using System;
using UnityEngine;
using UnityEngine.UI;

namespace TienLen.Presentation.DailyScreen.Views
{
    public class DailyView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button closeButton;

        public event Action OnCloseClicked;

        public void Initialize()
        {
            closeButton?.onClick.AddListener(() => OnCloseClicked?.Invoke());
        }

        private void OnDestroy()
        {
            closeButton?.onClick.RemoveAllListeners();
        }
    }
}
