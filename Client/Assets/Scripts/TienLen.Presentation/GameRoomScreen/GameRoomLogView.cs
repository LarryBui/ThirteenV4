using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace TienLen.Presentation.GameRoomScreen
{
    /// <summary>
    /// Displays a short rolling log of recent room actions.
    /// </summary>
    public sealed class GameRoomLogView : MonoBehaviour
    {
        [Header("Lines")]
        [SerializeField] private TMP_Text[] _lines = new TMP_Text[3];

        [Header("Animation")]
        [SerializeField] private float _rollSeconds = 0.25f;
        [SerializeField] private float _rollOffset = 24f;

        private readonly List<string> _entries = new();
        private Vector2[] _linePositions;
        private int _animationToken;

        private void Awake()
        {
            CacheLinePositions();
        }

        /// <summary>
        /// Adds a new entry to the log and rolls the text upward.
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
            AnimateRollUp().Forget();
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

        private async UniTask AnimateRollUp()
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

                var startPosition = _linePositions[i] - (Vector2.up * offset);
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
