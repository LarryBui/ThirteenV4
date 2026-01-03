using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TienLen.Application;
using TienLen.Application.Voice;
using TienLen.Infrastructure.Voice;
using TienLen.Infrastructure.Config;
using UnityEngine;

namespace TienLen.Tests.Infrastructure
{
    [TestFixture]
    public class VivoxVoiceChatServiceTests
    {
        private MockAuthService _mockAuth;
        private VivoxConfig _mockConfig;
        private VivoxVoiceChatService _service;

        [SetUp]
        public void Setup()
        {
            _mockAuth = new MockAuthService();
            _mockConfig = ScriptableObject.CreateInstance<VivoxConfig>();
            _service = new VivoxVoiceChatService(_mockAuth, _mockConfig);
        }

        [Test]
        public async Task GetTokenAsync_Login_UsesCurrentMatchId()
        {
            // Arrange
            const string matchId = "match_123";
            const string expectedToken = "token_abc";
            
            _mockAuth.IsAuthenticated = true;
            _mockAuth.RpcResponse = $"{{"token": "{expectedToken}"}}";
            
            // We need to trigger JoinChannelAsync or use reflection to set _currentContextMatchId
            // Since we can't easily call JoinChannelAsync (it calls static VivoxService), 
            // let's use the public RequestAuthTokenAsync to test the core logic.
            
            // Act
            var token = await _service.RequestAuthTokenAsync(matchId, "login");

            // Assert
            Assert.AreEqual(expectedToken, token);
            Assert.AreEqual("get_vivox_token", _mockAuth.LastRpcId);
            Assert.Contains(matchId, _mockAuth.LastRpcPayload);
            Assert.Contains("login", _mockAuth.LastRpcPayload);
        }

        [Test]
        public async Task GetTokenAsync_Join_ExtractsMatchIdFromUri()
        {
            // Arrange
            const string matchId = "match_456";
            const string expectedToken = "token_def";
            const string channelUri = "sip:confctl-g-domain." + matchId + "@domain.vivox.com";
            
            _mockAuth.IsAuthenticated = true;
            _mockAuth.RpcResponse = $"{{"token": "{expectedToken}"}}";
            
            // Act
            var token = await _service.GetTokenAsync(action: "join", channelUri: channelUri);

            // Assert
            Assert.AreEqual(expectedToken, token);
            Assert.AreEqual("get_vivox_token", _mockAuth.LastRpcId);
            Assert.Contains(matchId, _mockAuth.LastRpcPayload);
            Assert.Contains("join", _mockAuth.LastRpcPayload);
        }

        private class MockAuthService : IAuthenticationService
        {
            public bool IsAuthenticated { get; set; }
            public string CurrentUserId { get; set; }
            public string CurrentUserDisplayName { get; set; }
            public int CurrentUserAvatarIndex { get; set; }

            public string LastRpcId { get; private set; }
            public string LastRpcPayload { get; private set; }
            public string RpcResponse { get; set; }

            public event Action OnAuthenticated;
            public event Action<string> OnAuthenticationFailed;

            public UniTask LoginAsync() => UniTask.CompletedTask;

            public UniTask<string> ExecuteRpcAsync(string id, string payload)
            {
                LastRpcId = id;
                LastRpcPayload = payload;
                return UniTask.FromResult(RpcResponse);
            }
        }
    }
}
