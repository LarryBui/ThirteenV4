using System.Collections.Generic;
using TienLen.Application;
using UnityEngine;
using Cysharp.Threading.Tasks;
using TienLen.Presentation.GameRoomScreen.Views.Shared;
using TienLen.Domain.ValueObjects;

namespace TienLen.Presentation.GameRoomScreen.Views
{
    public sealed class GameRoomView : BaseGameRoomView
    {
        [Header("GameRoom Specific Views")]
        [SerializeField] private OpponentHandRevealer _opponentRevealer;

        protected override async void HandleGameEnded(GameEndedResultDto result)
        {
            await RunGameEndSequence(result.RemainingHands);
        }

        private async UniTask RunGameEndSequence(IReadOnlyDictionary<int, List<Card>> remainingHands)
        {
            _isAnimationBlocking = true;
            _actionButtons.SetActionButtonsVisible(false);
            _actionButtons.SetStartButtonVisible(false);
            _messageView.ShowInfo("Game Ended");

            _localHandView?.ShowHiddenSelectedCards();

            if (_opponentRevealer != null && remainingHands != null)
            {
                int localSeat = _presenter.CurrentMatch?.LocalSeatIndex ?? 0;
                foreach (var kvp in remainingHands)
                {
                    if (kvp.Key != localSeat)
                        _opponentRevealer.RevealHand(kvp.Key, kvp.Value);
                }
            }

            await UniTask.Delay(System.TimeSpan.FromSeconds(3), cancellationToken: this.GetCancellationTokenOnDestroy());

            _boardView.Clear();
            _localHandView?.Clear();
            _opponentRevealer?.Clear();

            _isAnimationBlocking = false;
            RefreshAll();
        }
    }
}
