using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using TienLen.Domain.Enums;
using TienLen.Domain.ValueObjects;
using UnityEngine;
using UnityEngine.UI;

namespace TienLen.Presentation.GameRoomScreen
{
    /// <summary>
    /// Renders the local player's hand as persistent UI cards.
    /// Intended to be driven by the deal animation: when a dealt card reaches the South anchor,
    /// call <see cref="RevealNextCard"/> to spawn and animate the next card into the hand layout.
    /// </summary>
    public sealed class LocalHandView : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private float _cardSpacing = 60f;
        [SerializeField] private float _revealMoveDuration = 0.15f;

        private readonly System.Collections.Generic.List<RectTransform> _spawnedCardRects = new();
        private IReadOnlyList<Card> _cardsToReveal = Array.Empty<Card>();
        private int _nextRevealIndex;

        private RectTransform _handAnchor;
        private Transform _uiParent;
        private GameObject _cardPrefab;

        /// <summary>
        /// Configures the UI objects used for rendering.
        /// </summary>
        /// <param name="cardPrefab">Prefab to instantiate per card (UI prefab with <see cref="RectTransform"/> recommended).</param>
        /// <param name="handAnchor">Anchor used as the center for the hand layout.</param>
        /// <param name="uiParent">Parent under a Canvas where instantiated cards will render.</param>
        public void Configure(GameObject cardPrefab, RectTransform handAnchor, Transform uiParent)
        {
            _cardPrefab = cardPrefab;
            _handAnchor = handAnchor;
            _uiParent = uiParent;
        }

        /// <summary>
        /// Clears any previously rendered cards and primes the view to reveal the provided cards.
        /// </summary>
        /// <param name="cards">Local player's hand, in the order they should be revealed.</param>
        public void BeginReveal(IReadOnlyList<Card> cards)
        {
            Clear();
            _cardsToReveal = cards ?? Array.Empty<Card>();
            _nextRevealIndex = 0;
        }

        /// <summary>
        /// Reveals the next card in the prepared hand by spawning it at <paramref name="fromWorldPosition"/>
        /// and animating it into its final slot relative to the configured hand anchor.
        /// </summary>
        /// <param name="fromWorldPosition">World position where the deal animation landed.</param>
        public void RevealNextCard(Vector3 fromWorldPosition)
        {
            if (_cardPrefab == null) return;
            if (_handAnchor == null) return;
            if (_uiParent == null) return;

            if (_nextRevealIndex < 0 || _nextRevealIndex >= _cardsToReveal.Count) return;

            var cardIndex = _nextRevealIndex;
            var card = _cardsToReveal[cardIndex];
            _nextRevealIndex++;

            var cardObject = Instantiate(_cardPrefab, _uiParent);
            cardObject.SetActive(true);

            var cardRect = cardObject.GetComponent<RectTransform>();
            if (cardRect == null)
            {
                Destroy(cardObject);
                return;
            }

            cardRect.position = fromWorldPosition;
            cardRect.SetAsLastSibling();

            ApplyCardLabel(cardObject, card);

            _spawnedCardRects.Add(cardRect);

            var targetWorldPosition = GetTargetWorldPosition(cardIndex, _cardsToReveal.Count);
            AnimateTo(cardRect, targetWorldPosition).Forget();
        }

        /// <summary>
        /// Destroys any instantiated card visuals and resets internal state.
        /// </summary>
        public void Clear()
        {
            foreach (var rect in _spawnedCardRects)
            {
                if (rect == null) continue;
                Destroy(rect.gameObject);
            }

            _spawnedCardRects.Clear();
            _cardsToReveal = Array.Empty<Card>();
            _nextRevealIndex = 0;
        }

        private Vector3 GetTargetWorldPosition(int cardIndex, int totalCards)
        {
            if (totalCards <= 0) return _handAnchor.position;

            var offsetFromCenter = cardIndex - ((totalCards - 1) / 2f);
            return _handAnchor.position + (Vector3.right * (offsetFromCenter * _cardSpacing));
        }

        private async UniTask AnimateTo(RectTransform rect, Vector3 targetWorldPosition)
        {
            if (rect == null) return;

            var startPosition = rect.position;
            var startTime = Time.time;

            while (rect != null && Time.time < startTime + _revealMoveDuration)
            {
                var t = (Time.time - startTime) / _revealMoveDuration;
                rect.position = Vector3.Lerp(startPosition, targetWorldPosition, 1 - (1 - t) * (1 - t));
                await UniTask.Yield();
            }

            if (rect != null)
            {
                rect.position = targetWorldPosition;
            }
        }

        private static void ApplyCardLabel(GameObject cardObject, Card card)
        {
            var image = cardObject.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
            }

            var label = cardObject.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            if (label == null)
            {
                var labelObject = new GameObject("Label", typeof(RectTransform));
                labelObject.transform.SetParent(cardObject.transform, worldPositionStays: false);

                var labelRect = (RectTransform)labelObject.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                label = labelObject.AddComponent<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = 18;
                label.fontSizeMax = 48;
                label.raycastTarget = false;
            }

            label.text = $"{ToRankString(card.Rank)}{ToSuitSymbol(card.Suit)}";
            label.color = ToSuitColor(card.Suit);
        }

        private static string ToRankString(Rank rank)
        {
            return rank switch
            {
                Rank.Three => "3",
                Rank.Four => "4",
                Rank.Five => "5",
                Rank.Six => "6",
                Rank.Seven => "7",
                Rank.Eight => "8",
                Rank.Nine => "9",
                Rank.Ten => "10",
                Rank.Jack => "J",
                Rank.Queen => "Q",
                Rank.King => "K",
                Rank.Ace => "A",
                Rank.Two => "2",
                _ => rank.ToString()
            };
        }

        private static string ToSuitSymbol(Suit suit)
        {
            return suit switch
            {
                Suit.Spades => "\u2660",
                Suit.Clubs => "\u2663",
                Suit.Diamonds => "\u2666",
                Suit.Hearts => "\u2665",
                _ => suit.ToString()
            };
        }

        private static Color ToSuitColor(Suit suit)
        {
            return suit is Suit.Diamonds or Suit.Hearts ? new Color(0.85f, 0.1f, 0.1f, 1f) : Color.white;
        }
    }
}
