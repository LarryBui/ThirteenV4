using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using VContainer;
using TienLen.Domain.ValueObjects;
using TienLen.Domain.Enums;

namespace TienLen.Presentation.GameRoomScreen.Views
{
    /// <summary>
    /// Displays a short rolling log of recent room actions.
    /// </summary>
    public sealed class GameRoomLogView : MonoBehaviour
    {
        [Header("Lines")]
        [SerializeField] private TMP_Text[] _lines = new TMP_Text[3];

        [Header("Animation")]
        [SerializeField] private float _rollSeconds = 0.5f;
        [SerializeField] private float _rollOffset = 24f;

        private GameRoomPresenter _presenter;
        private readonly List<string> _entries = new();
        private Vector2[] _linePositions;
        private int _animationToken;

        [Inject]
        public void Construct(GameRoomPresenter presenter)
        {
            _presenter = presenter;
        }

        private void Awake()
        {
            CacheLinePositions();
        }

        private void Start()
        {
            if (_presenter == null) return;

            _presenter.OnGameStarted += HandleGameStarted;
            _presenter.OnTurnPassed += HandleTurnPassed;
            _presenter.OnCardsPlayed += HandleCardsPlayed;
            _presenter.OnPresenceChanged += HandlePresenceChanged;
            _presenter.OnGameEnded += HandleGameEnded;
        }

        private void OnDestroy()
        {
            if (_presenter != null)
            {
                _presenter.OnGameStarted -= HandleGameStarted;
                _presenter.OnTurnPassed -= HandleTurnPassed;
                _presenter.OnCardsPlayed -= HandleCardsPlayed;
                _presenter.OnPresenceChanged -= HandlePresenceChanged;
                _presenter.OnGameEnded -= HandleGameEnded;
            }
        }

        private void HandleGameStarted()
        {
            AddEntry("Game Started");
        }

        private void HandleTurnPassed(int seatIndex)
        {
            string name = _presenter.ResolveDisplayName(seatIndex);
            AddEntry($"{name} passed.");
        }

        private void HandleCardsPlayed(int seatIndex, IReadOnlyList<Card> cards)
        {
            string name = _presenter.ResolveDisplayName(seatIndex);
            string cardStr = string.Join(", ", cards.Select(FormatCard));
            AddEntry($"{name} played: {cardStr}");
        }

        private void HandlePresenceChanged(IReadOnlyList<TienLen.Application.PresenceChange> changes)
        {
            foreach (var c in changes)
            {
                AddEntry($"{c.Username} {(c.Joined ? "joined" : "left")}.");
            }
        }

        private void HandleGameEnded(TienLen.Application.GameEndedResultDto result)
        {
            AddEntry("Game Ended");

            if (result.BalanceChanges == null) return;

            foreach (var kvp in result.BalanceChanges)
            {
                string userId = kvp.Key;
                long change = kvp.Value;

                int seat = _presenter.FindSeatByUserId(userId);
                string name = _presenter.ResolveDisplayName(seat, userId);

                string sign = change >= 0 ? "+" : "";
                AddEntry($"{name}: {sign}{change}");
            }
        }

        private string FormatCard(Card card)
        {
            string rankStr = card.Rank switch
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
                _ => "?"
            };

            string suitStr = card.Suit switch
            {
                Suit.Spades => "<color=#FFFFFF>♠</color>",
                Suit.Clubs => "<color=#FFFFFF>♣</color>",
                Suit.Diamonds => "<color=#FF0000>♦</color>",
                Suit.Hearts => "<color=#FF0000>♥</color>",
                _ => ""
            };

            return $"{rankStr}{suitStr}";
        }

        /// <summary>
        /// Adds a new entry to the log and rolls the text downward.
        /// </summary>
        /// <param name="entry">Text to display.</param>
        public void AddEntry(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry)) return;
            if (_lines == null || _lines.Length == 0) return;

            CacheLinePositions();

            _entries.Insert(0, entry);
            if (_entries.Count > _lines.Length)
            {
                _entries.RemoveAt(_entries.Count - 1);
            }

            ApplyEntries();
            AnimateRollDown().Forget();
        }

        /// <summary>
        /// Clears all log entries and hides the text lines.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            ApplyEntries();
            ResetLinePositions();
        }

        private void ApplyEntries()
        {
            if (_lines == null) return;

            for (int i = 0; i < _lines.Length; i++)
            {
                var line = _lines[i];
                if (line == null) continue;

                var text = i < _entries.Count ? _entries[i] : string.Empty;
                line.text = text;
                line.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
            }
        }

        private void CacheLinePositions()
        {
            if (_lines == null || _lines.Length == 0) return;
            if (_linePositions != null && _linePositions.Length == _lines.Length) return;

            _linePositions = new Vector2[_lines.Length];
            for (int i = 0; i < _lines.Length; i++)
            {
                var line = _lines[i];
                if (line == null) continue;
                _linePositions[i] = line.rectTransform.anchoredPosition;
            }
        }

        private float ResolveRollOffset()
        {
            if (_rollOffset > 0f) return _rollOffset;
            if (_linePositions != null && _linePositions.Length > 1)
            {
                return Mathf.Abs(_linePositions[0].y - _linePositions[1].y);
            }

            return 24f;
        }

        private async UniTask AnimateRollDown()
        {
            if (_lines == null || _linePositions == null) return;

            var token = ++_animationToken;
            var offset = ResolveRollOffset();
            var duration = Mathf.Max(0f, _rollSeconds);

            var startPositions = new Vector2[_lines.Length];
            for (int i = 0; i < _lines.Length; i++)
            {
                var line = _lines[i];
                if (line == null) continue;

                var startPosition = _linePositions[i] + (Vector2.up * offset);
                line.rectTransform.anchoredPosition = startPosition;
                startPositions[i] = startPosition;
            }

            if (duration <= 0f)
            {
                ResetLinePositions();
                return;
            }

            var startTime = Time.time;
            while (_animationToken == token && Time.time < startTime + duration)
            {
                var t = (Time.time - startTime) / duration;
                for (int i = 0; i < _lines.Length; i++)
                {
                    var line = _lines[i];
                    if (line == null) continue;
                    line.rectTransform.anchoredPosition = Vector2.Lerp(startPositions[i], _linePositions[i], t);
                }

                await UniTask.Yield();
            }

            if (_animationToken == token)
            {
                ResetLinePositions();
            }
        }

        private void ResetLinePositions()
        {
            if (_lines == null || _linePositions == null) return;

            for (int i = 0; i < _lines.Length; i++)
            {
                var line = _lines[i];
                if (line == null) continue;
                line.rectTransform.anchoredPosition = _linePositions[i];
            }
        }
    }
}
