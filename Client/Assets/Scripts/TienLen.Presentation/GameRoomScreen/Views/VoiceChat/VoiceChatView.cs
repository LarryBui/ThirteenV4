using System;
using System.Collections.Generic;
using TienLen.Presentation.GameRoomScreen;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace TienLen.Presentation.GameRoomScreen.Views
{
    /// <summary>
    /// Coordinates voice chat UI, including the microphone toggle and speech bubbles.
    /// </summary>
    public sealed class VoiceChatView : MonoBehaviour
    {
        [Header("Microphone Input")]
        [SerializeField] private Toggle _micToggle;
        [SerializeField] private Image _micIcon;
        [SerializeField] private Sprite _micOnSprite;
        [SerializeField] private Sprite _micOffSprite;

        [Header("Speech Bubbles")]
        [SerializeField] private SpeechBubbleView _bubblePrefab;
        [Tooltip("Order: 0=South(Local), 1=East, 2=North, 3=West")]
        [SerializeField] private RectTransform[] _bubbleAnchors;

        private GameRoomPresenter _presenter;
        private readonly Dictionary<int, SpeechBubbleView> _activeBubbles = new();

        [Inject]
        public void Construct(GameRoomPresenter presenter)
        {
            _presenter = presenter;
        }

        private void Start()
        {
            if (_presenter == null) return;

            // Wire up Toggle
            if (_micToggle != null)
            {
                _micToggle.onValueChanged.AddListener(HandleMicToggleChanged);
                UpdateMicVisuals(_micToggle.isOn);
            }

            // Wire up Events
            _presenter.OnInGameChatReceived += HandleInGameChatReceived;
        }

        private void OnDestroy()
        {
            if (_presenter != null)
            {
                _presenter.OnInGameChatReceived -= HandleInGameChatReceived;
            }
        }

        private void HandleMicToggleChanged(bool isOn)
        {
            UpdateMicVisuals(isOn);
            _presenter?.SetSpeechToTextActive(isOn);
        }

        private void UpdateMicVisuals(bool isOn)
        {
            if (_micIcon != null)
            {
                _micIcon.sprite = isOn ? _micOnSprite : _micOffSprite;
            }
        }

        private void HandleInGameChatReceived(int seatIndex, string message)
        {
            // Calculate relative index
            var match = _presenter?.CurrentMatch;
            if (match == null) return;

            int localSeat = match.LocalSeatIndex >= 0 ? match.LocalSeatIndex : 0;
            int relativeIndex = (seatIndex - localSeat + 4) % 4;

            if (relativeIndex >= 0 && relativeIndex < _bubbleAnchors.Length)
            {
                ShowBubble(relativeIndex, message);
            }
        }

        private void ShowBubble(int relativeIndex, string message)
        {
            if (!_activeBubbles.TryGetValue(relativeIndex, out var bubble))
            {
                if (_bubblePrefab == null || _bubbleAnchors[relativeIndex] == null) return;
                
                bubble = Instantiate(_bubblePrefab, _bubbleAnchors[relativeIndex]);
                bubble.transform.localPosition = Vector3.zero;
                _activeBubbles[relativeIndex] = bubble;
            }

            bubble.Show(message);
        }
    }
}
