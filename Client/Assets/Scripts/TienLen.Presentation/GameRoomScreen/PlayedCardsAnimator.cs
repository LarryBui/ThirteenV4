using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Domain.ValueObjects;
using UnityEngine;
using UnityEngine.UI;

namespace TienLen.Presentation.GameRoomScreen
{
    /// <summary>
    /// Animates a transient "ghost" set of cards from source positions to the board layout.
    /// Intended for optimistic local play feedback.
    /// </summary>
    public sealed class PlayedCardsAnimator : MonoBehaviour
    {
        private const int PoolGrowBatchSize = 4;

        [Header("References")]
        [SerializeField] private BoardCardsView _boardCardsView;
        [SerializeField] private GameObject _cardPrefab;
        [SerializeField] private RectTransform _boardAnchor;
        [SerializeField] private Transform _uiParent;

        [Header("Layout")]
        [SerializeField] private float _cardSpacing = 40f;
        [SerializeField] private int _scaleDownThreshold = 6;
        [SerializeField] private float _scaleStep = 0.05f;
        [SerializeField] private float _minScale = 0.6f;

        [Header("Animation")]
        [SerializeField] private float _flightSeconds = 0.3f;
        [SerializeField] private float _staggerSeconds = 0.03f;
        [SerializeField] private float _arcHeight = 24f;
        [SerializeField] private bool _disableRaycasts = true;

        [Header("Pooling")]
        [SerializeField] private int _poolSize = 12;

        private readonly Queue<GameObject> _pool = new();
        private readonly Dictionary<GameObject, int> _activeCards = new();
        private ILogger<PlayedCardsAnimator> _logger = NullLogger<PlayedCardsAnimator>.Instance;
        private int _animationToken;

        private void Awake()
        {
            TryInitializePool();
        }

        /// <summary>
        /// Configures runtime references for the animator.
        /// </summary>
        /// <param name="cardPrefab">Prefab used for ghost cards.</param>
        /// <param name="boardAnchor">Anchor used as the board center.</param>
        /// <param name="uiParent">Parent under a Canvas where cards should render.</param>
        public void Configure(GameObject cardPrefab, RectTransform boardAnchor, Transform uiParent)
        {
            _cardPrefab = cardPrefab;
            _boardAnchor = boardAnchor;
            _uiParent = uiParent;
            TryInitializePool();
        }

        /// <summary>
        /// Assigns a logger instance used for diagnostics.
        /// </summary>
        /// <param name="logger">Logger for this animator.</param>
        public void SetLogger(ILogger<PlayedCardsAnimator> logger)
        {
            _logger = logger ?? NullLogger<PlayedCardsAnimator>.Instance;
        }

        /// <summary>
        /// Cancels any in-flight animations and returns active cards to the pool.
        /// </summary>
        public void CancelActiveAnimations()
        {
            _animationToken++;

            if (_activeCards.Count == 0) return;

            var buffer = new List<GameObject>(_activeCards.Keys);
            foreach (var card in buffer)
            {
                ForceReturnToPool(card);
            }
        }

        /// <summary>
        /// Animates the provided cards from their source transforms to the board layout.
        /// </summary>
        /// <param name="cards">Cards to animate in play order.</param>
        /// <param name="sourceRects">Source transforms for each selected card.</param>
        public UniTask AnimatePlay(IReadOnlyList<Card> cards, IReadOnlyList<RectTransform> sourceRects)
        {
            if (cards == null || sourceRects == null) return UniTask.CompletedTask;
            if (cards.Count == 0 || sourceRects.Count == 0) return UniTask.CompletedTask;
            if (cards.Count != sourceRects.Count)
            {
                _logger.LogWarning("PlayedCardsAnimator: cards/source count mismatch.");
                return UniTask.CompletedTask;
            }

            var cardPrefab = ResolveCardPrefab();
            var boardAnchor = ResolveBoardAnchor();
            if (cardPrefab == null || boardAnchor == null)
            {
                _logger.LogWarning("PlayedCardsAnimator: Missing card prefab or board anchor.");
                return UniTask.CompletedTask;
            }

            CancelActiveAnimations();
            TryInitializePool();

            var token = _animationToken;
            var targetScale = ResolveScale(cards.Count);
            var tasks = new UniTask[cards.Count];

            for (int i = 0; i < cards.Count; i++)
            {
                var sourceRect = sourceRects[i];
                if (sourceRect == null)
                {
                    tasks[i] = UniTask.CompletedTask;
                    continue;
                }

                var ghostCard = GetPooledCard();
                if (ghostCard == null)
                {
                    tasks[i] = UniTask.CompletedTask;
                    continue;
                }

                var cardRect = ghostCard.GetComponent<RectTransform>();
                if (cardRect == null)
                {
                    ForceReturnToPool(ghostCard);
                    tasks[i] = UniTask.CompletedTask;
                    continue;
                }

                ghostCard.SetActive(true);
                cardRect.SetAsLastSibling();
                cardRect.position = sourceRect.position;
                cardRect.localScale = sourceRect.localScale;

                ApplyCardVisual(ghostCard, cards[i]);
                DisableRaycasts(ghostCard);
                _activeCards[ghostCard] = token;

                var targetPosition = ResolveTargetPosition(i, cards.Count);
                var startScale = cardRect.localScale;
                tasks[i] = AnimateCard(cardRect, targetPosition, startScale, targetScale, token, i);
            }

            return UniTask.WhenAll(tasks);
        }

        private void TryInitializePool()
        {
            if (_pool.Count > 0) return;
            if (_poolSize <= 0) return;

            var cardPrefab = ResolveCardPrefab();
            if (cardPrefab == null) return;

            var parent = ResolvePoolParent();
            if (parent == null) return;

            ReplenishPool(_poolSize, parent);
        }

        private Transform ResolvePoolParent()
        {
            var parent = ResolveUiParent();
            if (parent != null) return parent;

            var boardAnchor = ResolveBoardAnchor();
            return boardAnchor != null ? boardAnchor.transform : transform;
        }

        private GameObject ResolveCardPrefab()
        {
            if (_cardPrefab != null) return _cardPrefab;
            return _boardCardsView != null ? _boardCardsView.CardPrefab : null;
        }

        private RectTransform ResolveBoardAnchor()
        {
            if (_boardAnchor != null) return _boardAnchor;
            return _boardCardsView != null ? _boardCardsView.BoardAnchor : null;
        }

        private Transform ResolveUiParent()
        {
            if (_uiParent != null) return _uiParent;
            return _boardCardsView != null ? _boardCardsView.UiParent : null;
        }

        private float ResolveScale(int cardCount)
        {
            if (_boardCardsView != null) return _boardCardsView.GetScaleForCount(cardCount);
            return ComputeScale(cardCount);
        }

        private Vector3 ResolveTargetPosition(int index, int totalCards)
        {
            if (_boardCardsView != null) return _boardCardsView.GetTargetWorldPosition(index, totalCards);
            return ComputeTargetPosition(index, totalCards, ResolveBoardAnchor());
        }

        private GameObject GetPooledCard()
        {
            if (_pool.Count == 0)
            {
                var parent = ResolvePoolParent();
                if (parent != null)
                {
                    ReplenishPool(PoolGrowBatchSize, parent);
                }
            }

            if (_pool.Count == 0) return null;

            var card = _pool.Dequeue();
            var parentTransform = ResolvePoolParent();
            if (parentTransform != null && card.transform.parent != parentTransform)
            {
                card.transform.SetParent(parentTransform, worldPositionStays: false);
            }

            return card;
        }

        private bool ReplenishPool(int count, Transform poolParent)
        {
            var prefab = ResolveCardPrefab();
            if (prefab == null) return false;

            for (int i = 0; i < count; i++)
            {
                var card = Instantiate(prefab, poolParent);
                card.SetActive(false);
                _pool.Enqueue(card);
            }

            return true;
        }

        private void ReturnToPool(GameObject card, int token)
        {
            if (card == null) return;
            if (!_activeCards.TryGetValue(card, out var activeToken)) return;
            if (activeToken != token) return;

            _activeCards.Remove(card);
            card.SetActive(false);
            _pool.Enqueue(card);
        }

        private void ForceReturnToPool(GameObject card)
        {
            if (card == null) return;
            _activeCards.Remove(card);
            card.SetActive(false);
            _pool.Enqueue(card);
        }

        private async UniTask AnimateCard(
            RectTransform cardRect,
            Vector3 targetPosition,
            Vector3 startScale,
            float targetScale,
            int token,
            int index)
        {
            if (cardRect == null) return;

            if (_staggerSeconds > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_staggerSeconds * index));
            }

            if (_animationToken != token)
            {
                ReturnToPool(cardRect.gameObject, token);
                return;
            }

            var startPosition = cardRect.position;
            var duration = Mathf.Max(0f, _flightSeconds);
            var anchor = ResolveBoardAnchor();
            var upAxis = anchor != null ? anchor.up : Vector3.up;
            var controlPoint = (startPosition + targetPosition) * 0.5f + (upAxis * _arcHeight);

            if (duration <= 0f)
            {
                cardRect.position = targetPosition;
                cardRect.localScale = Vector3.one * targetScale;
                ReturnToPool(cardRect.gameObject, token);
                return;
            }

            var startTime = Time.time;
            while (cardRect != null && _animationToken == token && Time.time < startTime + duration)
            {
                var t = (Time.time - startTime) / duration;
                cardRect.position = QuadraticBezier(startPosition, controlPoint, targetPosition, t);
                cardRect.localScale = Vector3.Lerp(startScale, Vector3.one * targetScale, t);
                await UniTask.Yield();
            }

            if (cardRect != null && _animationToken == token)
            {
                cardRect.position = targetPosition;
                cardRect.localScale = Vector3.one * targetScale;
            }

            ReturnToPool(cardRect != null ? cardRect.gameObject : null, token);
        }

        private Vector3 ComputeTargetPosition(int cardIndex, int totalCards, RectTransform boardAnchor)
        {
            if (boardAnchor == null) return Vector3.zero;
            if (totalCards <= 0) return boardAnchor.position;

            var offsetFromCenter = cardIndex - ((totalCards - 1) / 2f);
            return boardAnchor.position + (boardAnchor.right * (offsetFromCenter * _cardSpacing));
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

        private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            var ab = Vector3.Lerp(a, b, t);
            var bc = Vector3.Lerp(b, c, t);
            return Vector3.Lerp(ab, bc, t);
        }
    }
}
