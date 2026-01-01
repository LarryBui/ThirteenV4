using System;
using Cysharp.Threading.Tasks;
using Nakama;
using TienLen.Application.Voice;
using TienLen.Infrastructure.Services;
using UnityEngine;

namespace TienLen.Infrastructure.Voice
{
    public class VivoxVoiceChatService : IVoiceChatService
    {
        private readonly NakamaAuthenticationService _authService;

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

        public VivoxVoiceChatService(NakamaAuthenticationService authService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        public UniTask InitializeAsync() => UniTask.CompletedTask;

        public UniTask JoinChannelAsync(string matchId) => UniTask.CompletedTask;

        public UniTask LeaveChannelAsync() => UniTask.CompletedTask;

        public async UniTask<string> RequestAuthTokenAsync(string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("matchId is required.", nameof(matchId));
            }

            if (!_authService.IsAuthenticated)
            {
                throw new InvalidOperationException("User must be authenticated before requesting Vivox token.");
            }

            if (_authService.Socket == null)
            {
                throw new InvalidOperationException("Nakama socket is not available.");
            }

            var request = new VivoxTokenRequest
            {
                action = "login",
                match_id = matchId
            };

            var rpcResult = await _authService.Socket.RpcAsync("get_vivox_token", JsonUtility.ToJson(request));
            var response = JsonUtility.FromJson<VivoxTokenResponse>(rpcResult.Payload);
            if (response == null || string.IsNullOrWhiteSpace(response.token))
            {
                throw new InvalidOperationException("Vivox token response was empty.");
            }

            return response.token;
        }
    }
}
