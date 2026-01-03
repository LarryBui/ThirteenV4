using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Nakama;
using TienLen.Application;
using TienLen.Application.Voice;
using TienLen.Infrastructure.Services;
using TienLen.Infrastructure.Config;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Vivox;

namespace TienLen.Infrastructure.Voice
{
    public class VivoxVoiceChatService : IVoiceChatService, IVivoxTokenProvider
    {
        private readonly IAuthenticationService _authService;
        private readonly VivoxConfig _config;
        private bool _isLoggedIn;
        private string _currentContextMatchId;
        private bool _isSpeechToTextActive;

        public event Action<string, string, bool> OnSpeechMessageReceived;

        [Serializable]
        private class VivoxTokenResponse
        {
            public string token;
        }

        [Serializable]
        private class VivoxTokenRequest
        {
            public string action;
            public string match_id;
        }

        public VivoxVoiceChatService(IAuthenticationService authService, VivoxConfig config)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async UniTask InitializeAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }
            
            // Set provider after initialization when Instance is available
            VivoxService.Instance.SetTokenProvider(this);
            
            // Subscribe to transcription events
            VivoxService.Instance.TranscribedMessageReceived += OnVivoxTranscribedMessage;
        }

        private void OnVivoxTranscribedMessage(VivoxTranscribedMessage message)
        {
            if (!_isSpeechToTextActive) return;
            // Only forward if we have text
            if (string.IsNullOrWhiteSpace(message.Text)) return;
            
            OnSpeechMessageReceived?.Invoke(message.SenderDisplayName, message.Text, message.FromSelf);
        }

        public UniTask EnableSpeechToTextAsync(bool active)
        {
            _isSpeechToTextActive = active;
            return UniTask.CompletedTask;
        }

        public async UniTask JoinChannelAsync(string matchId)
        {
            _currentContextMatchId = matchId;
            Debug.Log($"[Vivox] Joining channel for MatchId: {matchId}");

            // Ensure initialized
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await InitializeAsync();
            }

            if (!_isLoggedIn)
            {
                await LoginAsync();
            }

            var channelOptions = new ChannelOptions(); 
            // Note: Transcription might need specific channel properties depending on Vivox backend config
            await VivoxService.Instance.JoinGroupChannelAsync(matchId, ChatCapability.TextAndAudio, channelOptions);
        }

        public async UniTask LeaveChannelAsync()
        {
            await VivoxService.Instance.LeaveAllChannelsAsync();
            if (_isLoggedIn)
            {
                await VivoxService.Instance.LogoutAsync();
                _isLoggedIn = false;
            }
            _currentContextMatchId = null;
        }

        private async UniTask LoginAsync()
        {
            string playerId = _authService.CurrentUserId;
            string displayName = _authService.CurrentUserDisplayName ?? "Player";
            
            Debug.Log($"[Vivox] Attempting Login. PlayerId: '{playerId}', DisplayName: '{displayName}'");

            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogError("[Vivox] Login Aborted: PlayerId is null or empty!");
                throw new InvalidOperationException("Cannot login to Vivox without a valid PlayerId.");
            }

            var loginOptions = new LoginOptions
            {
                DisplayName = displayName,
                PlayerId = playerId
            };

            await VivoxService.Instance.LoginAsync(loginOptions);
            _isLoggedIn = true;
        }

        public async Task<string> GetTokenAsync(string issuer = null, TimeSpan? expiration = null, string targetUserUri = null, string action = null, string channelUri = null, string fromUserUri = null, string realm = null)
        {
            string matchId = _currentContextMatchId ?? "";
            Debug.Log($"[Vivox] GetTokenAsync called. Action: {action}, MatchId Context: {matchId}, ChannelUri: {channelUri}, UserUri: {fromUserUri}");

            if (action == "join" && !string.IsNullOrEmpty(channelUri))
            {
                // ... extraction logic ...
                int dashIndex = channelUri.LastIndexOf('-');
                int atIndex = channelUri.IndexOf('@');
                if (dashIndex != -1 && atIndex != -1 && atIndex > dashIndex)
                {
                    matchId = channelUri.Substring(dashIndex + 1, atIndex - dashIndex - 1);
                    Debug.Log($"[Vivox] Extracted MatchId from URI: {matchId}");
                }
            }

            // Convert UniTask to Task for interface compliance
            return await RequestAuthTokenAsync(matchId, action).AsTask();
        }

        public UniTask<string> RequestAuthTokenAsync(string matchId)
        {
            return RequestAuthTokenAsync(matchId, "login");
        }

        private async UniTask<string> RequestAuthTokenAsync(string matchId, string action)
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidOperationException("User must be authenticated before requesting Vivox token.");
            }

            Debug.Log($"[Vivox] Requesting Auth Token from Nakama. Action: {action}, MatchId: {matchId}");

            var request = new VivoxTokenRequest
            {
                action = action,
                match_id = matchId
            };

            var jsonPayload = JsonUtility.ToJson(request);
            var rpcResult = await _authService.ExecuteRpcAsync("get_vivox_token", jsonPayload);
            
            Debug.Log($"[Vivox] Received RPC response: {rpcResult}");

            var response = JsonUtility.FromJson<VivoxTokenResponse>(rpcResult);
            if (response == null || string.IsNullOrWhiteSpace(response.token))
            {
                throw new InvalidOperationException($"Vivox {action} token response was empty.");
            }

            return response.token;
        }
    }
}
