using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TienLen.Presentation.GameRoomScreen
{
    /// <summary>
    /// Renders an opponent hand counter as a card back with a badge and count text.
    /// </summary>
    public sealed class OpponentHandCounterView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _countText;
        [SerializeField] private Image _badgeImage;

        /// <summary>
        /// Attaches this view to the provided anchor and resets local transform values.
        /// </summary>
        /// <param name="anchor">Anchor that represents the opponent hand location.</param>
        public void AttachToAnchor(RectTransform anchor)
        {
            if (anchor == null) return;
            if (transform is not RectTransform rectTransform) return;

            rectTransform.SetParent(anchor, worldPositionStays: false);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
            rectTransform.SetAsLastSibling();
        }

        /// <summary>
        /// Updates the displayed count and toggles visibility based on the value.
        /// </summary>
        /// <param name="count">Number of cards remaining in the opponent's hand.</param>
        public void SetCount(int count)
        {
            var clamped = Mathf.Max(0, count);
            if (clamped <= 0)
            {
                Hide();
                return;
            }

            if (_countText != null)
            {
                _countText.text = clamped.ToString();
            }

            Show();
        }

        /// <summary>
        /// Shows the counter view.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides the counter view and clears the text.
        /// </summary>
        public void Hide()
        {
            if (_countText != null)
            {
                _countText.text = string.Empty;
            }

            gameObject.SetActive(false);
        }
    }
}
