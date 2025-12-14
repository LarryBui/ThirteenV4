using VContainer;
using VContainer.Unity;
using TienLen.Domain.Services;

using UnityEngine;

namespace TienLen.Infrastructure.Config
{
    public class GameLifetimeScope : LifetimeScope
    {
        [Header("Nakama Settings")]
        [SerializeField] private string scheme = "http";
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 7350;
        [SerializeField] private string serverKey = "defaultkey";

        protected override void Configure(IContainerBuilder builder)
        {
            // 1. Configuration
            // var nakamaConfig = new NakamaConfig
            // {
            //     Scheme = scheme,
            //     Host = host,
            //     Port = port,
            //     ServerKey = serverKey
            // };
            // builder.RegisterInstance(nakamaConfig);

            // // 2. Services
            // // builder.Register<NakamaAuthenticationService>(Lifetime.Singleton).As<IAuthenticationService>();

            // // 3. Entry Points (Application Logic)
            // builder.RegisterEntryPoint<TienLen.Application.GameStartup>();
        }
    }
}
