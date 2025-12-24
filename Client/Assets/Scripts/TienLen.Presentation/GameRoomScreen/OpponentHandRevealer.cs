using System;
using System.Collections.Generic;
using UnityEngine;
using TienLen.Domain.ValueObjects;
using TienLen.Domain.Enums;
using TMPro;
using UnityEngine.UI;

namespace TienLen.Presentation.GameRoomScreen
{
    /// <summary>
    /// Renders opponent cards when the game ends.
    /// Uses PlayerSeatsManagerView to locate the correct anchors.
    /// </summary>
    public sealed class OpponentHandRevealer : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private GameObject _cardPrefab;
        [SerializeField] private float _cardSpacing = 30f;
        [SerializeField] private float _cardScale = 0.6f;

        [Header("References")]
        [SerializeField] private Views.PlayerSeatsManagerView _seatsManager;

        private readonly List<GameObject> _spawnedCards = new List<GameObject>();

        /// <summary>
        /// Reveals the hand for a specific opponent seat.
        /// </summary>
        /// <param name="seatIndex">The absolute seat index from the server.</param>
        /// <param name="cards">The cards to reveal.</param>
        public void RevealHand(int seatIndex, IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count == 0) return;
            
            if (_seatsManager == null)
            {
                Debug.LogWarning("[OpponentHandRevealer] PlayerSeatsManagerView is missing.");
                return;
            }

            // Find the view associated with this absolute seat index
            var seatView = _seatsManager.GetViewBySeatIndex(seatIndex);
            if (seatView == null)
            {
                Debug.LogWarning($"[OpponentHandRevealer] No view found for seat index {seatIndex}.");
                return;
            }

            // Important: We only reveal opponents here. The local player reveal is handled by LocalHandView.
            // However, we check if the view is active to be safe.
            if (!seatView.gameObject.activeInHierarchy) return;

            SpawnCards(seatView.CardSourceAnchor, cards);
        }

        public void Clear()
        {
            foreach (var card in _spawnedCards)
            {
                if (card != null) Destroy(card.gameObject);
            }
            _spawnedCards.Clear();
        }

        private void SpawnCards(RectTransform anchor, IReadOnlyList<Card> cards)
        {
            float totalWidth = (cards.Count - 1) * _cardSpacing;
            float startX = -totalWidth / 2f;

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                var cardObj = Instantiate(_cardPrefab, anchor);
                cardObj.transform.localPosition = new Vector3(startX + (i * _cardSpacing), 0, 0);
                cardObj.transform.localScale = Vector3.one * _cardScale;
                
                ApplyCardVisuals(cardObj, card);
                _spawnedCards.Add(cardObj);
            }
        }

        private void ApplyCardVisuals(GameObject cardObject, Card card)
        {
            if (cardObject.TryGetComponent<FrontCardView>(out var frontCardView))
            {
                frontCardView.SetCard(card);
                return;
            }

            var label = cardObject.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            if (label != null)
            {
                label.text = CardTextFormatter.FormatShort(card);
                label.color = (card.Suit == Suit.Diamonds || card.Suit == Suit.Hearts) 
                    ? new Color(0.8f, 0.1f, 0.1f) 
                    : new Color(0.1f, 0.1f, 0.1f);
            }
        }
    }
}