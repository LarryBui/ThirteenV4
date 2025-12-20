using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TienLen.Domain.ValueObjects;
using TMPro;
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
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Layout")]
        [SerializeField] private float _cardSpacing = 40f;
        [SerializeField] private int _scaleDownThreshold = 6;
        [SerializeField] private float _scaleStep = 0.05f;
        [SerializeField] private float _minScale = 0.6f;

        [Header("Behavior")]
        [Tooltip("Disable raycasts on spawned cards to avoid blocking input.")]
        [SerializeField] private bool _disableRaycasts = true;
        [SerializeField] private float _fadeOutSeconds = 0.3f;
        [SerializeField] private float _fadeInSeconds = 0.3f;

        private readonly List<RectTransform> _spawnedCards = new();

        /// <summary>
        /// Prefab used for each board card.
        /// </summary>
        public GameObject CardPrefab => _cardPrefab;

        /// <summary>
        /// Anchor used as the center of the board layout.
        /// </summary>
        public RectTransform BoardAnchor => _boardAnchor;

        /// <summary>
        /// Optional parent used to render board cards.
        /// </summary>
        public Transform UiParent => _uiParent;

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
        /// Computes the world position for a card at the specified index within the board layout.
        /// </summary>
        /// <param name="cardIndex">Zero-based index within the combo.</param>
        /// <param name="totalCards">Total cards in the combo.</param>
        public Vector3 GetTargetWorldPosition(int cardIndex, int totalCards)
        {
            if (_boardAnchor == null) return Vector3.zero;
            if (totalCards <= 0) return _boardAnchor.position;

            var offsetFromCenter = cardIndex - ((totalCards - 1) / 2f);
            var axis = _boardAnchor.right;
            return _boardAnchor.position + (axis * (offsetFromCenter * _cardSpacing));
        }

        /// <summary>
        /// Computes the uniform scale applied to board cards for the given count.
        /// </summary>
        /// <param name="cardCount">Number of cards in the combo.</param>
        public float GetScaleForCount(int cardCount)
        {
            if (cardCount <= _scaleDownThreshold) return 1f;
            var reduction = (cardCount - _scaleDownThreshold) * _scaleStep;
            return Mathf.Max(_minScale, 1f - reduction);
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
            var scale = GetScaleForCount(cards.Count);

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
        /// Updates the board label text.
        /// </summary>
        /// <param name="text">Label text to display.</param>
        public void SetLabel(string text)
        {
            if (_label == null) return;
            _label.text = text ?? string.Empty;
            _label.gameObject.SetActive(!string.IsNullOrWhiteSpace(_label.text));
        }

        /// <summary>
        /// Plays a subtle fade-out/fade-in animation on the board.
        /// </summary>
        public void PlayNewRoundFade()
        {
            if (_canvasGroup == null) return;
            FadeRoutine().Forget();
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

        private async UniTask FadeRoutine()
        {
            _canvasGroup.alpha = 1f;
            if (_fadeOutSeconds > 0f)
            {
                await FadeTo(0f, _fadeOutSeconds);
            }
            if (_fadeInSeconds > 0f)
            {
                await FadeTo(1f, _fadeInSeconds);
            }
            _canvasGroup.alpha = 1f;
        }

        private async UniTask FadeTo(float targetAlpha, float durationSeconds)
        {
            if (_canvasGroup == null) return;
            if (durationSeconds <= 0f)
            {
                _canvasGroup.alpha = targetAlpha;
                return;
            }

            var startAlpha = _canvasGroup.alpha;
            var startTime = Time.time;

            while (_canvasGroup != null && Time.time < startTime + durationSeconds)
            {
                var t = (Time.time - startTime) / durationSeconds;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                await UniTask.Yield();
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = targetAlpha;
            }
        }
    }
}
