using TMPro;
using UnityEngine;

namespace TienLen.Presentation.GameRoomScreen.Views
{
    /// <summary>
    /// Displays the number of cards an opponent is currently holding.
    /// Typically shown as a badge on a card-back graphic.
    /// </summary>
    public sealed class OpponentHandCounterView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _countText;
        [SerializeField] private GameObject _container;

        private int _currentCount;

        public void SetActive(bool active)
        {
            if (_container != null) _container.SetActive(active);
        }

        public void SetCount(int count)
        {
            _currentCount = count;
            UpdateUI();
        }

        public void Increment()
        {
            _currentCount++;
            UpdateUI();
        }

        public void Decrement(int amount = 1)
        {
            _currentCount = Mathf.Max(0, _currentCount - amount);
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_countText != null)
            {
                _countText.text = _currentCount.ToString();
            }
            
            // Auto-hide if count is 0
            SetActive(_currentCount > 0);
        }
    }
}
