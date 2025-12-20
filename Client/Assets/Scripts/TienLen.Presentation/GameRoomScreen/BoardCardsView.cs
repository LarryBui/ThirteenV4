using System.Collections.Generic;
using TienLen.Domain.ValueObjects;
using UnityEngine;
using UnityEngine.UI;

namespace TienLen.Presentation.GameRoomScreen
{
    /// <summary>
    /// Renders the current board (last played combo) as a tight-overlap row of cards.
    /// </summary>
    public sealed class BoardCardsView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _cardPrefab;
        [SerializeField] private RectTransform _boardAnchor;
        [SerializeField] private Transform _uiParent;

        [Header("Layout")]
        [SerializeField] private float _cardSpacing = 40f;
        [SerializeField] private int _scaleDownThreshold = 6;
        [SerializeField] private float _scaleStep = 0.05f;
        [SerializeField] private float _minScale = 0.6f;

        [Header("Behavior")]
        [Tooltip("Disable raycasts on spawned cards to avoid blocking input.")]
        [SerializeField] private bool _disableRaycasts = true;

        private readonly List<RectTransform> _spawnedCards = new();

        /// <summary>
        /// Sets runtime references when the view is created dynamically.
        /// </summary>
        /// <param name="cardPrefab">Prefab used for each board card.</param>
        /// <param name="boardAnchor">Anchor used as the center of the board layout.</param>
        /// <param name="uiParent">Parent transform for instantiated cards.</param>
        public void Configure(GameObject cardPrefab, RectTransform boardAnchor, Transform uiParent)
        {
            _cardPrefab = cardPrefab;
            _boardAnchor = boardAnchor;
            _uiParent = uiParent;
        }

        /// <summary>
        /// Clears existing visuals and renders the provided cards on the board.
        /// </summary>
        /// <param name="cards">Cards to render in play order.</param>
        public void SetBoard(IReadOnlyList<Card> cards)
        {
            Clear();

            if (cards == null || cards.Count == 0) return;
            if (_cardPrefab == null || _boardAnchor == null) return;

            var parent = _uiParent != null ? _uiParent : _boardAnchor.transform;
            var scale = ComputeScale(cards.Count);

            for (int i = 0; i < cards.Count; i++)
            {
                var cardObject = Instantiate(_cardPrefab, parent);
                cardObject.SetActive(true);

                var cardRect = cardObject.GetComponent<RectTransform>();
                if (cardRect == null)
                {
                    Destroy(cardObject);
                    continue;
                }

                var targetPosition = GetTargetWorldPosition(i, cards.Count);
                cardRect.position = targetPosition;
                cardRect.localScale = Vector3.one * scale;
                cardRect.SetAsLastSibling();

                ApplyCardVisual(cardObject, cards[i]);
                DisableRaycasts(cardObject);

                _spawnedCards.Add(cardRect);
            }
        }

        /// <summary>
        /// Removes all spawned board cards.
        /// </summary>
        public void Clear()
        {
            foreach (var rect in _spawnedCards)
            {
                if (rect == null) continue;
                Destroy(rect.gameObject);
            }

            _spawnedCards.Clear();
        }

        private Vector3 GetTargetWorldPosition(int cardIndex, int totalCards)
        {
            if (totalCards <= 0) return _boardAnchor.position;

            var offsetFromCenter = cardIndex - ((totalCards - 1) / 2f);
            var axis = _boardAnchor != null ? _boardAnchor.right : Vector3.right;
            return _boardAnchor.position + (axis * (offsetFromCenter * _cardSpacing));
        }

        private float ComputeScale(int cardCount)
        {
            if (cardCount <= _scaleDownThreshold) return 1f;
            var reduction = (cardCount - _scaleDownThreshold) * _scaleStep;
            return Mathf.Max(_minScale, 1f - reduction);
        }

        private static void ApplyCardVisual(GameObject cardObject, Card card)
        {
            if (cardObject == null) return;
            if (cardObject.TryGetComponent<FrontCardView>(out var view))
            {
                view.SetCard(card);
            }
        }

        private void DisableRaycasts(GameObject cardObject)
        {
            if (!_disableRaycasts || cardObject == null) return;
            var graphics = cardObject.GetComponentsInChildren<Graphic>(includeInactive: true);
            foreach (var graphic in graphics)
            {
                graphic.raycastTarget = false;
            }
        }
    }
}
