using System;
using Cysharp.Threading.Tasks;
using Nakama;
using TienLen.Application.Voice;
using TienLen.Infrastructure.Config;
using TienLen.Infrastructure.Services;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;
using VContainer;

namespace TienLen.Infrastructure.Voice
{
    public class VivoxVoiceChatService : IVoiceChatService
    {
        private readonly NakamaAuthenticationService _authService;
        private readonly VivoxConfig _config;

        [Serializable]
        private class VivoxTokenResponse
        {
            public string token;
        }

        public VivoxVoiceChatService(NakamaAuthenticationService authService, VivoxConfig config)
        {
            _authService = authService;
            _config = config;
        }

        public async UniTask InitializeAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }
        }

        private class NakamaTokenProvider : IVivoxTokenProvider
        {
            private readonly string _token;
            public NakamaTokenProvider(string token) => _token = token;
            public System.Threading.Tasks.Task<string> GetTokenAsync(string issuer = null, TimeSpan? expiration = null, string targetUserUri = null, string action = null, string channelUri = null, string fromUserUri = null, string realm = null)
                => System.Threading.Tasks.Task.FromResult(_token);
        }

        public async UniTask JoinChannelAsync(string matchId)
        {
            try
            {
                // Ensure initialized
                await InitializeAsync();

                // 1. Get Token from Nakama
                var rpcResult = await _authService.Socket.RpcAsync("get_vivox_token", "{\"match_id\":\"" + matchId + "\"}");
                var token = JsonUtility.FromJson<VivoxTokenResponse>(rpcResult.Payload).token;

                // 2. Login to Vivox (using generic identity, channel token handles auth)
                if (!VivoxService.Instance.IsLoggedIn)
                {
                    var loginOptions = new LoginOptions
                    {
                        DisplayName = _authService.Session.UserId, // Or generic name
                        PlayerId = _authService.Session.UserId
                    };
                    await VivoxService.Instance.LoginAsync(loginOptions);
                }

                // 3. Join Channel
                VivoxService.Instance.SetTokenProvider(new NakamaTokenProvider(token));
                await VivoxService.Instance.JoinGroupChannelAsync(matchId, ChatCapability.TextAndAudio);
                Debug.Log($"[Vivox] Joined channel: {matchId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Vivox] Failed to join channel: {ex.Message}");
                throw;
            }
        }

        public async UniTask LeaveChannelAsync()
        {
             if (VivoxService.Instance.IsLoggedIn)
             {
                 await VivoxService.Instance.LogoutAsync();
             }
        }
    }
}
