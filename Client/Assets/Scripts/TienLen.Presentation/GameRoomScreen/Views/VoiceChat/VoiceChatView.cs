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
        [SerializeField] private Toggle _micToggle; // Controls STT (Dictation)
        [SerializeField] private Toggle _voiceMuteToggle; // Controls Voice Audio Mute
        [SerializeField] private Image _micIcon;
        [SerializeField] private Sprite _micOnSprite;
        [SerializeField] private Sprite _micOffSprite;

        [Header("Speech Bubbles")]
        [SerializeField] private SpeechBubbleView _bubblePrefab;
        [Tooltip("Order: 0=South(Local), 1=East, 2=North, 3=West")]
        [SerializeField] private RectTransform[] _bubbleAnchors;
        
        [Header("Speaking Indicators")]
        [SerializeField] private GameObject _speakingIconPrefab;

        private GameRoomPresenter _presenter;
        private readonly Dictionary<int, SpeechBubbleView> _activeBubbles = new();
        private readonly Dictionary<int, GameObject> _activeSpeakingIcons = new();

        [Inject]
        public void Construct(GameRoomPresenter presenter)
        {
            _presenter = presenter;
        }

        private void Start()
        {
            if (_presenter == null) return;

            // Wire up STT Toggle
            if (_micToggle != null)
            {
                _micToggle.onValueChanged.AddListener(HandleSttToggleChanged);
                // Don't auto-set here, let user choose
            }
            
            // Wire up Mute Toggle
            if (_voiceMuteToggle != null)
            {
                _voiceMuteToggle.onValueChanged.AddListener(HandleMuteToggleChanged);
                // Init Mute state? Default is usually Unmuted (Toggle OFF = Unmuted? Or ON = Muted?)
                // Assuming Toggle ON = Muted.
                _presenter.SetMicrophoneMuted(_voiceMuteToggle.isOn);
            }

            // Wire up Events
            _presenter.OnInGameChatReceived += HandleInGameChatReceived;
            _presenter.OnSeatSpeaking += HandleSeatSpeaking;
        }

        private void OnDestroy()
        {
            if (_presenter != null)
            {
                _presenter.OnInGameChatReceived -= HandleInGameChatReceived;
                _presenter.OnSeatSpeaking -= HandleSeatSpeaking;
            }
        }

        private void HandleSttToggleChanged(bool isOn)
        {
            UpdateMicVisuals(isOn);
            _presenter?.SetSpeechToTextActive(isOn);
        }
        
        private void HandleMuteToggleChanged(bool isMuted)
        {
            _presenter?.SetMicrophoneMuted(isMuted);
        }

        private void UpdateMicVisuals(bool isOn)
        {
            if (_micIcon != null)
            {
                _micIcon.sprite = isOn ? _micOnSprite : _micOffSprite;
            }
        }

        private void HandleSeatSpeaking(int seatIndex, bool isSpeaking)
        {
            int relativeIndex = GetRelativeSeatIndex(seatIndex);
            if (relativeIndex < 0 || relativeIndex >= _bubbleAnchors.Length) return;

            if (isSpeaking)
            {
                if (!_activeSpeakingIcons.TryGetValue(relativeIndex, out var icon))
                {
                    if (_speakingIconPrefab != null && _bubbleAnchors[relativeIndex] != null)
                    {
                        icon = Instantiate(_speakingIconPrefab, _bubbleAnchors[relativeIndex]);
                        // Offset slightly if needed, or assume prefab handles it
                        icon.transform.localPosition = new Vector3(30, 30, 0); // Example offset
                        _activeSpeakingIcons[relativeIndex] = icon;
                    }
                }
                if (icon != null) icon.SetActive(true);
            }
            else
            {
                if (_activeSpeakingIcons.TryGetValue(relativeIndex, out var icon) && icon != null)
                {
                    icon.SetActive(false);
                }
            }
        }

        private void HandleInGameChatReceived(int seatIndex, string message)
        {
            int relativeIndex = GetRelativeSeatIndex(seatIndex);

            if (relativeIndex >= 0 && relativeIndex < _bubbleAnchors.Length)
            {
                ShowBubble(relativeIndex, message);
            }
        }

        private int GetRelativeSeatIndex(int seatIndex)
        {
            var match = _presenter?.CurrentMatch;
            if (match == null) return -1;

            int localSeat = match.LocalSeatIndex >= 0 ? match.LocalSeatIndex : 0;
            return (seatIndex - localSeat + 4) % 4;
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
