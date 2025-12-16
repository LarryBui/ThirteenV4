using System;
using TienLen.Application;
using TienLen.Infrastructure.Config;
using TienLen.Infrastructure.Match;
using TienLen.Infrastructure.Services;
using TienLen.Presentation; // Needed for BootstrapUIController
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TienLen.Global
{
    public class GameLifetimeScope : LifetimeScope
    {
        [Header("Nakama Settings")]
        [SerializeField] private string scheme = NakamaConfig.DefaultScheme;
        [SerializeField] private string host = NakamaConfig.DefaultHost;
        [SerializeField] private int port = NakamaConfig.DefaultPort;
        [SerializeField] private string serverKey = NakamaConfig.DefaultServerKey;

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(this.gameObject);
        }

        protected override void Configure(IContainerBuilder builder)
        {
            var deviceId = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                deviceId = Guid.NewGuid().ToString();
            }

            var nakamaConfig = new NakamaConfig(deviceId, scheme, host, port, serverKey);
            builder.RegisterInstance<ITienLenAppConfig>(nakamaConfig);

            // Register Auth Service as interface and self
            builder.Register<NakamaAuthenticationService>(Lifetime.Singleton)
                .As<IAuthenticationService>()
                .AsSelf();

            // Register Match Infrastructure
            builder.Register<NakamaMatchClient>(Lifetime.Singleton)
                .As<IMatchNetworkClient>();

            // Register Application Handler
            builder.Register<TienLenMatchHandler>(Lifetime.Singleton);

            // Register Bootstrap UI (Component in Hierarchy) so it receives Injection
            builder.RegisterComponentInHierarchy<BootstrapUIController>();
        }
    }
}