using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TienLen.Presentation.GameRoomScreen
{
    public class CardDealer : MonoBehaviour
    {
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
        [SerializeField] private RectTransform[] _playerAnchors = new RectTransform[4]; // South, West, North, East

        [Header("Settings")]
        [SerializeField] private int _poolSize = 16;
        [SerializeField] private float _cardFlightDuration = 0.5f; // Time for one card to fly from deck to player

        private Queue<GameObject> _cardPool = new Queue<GameObject>();

        private void Awake()
        {
            InitializeCardPool();
        }

        private void InitializeCardPool()
        {
            if (_cardPrefab == null)
            {
                Debug.LogError("CardDealer: Card Prefab is not assigned.");
                return;
            }

            // For UI prefabs (RectTransform/Image), ensure cards are instantiated under a Canvas hierarchy.
            // Prefer the deck anchor as the parent when available so the spawned visuals render correctly.
            var poolParent = _deckAnchor != null ? _deckAnchor.transform : transform;

            for (int i = 0; i < _poolSize; i++)
            {
                GameObject card = Instantiate(_cardPrefab, poolParent);
                card.SetActive(false);
                _cardPool.Enqueue(card);
            }
        }

        /// <summary>
        /// Animates the dealing of cards from the deck to each player's anchor.
        /// </summary>
        /// <param name="totalCardsToDeal">Total number of cards to animate (e.g., 52 for a full deck).</param>
        /// <param name="totalAnimationDuration">Total time the entire dealing animation should take.</param>
        public async UniTask AnimateDeal(int totalCardsToDeal, float totalAnimationDuration)
        {
            if (_cardPool.Count == 0)
            {
                Debug.LogError("CardDealer: Card pool is empty. Cannot animate deal.");
                return;
            }
            if (_playerAnchors == null || _playerAnchors.Length != 4)
            {
                Debug.LogError("CardDealer: Player anchors not correctly assigned or not 4 players.");
                return;
            }
            if (_deckAnchor == null)
            {
                Debug.LogError("CardDealer: Deck anchor is not assigned.");
                return;
            }

            // Calculate the delay between each card being dealt.
            float delayPerCard = totalAnimationDuration / totalCardsToDeal;
            
            // Cards are dealt in a round-robin fashion, so currentCard will determine which player receives it.
            int currentCardIndex = 0;

            for (int i = 0; i < totalCardsToDeal; i++)
            {
                // Ensure we have a card available in the pool
                if (_cardPool.Count == 0)
                {
                    Debug.LogWarning("CardDealer: Card pool exhausted during animation. Some cards may not be dealt visually.");
                    break;
                }

                GameObject flyingCard = _cardPool.Dequeue();
                RectTransform flyingCardRect = flyingCard.GetComponent<RectTransform>();

                // Reset card position and make it visible
                flyingCardRect.position = _deckAnchor.position;
                flyingCard.SetActive(true);

                // Determine target player anchor (0=South, 1=West, 2=North, 3=East)
                int playerIndex = currentCardIndex % _playerAnchors.Length;
                RectTransform targetAnchor = _playerAnchors[playerIndex];

                // Fire and forget the card movement animation, returning it to the pool when done.
                AnimateCardMovement(flyingCardRect, targetAnchor.position, playerIndex).Forget();

                currentCardIndex++;

                // Wait for the calculated delay before dealing the next card.
                await UniTask.Delay(TimeSpan.FromSeconds(delayPerCard));
            }
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
        /// <param name="playerIndex">Player index matching the <see cref="_playerAnchors"/> order.</param>
        public RectTransform GetPlayerAnchor(int playerIndex)
        {
            if (_playerAnchors == null || _playerAnchors.Length == 0) return null;
            if (playerIndex < 0 || playerIndex >= _playerAnchors.Length) return null;
            return _playerAnchors[playerIndex];
        }
    }
}
