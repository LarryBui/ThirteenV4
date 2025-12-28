using System;
using System.Collections.Generic;
using TienLen.Presentation.GameRoomScreen;
using UnityEngine;
using VContainer;

namespace TienLen.Presentation.GameRoomScreen.Views
{
    /// <summary>
    /// Manages a single TurnTimerView instance and moves it between seat anchors.
    /// Reactive to GameRoomPresenter events.
    /// </summary>
    public sealed class TurnTimerManagerView : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TurnTimerView _timerPrefab;
        [SerializeField] private float _turnDuration = 15f;

        [Header("Anchors: Order: 0=South(Local), 1=East, 2=North, 3=West")]
        [Tooltip("Order: 0=South(Local), 1=East, 2=North, 3=West")]
        [SerializeField] private RectTransform[] _anchors;

        private GameRoomPresenter _presenter;
        private TurnTimerView _activeTimer;

        [Inject]
        public void Construct(GameRoomPresenter presenter)
        {
            _presenter = presenter;
        }

        private void Start()
        {
            if (_timerPrefab == null)
            {
                Debug.LogError("[TurnTimerManagerView] Timer Prefab is missing.");
                return;
            }

            // Create the single instance
            _activeTimer = Instantiate(_timerPrefab, transform);
            _activeTimer.Stop();

            if (_presenter != null)
            {
                _presenter.OnCardsPlayed += (seat, cards) => Refresh();
                _presenter.OnTurnPassed += (seat) => Refresh();
                _presenter.OnGameStarted += Refresh;
                
                // Reset timers when game ends
                _presenter.OnGameEnded += (result) =>
                {
                    _activeTimer?.Stop();
                };
            }
        }

        private void Refresh()
        {
            var match = _presenter?.CurrentMatch;
            
            if (match == null || match.Phase != "Playing" || match.CurrentTurnSeat < 0)
            {
                _activeTimer?.Stop();
                return;
            }

            // Calculate relative index
            // Relative = (Global - LocalOffset + 4) % 4
            int localSeat = match.LocalSeatIndex >= 0 ? match.LocalSeatIndex : 0;
            int relativeIndex = (match.CurrentTurnSeat - localSeat + 4) % 4;

            if (relativeIndex >= 0 && relativeIndex < _anchors.Length)
            {
                var anchor = _anchors[relativeIndex];
                if (anchor != null && _activeTimer != null)
                {
                    _activeTimer.transform.SetParent(anchor, false);
                    _activeTimer.transform.localPosition = Vector3.zero;
                    _activeTimer.Play(_turnDuration);
                }
            }
        }
    }
}