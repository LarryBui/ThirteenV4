using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TienLen.Application.Voice
{
    /// <summary>
    /// Application handler that manages the Vivox channel lifecycle for the game room.
    /// </summary>
    public sealed class VoiceChatHandler : IDisposable
    {
        private readonly IVoiceChatService _voiceChatService;
        private readonly ILogger<VoiceChatHandler> _logger;
        private readonly SemaphoreSlim _joinLock = new(1, 1);
        private string? _currentMatchId;
        private bool _isJoined;

        public event Action<string, string, bool> OnSpeechMessageReceived;
        public event Action<string, bool> OnParticipantSpeaking;

        /// <summary>
        /// Creates a new voice chat handler.
        /// </summary>
        /// <param name="voiceChatService">Voice chat service for Vivox calls.</param>
        /// <param name="logger">Logger for voice diagnostics.</param>
        public VoiceChatHandler(IVoiceChatService voiceChatService, ILogger<VoiceChatHandler> logger)
        {
            _voiceChatService = voiceChatService ?? throw new ArgumentNullException(nameof(voiceChatService));
            _logger = logger ?? NullLogger<VoiceChatHandler>.Instance;
            _voiceChatService.OnSpeechMessageReceived += HandleSpeechMessage;
            _voiceChatService.OnParticipantSpeaking += HandleParticipantSpeaking;
        }

        private void HandleSpeechMessage(string sender, string message, bool isFromSelf)
        {
            OnSpeechMessageReceived?.Invoke(sender, message, isFromSelf);
        }

        private void HandleParticipantSpeaking(string sender, bool isSpeaking)
        {
            OnParticipantSpeaking?.Invoke(sender, isSpeaking);
        }

        public async UniTask EnableSpeechToTextAsync(bool active)
        {
            await _voiceChatService.EnableSpeechToTextAsync(active);
        }

        public async UniTask SetInputMutedAsync(bool isMuted)
        {
            await _voiceChatService.MuteInputAsync(isMuted);
        }

        /// <summary>
        /// Ensures the Vivox channel is joined for the given match id.
        /// </summary>
        /// <param name="matchId">The authoritative match id used as the channel name.</param>
        public async UniTask EnsureGameRoomJoinedAsync(string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("Match id is required.", nameof(matchId));
            }

            if (_isJoined && string.Equals(_currentMatchId, matchId, StringComparison.Ordinal))
            {
                return;
            }

            await _joinLock.WaitAsync();
            try
            {
                if (_isJoined && string.Equals(_currentMatchId, matchId, StringComparison.Ordinal))
                {
                    return;
                }

                if (_isJoined && !string.Equals(_currentMatchId, matchId, StringComparison.Ordinal))
                {
                    await _voiceChatService.LeaveChannelAsync();
                    _isJoined = false;
                    _currentMatchId = null;
                }

                await _voiceChatService.JoinChannelAsync(matchId);
                _currentMatchId = matchId;
                _isJoined = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Voice chat join failed for match {MatchId}.", matchId);
                throw;
            }
            finally
            {
                _joinLock.Release();
            }
        }

        /// <summary>
        /// Leaves the current Vivox channel if connected.
        /// </summary>
        public async UniTask LeaveGameRoomAsync()
        {
            if (!_isJoined)
            {
                return;
            }

            await _joinLock.WaitAsync();
            try
            {
                if (!_isJoined)
                {
                    return;
                }

                await _voiceChatService.LeaveChannelAsync();
                _isJoined = false;
                _currentMatchId = null;
            }
            finally
            {
                _joinLock.Release();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_voiceChatService != null)
            {
                _voiceChatService.OnSpeechMessageReceived -= HandleSpeechMessage;
                _voiceChatService.OnParticipantSpeaking -= HandleParticipantSpeaking;
            }
            _joinLock.Dispose();
        }
    }
}
