using System;
using TienLen.Application;
using TienLen.Domain.Services;
using TienLen.Infrastructure.Match;
using TienLen.Infrastructure.Services;
using TienLen.Presentation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TienLen.Infrastructure.Config
{
    public class GameLifetimeScope : LifetimeScope
    {
        [Header("Nakama Settings")]
        [SerializeField] private string scheme = NakamaConfig.DefaultScheme;
        [SerializeField] private string host = NakamaConfig.DefaultHost;
        [SerializeField] private int port = NakamaConfig.DefaultPort;
        [SerializeField] private string serverKey = NakamaConfig.DefaultServerKey;

        protected override void Configure(IContainerBuilder builder)
        {
            var deviceId = SystemInfo.deviceUniqueIdentifier;
            Debug.LogWarning("Device ID: " + deviceId);
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                deviceId = Guid.NewGuid().ToString();
                Debug.LogWarning("Device ID is empty; generated a temporary GUID for Nakama auth.");
            }

            var nakamaConfig = new NakamaConfig(deviceId, scheme, host, port, serverKey);
            builder.RegisterInstance<ITienLenAppConfig>(nakamaConfig);

            // Register Auth Service as interface and self (so MatchClient can access internal Socket)
            builder.Register<NakamaAuthenticationService>(Lifetime.Singleton)
                .As<IAuthenticationService>()
                .AsSelf();

            // Register Match Infrastructure
            builder.Register<NakamaMatchClient>(Lifetime.Singleton)
                .As<IMatchNetworkClient>();

            // Register Application Handler
            builder.Register<TienLenMatchHandler>(Lifetime.Singleton);

            builder.RegisterComponentInHierarchy<HomeUIController>();
            builder.RegisterComponentInHierarchy<GameRoomController>();

            builder.RegisterEntryPoint<GameStartup>();
        }
    }
}
