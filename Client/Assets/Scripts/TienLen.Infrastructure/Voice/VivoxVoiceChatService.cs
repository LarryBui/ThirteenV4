using System;
using System.ComponentModel;
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
        public event Action<string, bool> OnParticipantSpeaking;

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

        private readonly HashSet<VivoxParticipant> _activeParticipants = new HashSet<VivoxParticipant>();
        private readonly Dictionary<string, bool> _speakingStates = new Dictionary<string, bool>();

        public async UniTask InitializeAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }
            
            // Set provider after initialization when Instance is available
            VivoxService.Instance.SetTokenProvider(this);
            
            // Subscribe to channel messages
            VivoxService.Instance.ChannelMessageReceived += (message) => 
            {
                if (!_isSpeechToTextActive) return;
                if (string.IsNullOrWhiteSpace(message.MessageText)) return;
                OnSpeechMessageReceived?.Invoke(message.SenderDisplayName, message.MessageText, message.FromSelf);
            };

            // Track participants for polling
            VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;

            // Start Polling Loop
            PollSpeakingStatusAsync().Forget();
        }

        private void OnParticipantAdded(VivoxParticipant participant)
        {
            Debug.Log($"[Vivox] Participant Added: {participant.PlayerId}");
            _activeParticipants.Add(participant);
        }

        private void OnParticipantRemoved(VivoxParticipant participant)
        {
            _activeParticipants.Remove(participant);
            if (_speakingStates.ContainsKey(participant.PlayerId))
            {
                OnParticipantSpeaking?.Invoke(participant.PlayerId, false);
                _speakingStates.Remove(participant.PlayerId);
            }
        }

        private async UniTaskVoid PollSpeakingStatusAsync()
        {
            while (true)
            {
                if (_isLoggedIn && _activeParticipants.Count > 0)
                {
                    foreach (var p in _activeParticipants)
                    {
                        // Note: If AudioEnergy is also missing in this SDK version, we will need another fix.
                        // Threshold of 0.1 avoids noise flickering.
                        bool isSpeaking = p.AudioEnergy > 0.05f; 
                        
                        if (!_speakingStates.TryGetValue(p.PlayerId, out var wasSpeaking) || wasSpeaking != isSpeaking)
                        {
                            _speakingStates[p.PlayerId] = isSpeaking;
                            OnParticipantSpeaking?.Invoke(p.PlayerId, isSpeaking);
                        }
                    }
                }
                await UniTask.Delay(100); // Check 10 times a second
            }
        }

        public UniTask MuteInputAsync(bool isMuted)
        {
            if (isMuted)
                VivoxService.Instance.MuteInputDevice();
            else
                VivoxService.Instance.UnmuteInputDevice();
            
            return UniTask.CompletedTask;
        }

        public UniTask EnableSpeechToTextAsync(bool active)
        {
            if (active)
            {
                Debug.Log("[Vivox] Speech-to-Text (STT) enabled in client. \n" +
                          "IMPORTANT: STT is a paid add-on. You must contact Vivox Sales to enable 'Speech-to-Text Transcription' for your organization before this will work.");
            }
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

            // NOTE: Transcription (STT) must be enabled in the Unity Dashboard > Vivox > Settings.
            var channelOptions = new ChannelOptions(); 
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
            
            // Debug Microphone Status
            Debug.Log($"[Vivox] Active Input Device: {VivoxService.Instance.ActiveInputDevice?.DeviceName}");
            foreach (var device in VivoxService.Instance.AvailableInputDevices)
            {
                Debug.Log($"[Vivox] Detected Input Device: {device.DeviceName}");
            }
            
            if (VivoxService.Instance.ActiveInputDevice == null && VivoxService.Instance.AvailableInputDevices.Count > 0)
            {
                var firstDevice = VivoxService.Instance.AvailableInputDevices[0];
                Debug.LogWarning($"[Vivox] No active input device! Force setting to: {firstDevice.DeviceName}");
                await VivoxService.Instance.SetActiveInputDeviceAsync(firstDevice);
            }
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
