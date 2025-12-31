using System;
using System.Threading.Tasks;
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

        [Serializable]
        private class VivoxTokenRequest
        {
            public string action;
            public string match_id;
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
            private readonly ISocket _socket;
            private readonly string _defaultChannelName;

            public NakamaTokenProvider(ISocket socket, string defaultChannelName)
            {
                _socket = socket;
                _defaultChannelName = defaultChannelName;
            }

            public async Task<string> GetTokenAsync(string issuer = null, TimeSpan? expiration = null, string targetUserUri = null, string action = null, string channelUri = null, string fromUserUri = null, string realm = null)
            {
                if (_socket == null)
                {
                    throw new InvalidOperationException("Nakama socket is not available.");
                }

                var resolvedAction = NormalizeAction(action, channelUri);
                var channelName = ResolveChannelName(channelUri) ?? _defaultChannelName;

                if (resolvedAction == "join" && string.IsNullOrWhiteSpace(channelName))
                {
                    throw new InvalidOperationException("Vivox join requires a channel name.");
                }

                var request = new VivoxTokenRequest
                {
                    action = resolvedAction,
                    match_id = channelName
                };

                var rpcResult = await _socket.RpcAsync("get_vivox_token", JsonUtility.ToJson(request));
                var response = JsonUtility.FromJson<VivoxTokenResponse>(rpcResult.Payload);
                if (response == null || string.IsNullOrWhiteSpace(response.token))
                {
                    throw new InvalidOperationException("Vivox token response was empty.");
                }

                return response.token;
            }

            private static string NormalizeAction(string action, string channelUri)
            {
                if (!string.IsNullOrWhiteSpace(action))
                {
                    return action.Trim().ToLowerInvariant();
                }

                return string.IsNullOrWhiteSpace(channelUri) ? "login" : "join";
            }

            private static string ResolveChannelName(string channelUri)
            {
                if (string.IsNullOrWhiteSpace(channelUri))
                {
                    return null;
                }

                const string prefix = "sip:confctl-g-";
                if (!channelUri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var atIndex = channelUri.IndexOf('@', prefix.Length);
                if (atIndex <= prefix.Length)
                {
                    return null;
                }

                return channelUri.Substring(prefix.Length, atIndex - prefix.Length);
            }
        }

        public async UniTask JoinChannelAsync(string matchId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(matchId))
                {
                    throw new ArgumentException("matchId is required.", nameof(matchId));
                }

                // Ensure initialized
                await InitializeAsync();

                // Provide token provider before login so Vivox can request login/join tokens.
                VivoxService.Instance.SetTokenProvider(new NakamaTokenProvider(_authService.Socket, matchId));

                // Login to Vivox only when joining a voice-enabled room.
                if (!VivoxService.Instance.IsLoggedIn)
                {
                    var loginOptions = new LoginOptions
                    {
                        DisplayName = _authService.Session.UserId, // Or generic name
                        PlayerId = _authService.Session.UserId
                    };
                    await VivoxService.Instance.LoginAsync(loginOptions);
                }

                // Join Channel
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
