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

        [Header("Selection")]
        [Tooltip("When enabled, clicking a card toggles its selection state.")]
        [SerializeField] private bool _enableSelection = true;
        [Tooltip("World-space Y offset applied to selected cards.")]
        [SerializeField] private float _selectedYOffset = 30f;
        [Tooltip("Duration (seconds) for select/deselect movement.")]
        [SerializeField] private float _selectionMoveDuration = 0.08f;

        /// <summary>
        /// Raised whenever the selected card set changes.
        /// </summary>
        public event Action<IReadOnlyList<Card>> SelectionChanged;

        /// <summary>
        /// Current set of selected cards, ordered as they appear in the hand.
        /// Treat this list as read-only; copy it if you need a snapshot.
        /// </summary>
        public IReadOnlyList<Card> SelectedCards => _selectedCards;

        /// <summary>
        /// Snapshot of a selected card's UI state for transient animations.
        /// </summary>
        public sealed class SelectedCardSnapshot
        {
            /// <summary>
            /// Domain card represented by the visual.
            /// </summary>
            public Card Card { get; }

            /// <summary>
            /// RectTransform associated with the selected card.
            /// </summary>
            public RectTransform Rect { get; }

            /// <summary>
            /// World position captured at snapshot time.
            /// </summary>
            public Vector3 WorldPosition { get; }

            /// <summary>
            /// Local scale captured at snapshot time.
            /// </summary>
            public Vector3 LocalScale { get; }

            public SelectedCardSnapshot(Card card, RectTransform rect, Vector3 worldPosition, Vector3 localScale)
            {
                Card = card;
                Rect = rect;
                WorldPosition = worldPosition;
                LocalScale = localScale;
            }
        }

        /// <summary>
        /// Clears the current selection and animates any selected cards back to their base positions.
        /// </summary>
        public void ClearSelection()
        {
            var selectionChanged = false;

            foreach (var entry in _handCards)
            {
                if (entry == null) continue;
                if (!entry.IsSelected) continue;

                entry.IsSelected = false;
                selectionChanged = true;

                if (entry.Rect != null)
                {
                    AnimateTo(entry, entry.BaseWorldPosition, _selectionMoveDuration).Forget();
                }
            }

            if (!selectionChanged) return;

            RefreshSelectedCards();
            SelectionChanged?.Invoke(_selectedCards);
        }

        /// <summary>
        /// Captures the currently selected cards and their UI transforms in hand order.
        /// </summary>
        /// <param name="snapshots">Snapshot list of selected cards.</param>
        /// <returns>True if at least one selected card was captured.</returns>
        public bool TryGetSelectedCardSnapshots(out IReadOnlyList<SelectedCardSnapshot> snapshots)
        {
            if (_handCards.Count == 0)
            {
                snapshots = Array.Empty<SelectedCardSnapshot>();
                return false;
            }

            var result = new List<SelectedCardSnapshot>();
            foreach (var entry in _handCards)
            {
                if (entry == null || !entry.IsSelected) continue;
                if (entry.Rect == null) continue;

                var rect = entry.Rect;
                result.Add(new SelectedCardSnapshot(entry.Card, rect, rect.position, rect.localScale));
            }

            snapshots = result;
            return result.Count > 0;
        }

        /// <summary>
        /// Temporarily hides selected card visuals without changing hand layout.
        /// </summary>
        public void HideSelectedCards()
        {
            if (_handCards.Count == 0) return;

            foreach (var entry in _handCards)
            {
                if (entry == null || !entry.IsSelected) continue;
                if (entry.Rect == null) continue;

                var cardObject = entry.Rect.gameObject;
                if (!cardObject.activeSelf) continue;

                cardObject.SetActive(false);
                _hiddenSelectedCards.Add(entry.Rect);
            }
        }

        /// <summary>
        /// Restores visibility for any cards hidden by <see cref="HideSelectedCards"/>.
        /// </summary>
        public void ShowHiddenSelectedCards()
        {
            if (_hiddenSelectedCards.Count == 0) return;

            foreach (var rect in _hiddenSelectedCards)
            {
                if (rect == null) continue;
                rect.gameObject.SetActive(true);
            }

            _hiddenSelectedCards.Clear();
        }

        private sealed class HandCardEntry
        {
            public Card Card { get; }
            public RectTransform Rect { get; }
            public Vector3 BaseWorldPosition { get; }
            public bool IsSelected { get; set; }
            public int AnimationToken { get; set; }

            public HandCardEntry(Card card, RectTransform rect, Vector3 baseWorldPosition)
            {
                Card = card;
                Rect = rect;
                BaseWorldPosition = baseWorldPosition;
            }
        }

        private readonly System.Collections.Generic.List<RectTransform> _spawnedCardRects = new();
        private readonly System.Collections.Generic.List<HandCardEntry> _handCards = new();
        private readonly System.Collections.Generic.List<Card> _selectedCards = new();
        private readonly HashSet<RectTransform> _hiddenSelectedCards = new();

        private IReadOnlyList<Card> _cardsToReveal = Array.Empty<Card>();
        private int _nextRevealIndex;

        private RectTransform _handAnchor;
        private Transform _uiParent;
        private GameObject _cardPrefab;

        /// <summary>
        /// Immediately resets the hand to display the provided cards without animation.
        /// Useful for refreshing the state after a play or when reconnecting.
        /// </summary>
        public void SetHand(IReadOnlyList<Card> cards)
        {
            Clear();
            if (cards == null || cards.Count == 0) return;

            // Populate internal lists but do not animate
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                var cardObject = Instantiate(_cardPrefab, _uiParent);
                cardObject.SetActive(true);

                var cardRect = cardObject.GetComponent<RectTransform>();
                if (cardRect == null)
                {
                    Destroy(cardObject);
                    continue;
                }

                var targetPos = GetTargetWorldPosition(i, cards.Count);
                cardRect.position = targetPos;
                cardRect.SetAsLastSibling(); // Ensure draw order

                var entry = new HandCardEntry(card, cardRect, targetPos);
                ApplyCardLabel(cardObject, card, enableRaycasts: _enableSelection);

                if (_enableSelection)
                {
                    var selectionInput = cardObject.GetComponent<HandCardSelectionInput>();
                    if (selectionInput == null) selectionInput = cardObject.AddComponent<HandCardSelectionInput>();
                    selectionInput.Bind(() => ToggleSelection(entry));
                }

                _spawnedCardRects.Add(cardRect);
                _handCards.Add(entry);
            }
        }

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

            var targetWorldPosition = GetTargetWorldPosition(cardIndex, _cardsToReveal.Count);
            var entry = new HandCardEntry(card, cardRect, targetWorldPosition);

            ApplyCardLabel(cardObject, card, enableRaycasts: _enableSelection);

            if (_enableSelection)
            {
                var selectionInput = cardObject.GetComponent<HandCardSelectionInput>();
                if (selectionInput == null)
                {
                    selectionInput = cardObject.AddComponent<HandCardSelectionInput>();
                }

                selectionInput.Bind(() => ToggleSelection(entry));
            }

            _spawnedCardRects.Add(cardRect);
            _handCards.Add(entry);

            AnimateTo(entry, targetWorldPosition, _revealMoveDuration).Forget();
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
            _handCards.Clear();
            _cardsToReveal = Array.Empty<Card>();
            _nextRevealIndex = 0;

            _selectedCards.Clear();
            _hiddenSelectedCards.Clear();
            SelectionChanged?.Invoke(_selectedCards);
        }

        private Vector3 GetTargetWorldPosition(int cardIndex, int totalCards)
        {
            if (totalCards <= 0) return _handAnchor.position;

            var offsetFromCenter = cardIndex - ((totalCards - 1) / 2f);
            return _handAnchor.position + (Vector3.right * (offsetFromCenter * _cardSpacing));
        }

        private void ToggleSelection(HandCardEntry entry)
        {
            if (entry == null) return;
            if (!_enableSelection) return;
            if (entry.Rect == null) return;

            entry.IsSelected = !entry.IsSelected;

            var targetWorldPosition = entry.BaseWorldPosition + (entry.IsSelected ? GetSelectionOffset() : Vector3.zero);
            AnimateTo(entry, targetWorldPosition, _selectionMoveDuration).Forget();

            RefreshSelectedCards();
            SelectionChanged?.Invoke(_selectedCards);
        }

        private Vector3 GetSelectionOffset()
        {
            var axisUp = _handAnchor != null ? _handAnchor.up : Vector3.up;
            return axisUp * _selectedYOffset;
        }

        private void RefreshSelectedCards()
        {
            _selectedCards.Clear();

            foreach (var entry in _handCards)
            {
                if (entry == null) continue;
                if (!entry.IsSelected) continue;
                _selectedCards.Add(entry.Card);
            }
        }

        private async UniTask AnimateTo(HandCardEntry entry, Vector3 targetWorldPosition, float durationSeconds)
        {
            if (entry == null) return;

            var rect = entry.Rect;
            if (rect == null) return;
            if (durationSeconds <= 0f)
            {
                rect.position = targetWorldPosition;
                return;
            }

            var token = ++entry.AnimationToken;

            var startPosition = rect.position;
            var startTime = Time.time;

            while (rect != null && entry.AnimationToken == token && Time.time < startTime + durationSeconds)
            {
                var t = (Time.time - startTime) / durationSeconds;
                rect.position = Vector3.Lerp(startPosition, targetWorldPosition, 1 - (1 - t) * (1 - t));
                await UniTask.Yield();
            }

            if (rect != null && entry.AnimationToken == token)
            {
                rect.position = targetWorldPosition;
            }
        }

        private static void ApplyCardLabel(GameObject cardObject, Card card, bool enableRaycasts)
        {
            var image = cardObject.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = enableRaycasts;
            }

            if (cardObject.TryGetComponent<FrontCardView>(out var frontCardView))
            {
                frontCardView.SetCard(card);
                return;
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

            label.raycastTarget = false;
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
            return suit is Suit.Diamonds or Suit.Hearts ? new Color(0.85f, 0.1f, 0.1f, 1f) : new Color(0.1f, 0.1f, 0.1f, 1f);
        }
    }
}
