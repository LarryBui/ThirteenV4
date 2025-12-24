using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TienLen.Domain.ValueObjects;
using UnityEngine;
using UnityEngine.UI; // Needed for Layout components if we used them, or general UI
using TMPro;
using TienLen.Presentation.GameRoomScreen.Components; // For labels

namespace TienLen.Presentation.GameRoomScreen.Views
{
    /// <summary>
    /// Manages the center board area where played cards are displayed.
    /// Uses custom layout logic to allow smooth animations from player hands to the board.
    /// </summary>
    public sealed class GameBoardView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform _cardContainer;
        [SerializeField] private GameObject _cardPrefab;
        [SerializeField] private TMP_Text _statusLabel; // e.g. "New Round"

        [Header("Layout Settings")]
        [SerializeField] private float _cardSpacing = 40f;
        [SerializeField] private float _animationDuration = 0.4f;
        [SerializeField] private AnimationCurve _animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private readonly List<RectTransform> _activeCards = new List<RectTransform>();

        /// <summary>
        /// Clears the board immediately.
        /// </summary>
        public void Clear()
        {
            foreach (var card in _activeCards)
            {
                if (card != null) Destroy(card.gameObject);
            }
            _activeCards.Clear();
            SetStatusLabel(string.Empty);
        }

        /// <summary>
        /// Sets the status label text (e.g., "New Round", "Player X Passed").
        /// </summary>
        public void SetStatusLabel(string text)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = text;
                _statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
        }

        /// <summary>
        /// Animates a set of cards flying from a source position to the board.
        /// This replaces the current board content with the new cards.
        /// </summary>
        /// <param name="cards">The cards to display.</param>
        /// <param name="fromWorldPos">The world position to spawn the cards at (usually player's avatar).</param>
        public void AnimatePlay(IReadOnlyList<Card> cards, Vector3 fromWorldPos)
        {
            // 1. Clear existing cards (or fade them out - for now, clear)
            Clear();

            if (cards == null || cards.Count == 0) return;

            // 2. Instantiate all cards
            var newCards = new List<RectTransform>(cards.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                var cardObj = Instantiate(_cardPrefab, _cardContainer);
                var rt = cardObj.GetComponent<RectTransform>();
                
                // Set visual data
                if (cardObj.TryGetComponent<FrontCardView>(out var view))
                {
                    view.SetCard(cards[i]);
                }
                
                // Initial Position: At the source
                rt.position = fromWorldPos;
                
                newCards.Add(rt);
                _activeCards.Add(rt);
            }

            // 3. Calculate target positions and animate
            AnimateCardsToLayout(newCards).Forget();
        }

        /// <summary>
        /// Updates the board immediately without animation (e.g. on rejoin).
        /// </summary>
        public void SetBoard(IReadOnlyList<Card> cards)
        {
            Clear();
            if (cards == null || cards.Count == 0) return;

            foreach (var card in cards)
            {
                var cardObj = Instantiate(_cardPrefab, _cardContainer);
                var rt = cardObj.GetComponent<RectTransform>();
                
                if (cardObj.TryGetComponent<FrontCardView>(out var view))
                {
                    view.SetCard(card);
                }
                
                _activeCards.Add(rt);
            }

            UpdateLayoutImmediate();
        }

        private async UniTaskVoid AnimateCardsToLayout(List<RectTransform> cards)
        {
            float totalWidth = (cards.Count - 1) * _cardSpacing;
            float startX = -totalWidth / 2f;

            var tasks = new List<UniTask>();

            for (int i = 0; i < cards.Count; i++)
            {
                var rt = cards[i];
                if (rt == null) continue;

                // Calculate target local position
                Vector3 targetLocalPos = new Vector3(startX + (i * _cardSpacing), 0, 0);

                // We need to tween from current WORLD position to target LOCAL position.
                // However, tweening localPosition is cleaner if we convert world start to local start first.
                Vector3 startLocalPos = _cardContainer.InverseTransformPoint(rt.position);
                
                // Reset scale just in case
                rt.localScale = Vector3.one; 

                tasks.Add(TweenPosition(rt, startLocalPos, targetLocalPos));
            }

            await UniTask.WhenAll(tasks);
        }

        private void UpdateLayoutImmediate()
        {
            float totalWidth = (_activeCards.Count - 1) * _cardSpacing;
            float startX = -totalWidth / 2f;

            for (int i = 0; i < _activeCards.Count; i++)
            {
                var rt = _activeCards[i];
                if (rt == null) continue;
                rt.localPosition = new Vector3(startX + (i * _cardSpacing), 0, 0);
                rt.localScale = Vector3.one;
            }
        }

        private async UniTask TweenPosition(RectTransform rt, Vector3 startPos, Vector3 endPos)
        {
            float elapsed = 0f;
            while (elapsed < _animationDuration)
            {
                if (rt == null) return;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _animationDuration);
                float curveT = _animationCurve.Evaluate(t);

                rt.localPosition = Vector3.Lerp(startPos, endPos, curveT);
                await UniTask.Yield();
            }

            if (rt != null) rt.localPosition = endPos;
        }
    }
}
