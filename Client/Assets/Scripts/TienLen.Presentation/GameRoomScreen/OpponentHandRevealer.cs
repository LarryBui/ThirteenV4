using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using TienLen.Domain.ValueObjects;
using TienLen.Domain.Enums;
using TMPro;
using UnityEngine.UI;

namespace TienLen.Presentation.GameRoomScreen
{
    /// <summary>
    /// Renders opponent cards when the game ends.
    /// Spawns cards at the opponent's anchor (provided by CardDealer) and fans them out.
    /// </summary>
    public class OpponentHandRevealer : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private GameObject _cardPrefab;
        [SerializeField] private float _cardSpacing = 30f;
        [SerializeField] private float _cardScale = 0.6f;

        [Header("References")]
        [SerializeField] private CardDealer _cardDealer;

        private readonly List<GameObject> _spawnedCards = new List<GameObject>();

        public void Configure(CardDealer cardDealer, GameObject cardPrefab)
        {
            _cardDealer = cardDealer;
            _cardPrefab = cardPrefab;
        }

        /// <summary>
        /// Reveals the hand for a specific opponent seat.
        /// </summary>
        /// <param name="seatIndex">The seat index of the opponent.</param>
        /// <param name="localSeatIndex">The seat index of the local player (to determine relative position).</param>
        /// <param name="cards">The cards to reveal.</param>
        public void RevealHand(int seatIndex, int localSeatIndex, IReadOnlyList<Card> cards)
        {
            Debug.Log($"[OpponentHandRevealer] RevealHand request: seat={seatIndex} local={localSeatIndex} cards={cards?.Count ?? 0}");

            if (cards == null || cards.Count == 0) return;
            if (_cardDealer == null)
            {
                Debug.LogWarning("[OpponentHandRevealer] CardDealer is missing.");
                return;
            }

            // Calculate relative index: 0=South (Local), 1=East, 2=North, 3=West
            // Assuming SeatCount=4. Formula: (seat - local + 4) % 4
            int relativeIndex = (seatIndex - localSeatIndex + 4) % 4;

            if (relativeIndex == 0) return; // Do not reveal local player (handled by LocalHandView)

            var anchor = _cardDealer.GetPlayerAnchor(relativeIndex);
            if (anchor == null)
            {
                Debug.LogWarning($"[OpponentHandRevealer] Anchor not found for relative index {relativeIndex} (seat {seatIndex})");
                return;
            }

            SpawnCards(anchor, cards);
        }

        public void Clear()
        {
            foreach (var card in _spawnedCards)
            {
                if (card != null) Destroy(card);
            }
            _spawnedCards.Clear();
        }

        private void SpawnCards(RectTransform anchor, IReadOnlyList<Card> cards)
        {
            // Center the fan around the anchor
            float totalWidth = (cards.Count - 1) * _cardSpacing;
            float startX = -totalWidth / 2f;

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                var cardObj = Instantiate(_cardPrefab, anchor); // Parent to anchor so it moves/scales with it
                cardObj.transform.localPosition = new Vector3(startX + (i * _cardSpacing), 0, 0);
                cardObj.transform.localScale = Vector3.one * _cardScale;
                
                // Ensure it's visible and configured
                ApplyCardVisuals(cardObj, card);
                
                _spawnedCards.Add(cardObj);
            }
        }

        private void ApplyCardVisuals(GameObject cardObject, Card card)
        {
            // Reuse logic similar to LocalHandView or shared helper
            if (cardObject.TryGetComponent<FrontCardView>(out var frontCardView))
            {
                frontCardView.SetCard(card);
                return;
            }

            // Fallback text label if no FrontCardView
            var label = cardObject.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            if (label == null)
            {
                // Simple label creation if missing
                var labelObject = new GameObject("Label", typeof(RectTransform));
                labelObject.transform.SetParent(cardObject.transform, worldPositionStays: false);
                var labelRect = (RectTransform)labelObject.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                label = labelObject.AddComponent<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
            }
            
            label.text = CardTextFormatter.FormatShort(card);
            label.color = (card.Suit == Suit.Diamonds || card.Suit == Suit.Hearts) 
                ? new Color(0.8f, 0.1f, 0.1f) 
                : new Color(0.1f, 0.1f, 0.1f);
        }
    }
}
