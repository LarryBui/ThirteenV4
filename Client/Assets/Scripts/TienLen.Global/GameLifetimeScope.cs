using System;
using TienLen.Application;
using TienLen.Application.Errors;
using Microsoft.Extensions.Logging;
using TienLen.Application.Session;
using TienLen.Infrastructure.Config;
using TienLen.Infrastructure.Logging;
using TienLen.Infrastructure.Match;
using TienLen.Application.Chat;
using TienLen.Application.Speech;
using TienLen.Infrastructure.Chat;
using TienLen.Infrastructure.Errors;
using TienLen.Infrastructure.Speech;
using TienLen.Application.Voice;
using TienLen.Infrastructure.Voice;
using TienLen.Infrastructure.Services;
using TienLen.Presentation.BootstrapScreen; // Needed for BootstrapView
using TienLen.Presentation.Shared;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TienLen.Global
{
    public class GameLifetimeScope : LifetimeScope
    {
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

            // Load Nakama server settings from an external JSON file in StreamingAssets.
            // This allows changing the Host/Port without recompiling the project.
            var nakamaConfig = ConfigLoader.Load(deviceId);
            builder.RegisterInstance<ITienLenAppConfig>(nakamaConfig);

            // Register Session Context
            builder.Register<GameSessionContext>(Lifetime.Singleton)
                .As<IGameSessionContext>();

            // Register Application Error Bus
            builder.Register<AppErrorBus>(Lifetime.Singleton)
                .As<IAppErrorBus>();

            // Register Application Error Handler
            builder.Register<AppErrorHandler>(Lifetime.Singleton);

            // Register Application Error Presenter
            builder.Register<AppErrorPresenter>(Lifetime.Singleton);

            // Register Logging
            builder.Register<ZLoggerService>(Lifetime.Singleton);
            builder.Register<ILoggerFactory>(container => container.Resolve<ZLoggerService>().LoggerFactory, Lifetime.Singleton);
            builder.Register(typeof(ILogger<>), typeof(Logger<>), Lifetime.Singleton);

            // Register Auth Service as interface and self
            builder.Register<NakamaAuthenticationService>(Lifetime.Singleton)
                .As<IAuthenticationService>()
                .AsSelf();

            // Register Match Infrastructure
            builder.Register<NakamaMatchClient>(Lifetime.Singleton)
                .As<IMatchNetworkClient>();

            // Register Chat Infrastructure
            builder.Register<NakamaChatClient>(Lifetime.Singleton)
                .As<IChatNetworkClient>();
            builder.Register<GlobalChatHandler>(Lifetime.Singleton);

            // Register Speech-to-Text
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            builder.Register<WindowsSpeechToTextService>(Lifetime.Singleton)
                .As<ISpeechToTextService>();
#else
            builder.Register<UnsupportedSpeechToTextService>(Lifetime.Singleton)
                .As<ISpeechToTextService>();
#endif

            // Register Application Handler
            builder.Register<TienLenMatchHandler>(Lifetime.Singleton);

            // Register Vivox Config
            var vivoxConfig = Resources.Load<VivoxConfig>("VivoxConfig");
            builder.RegisterInstance(vivoxConfig);

            // Register Socket for injection
            builder.Register(container => container.Resolve<NakamaAuthenticationService>().Socket, Lifetime.Singleton);

            // Register Voice Chat Service
            builder.Register<VivoxVoiceChatService>(Lifetime.Singleton)
                .As<IVoiceChatService>();

            // Register Bootstrap UI (Component in Hierarchy) so it receives Injection
            builder.RegisterComponentInHierarchy<BootstrapView>();

            builder.RegisterBuildCallback(container => container.Resolve<AppErrorPresenter>());
        }
    }
}
