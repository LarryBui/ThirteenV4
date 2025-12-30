using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Application;
using TienLen.Presentation.GameRoomScreen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace TienLen.Presentation.GameRoomScreen.DebugTools
{
    /// <summary>
    /// Debug-only view that sends rigged deck inputs to the server for parsing.
    /// </summary>
    public sealed class RiggedDeckDebugView : MonoBehaviour
    {
        [Header("Button")]
        [SerializeField] private Button _riggedStartButton;
        [SerializeField] private TMP_Text _statusText;

        [Header("Seat Inputs")]
        [Tooltip("Seat 0 cards (e.g. 3H, 3D, 10S, QH, ALL).")]
        [TextArea(2, 4)]
        [SerializeField] private string _seat0Cards;
        [Tooltip("Seat 1 cards (e.g. 3H, 3D, 10S, QH, ALL).")]
        [TextArea(2, 4)]
        [SerializeField] private string _seat1Cards;
        [Tooltip("Seat 2 cards (e.g. 3H, 3D, 10S, QH, ALL).")]
        [TextArea(2, 4)]
        [SerializeField] private string _seat2Cards;
        [Tooltip("Seat 3 cards (e.g. 3H, 3D, 10S, QH, ALL).")]
        [TextArea(2, 4)]
        [SerializeField] private string _seat3Cards;

        [Header("Preset (Optional)")]
        [SerializeField] private RiggedDeckPreset _riggedDeckPreset;

        private GameRoomPresenter _presenter;
        private ILogger<RiggedDeckDebugView> _logger;
        private bool _isSending;

        /// <summary>
        /// Injects the presenter and logger dependencies.
        /// </summary>
        /// <param name="presenter">Game room presenter.</param>
        /// <param name="logger">Logger instance.</param>
        [Inject]
        public void Construct(GameRoomPresenter presenter, ILogger<RiggedDeckDebugView> logger)
        {
            _presenter = presenter;
            _logger = logger ?? NullLogger<RiggedDeckDebugView>.Instance;
        }

        private void Awake()
        {
            if (_riggedStartButton != null)
            {
                _riggedStartButton.onClick.AddListener(OnRiggedStartClicked);
            }
        }

        private void Start()
        {
            if (_presenter == null)
            {
                _logger.LogError("RiggedDeckDebugView: Presenter not injected.");
                SetStatus("Rigged start unavailable.");
                return;
            }

            _presenter.OnStateUpdated += RefreshButtonState;
            RefreshButtonState();
        }

        private void OnDestroy()
        {
            if (_presenter != null)
            {
                _presenter.OnStateUpdated -= RefreshButtonState;
            }

            if (_riggedStartButton != null)
            {
                _riggedStartButton.onClick.RemoveListener(OnRiggedStartClicked);
            }
        }

        private void OnRiggedStartClicked()
        {
            StartRiggedGameAsync().Forget();
        }

        private async UniTaskVoid StartRiggedGameAsync()
        {
            if (_isSending) return;

            if (_presenter == null || !_presenter.CanStartGame())
            {
                SetStatus("Rigged start is only available for the match owner in lobby.");
                return;
            }

            var matchId = _presenter.CurrentMatch?.Id;
            if (string.IsNullOrWhiteSpace(matchId))
            {
                SetStatus("Match is not ready for a rigged start.");
                return;
            }

            if (!TryBuildRequest(matchId, out var request, out var error))
            {
                SetStatus(error ?? "Rigged deck input is invalid.");
                return;
            }

            _isSending = true;
            RefreshButtonState();

            try
            {
                await _presenter.StartRiggedGameAsync(request);
                SetStatus("Rigged start requested.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RiggedDeckDebugView: Rigged start failed.");
                SetStatus("Rigged start failed. Check server test mode.");
            }
            finally
            {
                _isSending = false;
                RefreshButtonState();
            }
        }

        private void RefreshButtonState()
        {
            if (_riggedStartButton == null) return;

            var canStart = _presenter != null && _presenter.CanStartGame();
            var hasInput = HasAnyInput();
            _riggedStartButton.interactable = canStart && hasInput && !_isSending;
        }

        private bool HasAnyInput()
        {
            return !string.IsNullOrWhiteSpace(_seat0Cards)
                   || !string.IsNullOrWhiteSpace(_seat1Cards)
                   || !string.IsNullOrWhiteSpace(_seat2Cards)
                   || !string.IsNullOrWhiteSpace(_seat3Cards)
                   || _riggedDeckPreset != null;
        }

        private bool TryBuildRequest(string matchId, out RiggedDeckRequestDto request, out string error)
        {
            var handTexts = BuildHandTexts();
            if (handTexts.Count == 0 && _riggedDeckPreset != null)
            {
                return _riggedDeckPreset.TryBuildRequest(matchId, out request, out error);
            }

            request = new RiggedDeckRequestDto(matchId, new List<RiggedHandDto>(), handTexts);
            if (!RiggedDeckValidator.TryValidate(request, out error))
            {
                request = null;
                return false;
            }

            return true;
        }

        private List<RiggedHandTextDto> BuildHandTexts()
        {
            var results = new List<RiggedHandTextDto>();
            AddSeatText(results, 0, _seat0Cards);
            AddSeatText(results, 1, _seat1Cards);
            AddSeatText(results, 2, _seat2Cards);
            AddSeatText(results, 3, _seat3Cards);
            return results;
        }

        private static void AddSeatText(List<RiggedHandTextDto> results, int seat, string input)
        {
            if (results == null || string.IsNullOrWhiteSpace(input)) return;
            results.Add(new RiggedHandTextDto(seat, input.Trim()));
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message ?? string.Empty;
                return;
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                _logger.LogInformation("RiggedDeckDebugView: {Message}", message);
            }
        }
    }
}
