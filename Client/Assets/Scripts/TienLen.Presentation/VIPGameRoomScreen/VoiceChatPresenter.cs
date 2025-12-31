using System;
using Cysharp.Threading.Tasks;
using TienLen.Application.Voice;
using UnityEngine;
using VContainer;
using Microsoft.Extensions.Logging;

namespace TienLen.Presentation.VIPGameRoomScreen
{
    public class VoiceChatPresenter : IDisposable
    {
        private readonly IVoiceChatService _voiceService;
        private readonly ILogger<VoiceChatPresenter> _logger;

        [Inject]
        public VoiceChatPresenter(IVoiceChatService voiceService, ILogger<VoiceChatPresenter> logger)
        {
            _voiceService = voiceService;
            _logger = logger;
        }

        public void JoinMatchVoice(string matchId)
        {
            JoinAsync(matchId).Forget();
        }

        private async UniTaskVoid JoinAsync(string matchId)
        {
            try 
            {
                await _voiceService.JoinChannelAsync(matchId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to join voice chat.");
            }
        }

        public void Dispose()
        {
            _voiceService.LeaveChannelAsync().Forget();
        }
    }
}