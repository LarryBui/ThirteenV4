using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityEngine;
using VContainer;

namespace TienLen.Presentation.GameRoomScreen
{
    public class CardDealer : MonoBehaviour
    {
        private const int PoolGrowBatchSize = 4;
        private const int PlayerCount = 4;
        /// <summary>
        /// Raised when a dealt card reaches a player anchor (0=South, 1=West, 2=North, 3=East).
        /// Use this to synchronize persistent UI (e.g., revealing the local player's hand) with the
        /// transient deal animation.
        /// </summary>
        public event Action<int, Vector3> CardArrivedAtPlayerAnchor;

        /// <summary>
        /// Prefab used for card visuals (typically a UI prefab with a <see cref="RectTransform"/>).
        /// </summary>
        public GameObject CardPrefab => _cardPrefab;

        [Header("References")]
        [SerializeField] private GameObject _cardPrefab;
        [SerializeField] private RectTransform _deckAnchor;
        [SerializeField] private RectTransform _southAnchor;
        [SerializeField] private RectTransform _westAnchor;
        [SerializeField] private RectTransform _northAnchor;
        [SerializeField] private RectTransform _eastAnchor;

        [Header("Settings")]
        [SerializeField] private int _poolSize = 16;
        [SerializeField] private float _cardFlightDuration = 0.5f; // Time for one card to fly from deck to player
        [SerializeField] private float _dealSecondsPerCard = 0.04f; // Delay between each dealt card.

        private Queue<GameObject> _cardPool = new Queue<GameObject>();
        private ILogger<CardDealer> _logger = NullLogger<CardDealer>.Instance;

        /// <summary>
        /// Injects the logger used for diagnostics.
        /// </summary>
        /// <param name="logger">Logger instance for this dealer.</param>
        [Inject]
        public void Construct(ILogger<CardDealer> logger)
        {
            _logger = logger ?? NullLogger<CardDealer>.Instance;
        }

        private void Awake()
        {
            InitializeCardPool();
        }

        private void InitializeCardPool()
        {
            if (_cardPrefab == null)
            {
                _logger.LogError("CardDealer: Card Prefab is not assigned.");
                return;
            }

            // For UI prefabs (RectTransform/Image), ensure cards are instantiated under a Canvas hierarchy.
            // Prefer the deck anchor as the parent when available so the spawned visuals render correctly.
            var poolParent = _deckAnchor != null ? _deckAnchor.transform : transform;

            if (!ReplenishPool(_poolSize, poolParent))
            {
                return;
            }
        }

        /// <summary>
        /// Animates the dealing of cards from the deck to each player's anchor
        /// using the configured per-card delay.
        /// </summary>
        /// <param name="totalCardsToDeal">Total number of cards to animate (e.g., 52 for a full deck).</param>
        public UniTask AnimateDeal(int totalCardsToDeal)
        {
            return AnimateDealWithDelay(totalCardsToDeal, _dealSecondsPerCard);
        }

        /// <summary>
        /// Animates the dealing of cards from the deck to each player's anchor.
        /// </summary>
        /// <param name="totalCardsToDeal">Total number of cards to animate (e.g., 52 for a full deck).</param>
        /// <param name="totalAnimationDuration">Total time the entire dealing animation should take.</param>
        public UniTask AnimateDeal(int totalCardsToDeal, float totalAnimationDuration)
        {
            float delayPerCard = totalCardsToDeal > 0 ? totalAnimationDuration / totalCardsToDeal : 0f;
            return AnimateDealWithDelay(totalCardsToDeal, delayPerCard);
        }

        private async UniTask AnimateDealWithDelay(int totalCardsToDeal, float delayPerCard)
        {
            if (_deckAnchor == null)
            {
                _logger.LogError("CardDealer: Deck anchor is not assigned.");
                return;
            }
            if (!AreDealAnchorsAssigned())
            {
                _logger.LogError("CardDealer: Deal anchors (South, West, North, East) must be assigned.");
                return;
            }
            if (totalCardsToDeal <= 0)
            {
                _logger.LogWarning("CardDealer: Total cards to deal must be greater than zero.");
                return;
            }

            delayPerCard = Mathf.Max(0f, delayPerCard);

            if (_cardPool.Count == 0)
            {
                if (!ReplenishPool(PoolGrowBatchSize, _deckAnchor.transform))
                {
                    _logger.LogError("CardDealer: Card pool is empty. Cannot animate deal.");
                    return;
                }
            }

            // Cards are dealt in a round-robin fashion, so currentCard will determine which player receives it.
            int currentCardIndex = 0;

            for (int i = 0; i < totalCardsToDeal; i++)
            {
                // Ensure we have a card available in the pool
                if (_cardPool.Count == 0)
                {
                    if (!ReplenishPool(PoolGrowBatchSize, _deckAnchor.transform))
                    {
                        _logger.LogWarning(
                            "CardDealer: Card pool exhausted during animation. Some cards may not be dealt visually.");
                        break;
                    }
                }

                GameObject flyingCard = _cardPool.Dequeue();
                RectTransform flyingCardRect = flyingCard.GetComponent<RectTransform>();

                // Reset card position and make it visible
                flyingCardRect.position = _deckAnchor.position;
                flyingCard.SetActive(true);

                // Determine target player anchor (0=South, 1=West, 2=North, 3=East)
                int playerIndex = currentCardIndex % PlayerCount;
                RectTransform targetAnchor = GetAnchorByIndex(playerIndex);
                if (targetAnchor == null)
                {
                    _logger.LogError("CardDealer: Deal anchor missing for player index {PlayerIndex}.", playerIndex);
                    flyingCard.SetActive(false);
                    _cardPool.Enqueue(flyingCard);
                    break;
                }

                // Fire and forget the card movement animation, returning it to the pool when done.
                AnimateCardMovement(flyingCardRect, targetAnchor.position, playerIndex).Forget();

                currentCardIndex++;

                // Wait for the calculated delay before dealing the next card.
                await UniTask.Delay(TimeSpan.FromSeconds(delayPerCard));
            }
        }

        /// <summary>
        /// Instantiates a new card, disables it, and returns it for pooling.
        /// </summary>
        /// <param name="poolParent">Transform used to parent the pooled card.</param>
        private GameObject CreatePooledCard(Transform poolParent)
        {
            if (_cardPrefab == null)
            {
                _logger.LogError("CardDealer: Card Prefab is not assigned.");
                return null;
            }

            var card = Instantiate(_cardPrefab, poolParent);
            card.SetActive(false);
            return card;
        }

        private bool ReplenishPool(int count, Transform poolParent)
        {
            for (int i = 0; i < count; i++)
            {
                var card = CreatePooledCard(poolParent);
                if (card == null) return false;
                _cardPool.Enqueue(card);
            }

            return true;
        }

        private async UniTask AnimateCardMovement(RectTransform cardRect, Vector3 targetPosition, int playerIndex)
        {
            Vector3 startPosition = cardRect.position;
            float startTime = Time.time;

            while (Time.time < startTime + _cardFlightDuration)
            {
                float t = (Time.time - startTime) / _cardFlightDuration;
                // Using a quadratic ease-out for natural feel
                cardRect.position = Vector3.Lerp(startPosition, targetPosition, 1 - (1 - t) * (1 - t));
                await UniTask.Yield(); // Wait for next frame
            }

            // Ensure it reaches the exact target
            cardRect.position = targetPosition;
            CardArrivedAtPlayerAnchor?.Invoke(playerIndex, targetPosition);
            cardRect.gameObject.SetActive(false);
            _cardPool.Enqueue(cardRect.gameObject); // Return to pool
        }

        /// <summary>
        /// Returns the UI anchor used for a player's deal target (0=South, 1=West, 2=North, 3=East).
        /// </summary>
        /// <param name="playerIndex">Player index matching the South/West/North/East order.</param>
        public RectTransform GetPlayerAnchor(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= PlayerCount) return null;
            return GetAnchorByIndex(playerIndex);
        }

        private bool AreDealAnchorsAssigned()
        {
            return _southAnchor != null && _westAnchor != null && _northAnchor != null && _eastAnchor != null;
        }

        private RectTransform GetAnchorByIndex(int playerIndex)
        {
            return playerIndex switch
            {
                0 => _southAnchor,
                1 => _eastAnchor,
                2 => _northAnchor,
                3 => _westAnchor,
                _ => null
            };
        }
    }
}
