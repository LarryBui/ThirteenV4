using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Presentation.GameRoomScreen.Views;
using UnityEngine;
using VContainer;

namespace TienLen.Presentation.GameRoomScreen.Services
{
    /// <summary>
    /// Animates the dealing of cards from a deck to player seats.
    /// Uses PlayerSeatsManagerView to find target positions dynamically.
    /// </summary>
    public sealed class CardDealer : MonoBehaviour
    {
        private const int PoolGrowBatchSize = 4;
        private const int PlayerCount = 4;

        /// <summary>
        /// Raised when a dealt card reaches a player anchor.
        /// playerIndex: 0=South (Local), 1=East, 2=North, 3=West.
        /// </summary>
        public event Action<int, Vector3> CardArrivedAtPlayerAnchor;

        [Header("References")]
        [SerializeField] private GameObject _cardPrefab;
        [SerializeField] private RectTransform _deckAnchor;
        [SerializeField] private Views.PlayerSeatsManagerView _seatsManager;

        [Header("Settings")]
        [SerializeField] private int _poolSize = 16;
        [SerializeField] private float _cardFlightDuration = 0.5f;
        [SerializeField] private float _dealSecondsPerCard = 0.04f;

        public GameObject CardPrefab => _cardPrefab;

        private readonly Queue<GameObject> _cardPool = new Queue<GameObject>();
        private ILogger<CardDealer> _logger = NullLogger<CardDealer>.Instance;
        private GameRoomPresenter _presenter;

        [Inject]
        public void Construct(ILogger<CardDealer> logger, GameRoomPresenter presenter)
        {
            _logger = logger ?? NullLogger<CardDealer>.Instance;
            _presenter = presenter;
        }

        private void Awake()
        {
            InitializeCardPool();
        }

        private void InitializeCardPool()
        {
            if (_cardPrefab == null) return;
            var poolParent = _deckAnchor != null ? _deckAnchor.transform : transform;
            ReplenishPool(_poolSize, poolParent);
        }

        public UniTask AnimateDeal(int totalCardsToDeal)
        {
            return AnimateDealWithDelay(totalCardsToDeal, _dealSecondsPerCard);
        }

        private async UniTask AnimateDealWithDelay(int totalCardsToDeal, float delayPerCard)
        {
            if (_deckAnchor == null || _seatsManager == null || totalCardsToDeal <= 0) return;

            int currentCardIndex = 0;
            for (int i = 0; i < totalCardsToDeal; i++)
            {
                if (_cardPool.Count == 0) ReplenishPool(PoolGrowBatchSize, _deckAnchor.transform);

                GameObject flyingCard = _cardPool.Dequeue();
                RectTransform rt = flyingCard.GetComponent<RectTransform>();

                rt.position = _deckAnchor.position;
                flyingCard.SetActive(true);

                // playerIndex: 0=South (Local), 1=East, 2=North, 3=West
                int playerIndex = currentCardIndex % PlayerCount;
                
                RectTransform targetAnchor = _seatsManager.GetAnchorByRelativeIndex(playerIndex);
                
                if (targetAnchor != null)
                {
                    AnimateCardMovement(rt, targetAnchor.position, playerIndex).Forget();
                }
                else
                {
                    flyingCard.SetActive(false);
                    _cardPool.Enqueue(flyingCard);
                }

                currentCardIndex++;
                await UniTask.Delay(TimeSpan.FromSeconds(delayPerCard));
            }

            // Wait for the final card flight to complete before finishing the task.
            await UniTask.Delay(TimeSpan.FromSeconds(_cardFlightDuration));
        }

        private async UniTask AnimateCardMovement(RectTransform cardRect, Vector3 targetPosition, int playerIndex)
        {
            Vector3 startPosition = cardRect.position;
            float startTime = Time.time;

            while (Time.time < startTime + _cardFlightDuration)
            {
                if (cardRect == null) return;
                float t = (Time.time - startTime) / _cardFlightDuration;
                cardRect.position = Vector3.Lerp(startPosition, targetPosition, 1 - (1 - t) * (1 - t));
                await UniTask.Yield();
            }

            if (cardRect != null)
            {
                cardRect.position = targetPosition;
                
                // Notify logic layer
                _presenter?.OnCardDelivered(playerIndex);

                // Notify visual layer (e.g. LocalHandView)
                CardArrivedAtPlayerAnchor?.Invoke(playerIndex, targetPosition);
                
                cardRect.gameObject.SetActive(false);
                _cardPool.Enqueue(cardRect.gameObject);
            }
        }

        private void ReplenishPool(int count, Transform parent)
        {
            for (int i = 0; i < count; i++)
            {
                var card = Instantiate(_cardPrefab, parent);
                card.SetActive(false);
                _cardPool.Enqueue(card);
            }
        }
    }
}
