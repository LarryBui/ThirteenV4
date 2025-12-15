using System;
using TienLen.Application;
using TienLen.Domain.Services;
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
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                deviceId = Guid.NewGuid().ToString();
                Debug.LogWarning("Device ID is empty; generated a temporary GUID for Nakama auth.");
            }

            var nakamaConfig = new NakamaConfig(deviceId, scheme, host, port, serverKey);
            builder.RegisterInstance<ITienLenAppConfig>(nakamaConfig);

            builder.Register<NakamaAuthenticationService>(Lifetime.Singleton).As<IAuthenticationService>();

            builder.RegisterComponentInHierarchy<HomeUIController>();

            builder.RegisterEntryPoint<GameStartup>();
        }
    }
}
