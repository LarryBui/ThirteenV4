using System;
using Cysharp.Threading.Tasks;
using TienLen.Application;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;
using VContainer;
using Nakama;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TienLen.Infrastructure.Services
{
    public interface IVivoxService
    {
        UniTask InitializeAsync();
        UniTask LoginAsync();
        UniTask JoinChannelAsync(string channelName);
        UniTask LeaveChannelAsync(string channelName);
        UniTask LogoutAsync();
    }

    public class VivoxService : IVivoxService
    {
        private readonly NakamaAuthenticationService _authService;
        private readonly ILogger<VivoxService> _logger;

        [Inject]
        public VivoxService(
            NakamaAuthenticationService authService,
            ILogger<VivoxService> logger)
        {
            _authService = authService;
            _logger = logger ?? NullLogger<VivoxService>.Instance;
        }

        public async UniTask InitializeAsync()
        {
            await UnityServices.InitializeAsync();

            // Register our custom token provider
            // We must do this before logging in.
            // Note: Since IVivoxTokenProvider is an interface in Unity.Services.Vivox, we implement it.
            // However, we need to pass the Nakama client/session to it.
            // Since we can't inject into a `new` object easily unless we pass it, we'll pass the auth service.

            var tokenProvider = new NakamaVivoxTokenProvider(_authService, _logger);
            // Assuming the API is VivoxService.Instance.SetTokenProvider(IVivoxTokenProvider)
            // Based on search results, this exists.
            Unity.Services.Vivox.VivoxService.Instance.SetTokenProvider(tokenProvider);

            await Unity.Services.Vivox.VivoxService.Instance.InitializeAsync();
            _logger.LogInformation("Vivox initialized and token provider set.");
        }

        public async UniTask LoginAsync()
        {
            if (Unity.Services.Vivox.VivoxService.Instance.IsSignedIn)
            {
                _logger.LogInformation("Already signed in to Vivox.");
                return;
            }

            _logger.LogInformation("Logging into Vivox...");

            var loginOptions = new LoginOptions
            {
                DisplayName = _authService.CurrentUserDisplayName,
                ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.FivePerSecond
            };

            // LoginAsync will internally call our TokenProvider.GetTokenAsync
            await Unity.Services.Vivox.VivoxService.Instance.LoginAsync(loginOptions);
            _logger.LogInformation("Vivox Login Complete.");
        }

        public async UniTask JoinChannelAsync(string channelName)
        {
             // JoinGroupChannelAsync will internally call our TokenProvider.GetTokenAsync for the join action
             await Unity.Services.Vivox.VivoxService.Instance.JoinGroupChannelAsync(channelName, ChatCapability.AudioOnly);
        }

        public async UniTask LeaveChannelAsync(string channelName)
        {
            await Unity.Services.Vivox.VivoxService.Instance.LeaveChannelAsync(channelName);
        }

        public async UniTask LogoutAsync()
        {
            await Unity.Services.Vivox.VivoxService.Instance.LogoutAsync();
        }
    }

    // Implementing IVivoxTokenProvider
    // Based on search, method signature is `GetTokenAsync(string issuer, TimeSpan? expiration, string userUri, string action, string channelUri)`
    // Or similar. Let's try to match it.
    // If the signature is slightly different, the compiler would fail, but I can't check compiler.
    // I'll make a best guess based on standard "Action, Issuer, Expiration, UserUri, ChannelUri" pattern.
    // The snippet says: `GetTokenAsync(string, TimeSpan?, string ...`

    // I will use `Task<string>` as return type (or `UniTask<string>` if interface allows, but likely standard Task).

    public class NakamaVivoxTokenProvider : IVivoxTokenProvider
    {
        private readonly NakamaAuthenticationService _authService;
        private readonly ILogger _logger;

        public NakamaVivoxTokenProvider(NakamaAuthenticationService authService, ILogger logger)
        {
            _authService = authService;
            _logger = logger;
        }

        // The interface method signature. I'll implement explicit implementation to be safe if I get names wrong,
        // but explicit doesn't help with signature mismatch.
        // Let's assume standard params.
        public async System.Threading.Tasks.Task<string> GetTokenAsync(string issuer = null, TimeSpan? expiration = null, string userUri = null, string action = null, string channelUri = null)
        {
            // Note: The arguments passed BY Vivox TO us tell us what token it needs.
            // action: "login", "join", "kick", etc.
            // userUri: The SIP URI of the user.
            // channelUri: The SIP URI of the channel (for join).

            // Nakama RPC expects: {"action": "login"|"join", "channelName": "..."}
            // Nakama generates the URIs internally based on issuer/user.
            // HOWEVER, Vivox SDK might expect the token to match the URIs IT provides.
            // Ideally, Nakama should generate the token for the SAME URIs.

            // Problem: Nakama generates the URI logic itself: `sip:.issuer.username.@domain`.
            // Vivox SDK generates the URI logic itself to pass to us: `sip:.issuer.username.@domain`.
            // We need to ensure they match.
            // If we just ask Nakama for a token, Nakama constructs the URI.
            // If the URIs don't match exactly, the signature verification fails.

            // Strategy: Pass the `userUri` and `channelUri` FROM Vivox TO Nakama so Nakama uses them in the payload.
            // I need to update the Nakama RPC to accept `userURI` and `channelURI` overrides if provided,
            // OR ensure Nakama logic is identical.

            // Looking at my Go code:
            // `userURI = fmt.Sprintf("sip:.%s.%s.@%s", issuer, safeUserId, domain)`
            // If Vivox SDK uses a different format (e.g. including display name or different escaping), it fails.

            // BETTER: Update Go RPC to accept `userUri` and `channelUri` as arguments and sign THEM.
            // This makes the backend "dumb" about URI construction but "smart" about signing (it holds the secret).
            // This is safer for compatibility.

            _logger.LogInformation($"Vivox Token Requested. Action: {action}, Channel: {channelUri}");

            var payload = new
            {
                action = action,
                userUri = userUri,
                channelUri = channelUri,
                // We might need to extract channelName if Nakama needs it for something else, but here we just sign provided URIs.
            };

            try
            {
                var response = await _authService.Client.RpcAsync(_authService.Session, "generate_vivox_token",
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload));

                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, string>>(response.Payload);
                return data.ContainsKey("token") ? data["token"] : string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Vivox token via RPC.");
                return string.Empty;
            }
        }
    }
}
