using System.Collections.Generic;
using TienLen.Application;
using Cysharp.Threading.Tasks;
using TienLen.Presentation.GameRoomScreen.Views;
using TienLen.Presentation.GameRoomScreen.Views.Shared;
using TienLen.Presentation.GameRoomScreen;
using TienLen.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace TienLen.Presentation.VIPGameRoomScreen.Views
{
    public sealed class VIPGameRoomView : BaseGameRoomView
    {
        [SerializeField] private VoiceChatView _voiceChatView;
        private VoiceChatPresenter _voiceChatPresenter;

        [VContainer.Inject]
        public void Construct(
            GameRoomPresenter presenter,
            TienLen.Infrastructure.Config.AvatarRegistry avatarRegistry,
            ILogger<BaseGameRoomView> logger,
            VoiceChatPresenter voiceChatPresenter)
        {
            base.Construct(presenter, avatarRegistry, logger);
            _voiceChatPresenter = voiceChatPresenter;
        }

        protected override void Start()
        {
            base.Start();
            if (_voiceChatPresenter != null && !string.IsNullOrEmpty(_presenter.CurrentMatch?.Id))
            {
                _voiceChatPresenter.JoinMatchVoice(_presenter.CurrentMatch.Id);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _voiceChatPresenter?.Dispose();
        }

        protected override async void HandleGameEnded(GameEndedResultDto result)
        {
            await RunGameEndSequence();
        }

        private async UniTask RunGameEndSequence()
        {
            _isAnimationBlocking = true;
            _actionButtons.SetActionButtonsVisible(false);
            _actionButtons.SetStartButtonVisible(false);
            _messageView.ShowInfo("Game Ended");

            _localHandView?.ShowHiddenSelectedCards();

            await UniTask.Delay(System.TimeSpan.FromSeconds(3), cancellationToken: this.GetCancellationTokenOnDestroy());

            _boardView.Clear();
            _localHandView?.Clear();

            _isAnimationBlocking = false;
            RefreshAll();
        }
    }
}
