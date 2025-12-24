using TMPro;
using TienLen.Domain.Enums;
using TienLen.Domain.ValueObjects;
using UnityEngine;
using TienLen.Presentation.GameRoomScreen.Utils;

namespace TienLen.Presentation.GameRoomScreen.Components
{
    /// <summary>
    /// UI view component for a face-up card prefab.
    /// Attach this to the local-hand card prefab and wire the TMP fields in the Inspector
    /// so code can set rank/suit text without relying on child name/order.
    /// </summary>
    public sealed class FrontCardView : MonoBehaviour
    {
        [Header("Text References")]
        [SerializeField] private TextMeshProUGUI _rankText;
        [SerializeField] private TextMeshProUGUI _suitText;
        [SerializeField] private TextMeshProUGUI _centerText;

        [Header("Colors")]
        [SerializeField] private Color _blackSuitColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        [SerializeField] private Color _redSuitColor = new Color(0.85f, 0.1f, 0.1f, 1f);

        /// <summary>
        /// Applies card rank/suit display to the configured text references.
        /// </summary>
        /// <param name="card">Domain card value.</param>
        public void SetCard(Card card)
        {
            var rank = CardTextFormatter.ToRankString(card.Rank);
            var suit = CardTextFormatter.ToSuitSymbol(card.Suit);
            var color = GetSuitColor(card.Suit);

            if (_rankText != null)
            {
                _rankText.raycastTarget = false;
                _rankText.text = rank;
                _rankText.color = color;
            }

            if (_suitText != null)
            {
                _suitText.raycastTarget = false;
                _suitText.text = suit;
                _suitText.color = color;
            }

            if (_centerText != null)
            {
                _centerText.raycastTarget = false;
                _centerText.text = suit;
                _centerText.color = color;
            }
        }

        private Color GetSuitColor(Suit suit)
        {
            return suit is Suit.Diamonds or Suit.Hearts ? _redSuitColor : _blackSuitColor;
        }
    }
}
