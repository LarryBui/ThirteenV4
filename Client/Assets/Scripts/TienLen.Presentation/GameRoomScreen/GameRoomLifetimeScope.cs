using TienLen.Presentation.GameRoomScreen.DebugTools;
using TienLen.Presentation.GameRoomScreen.Services;
using TienLen.Presentation.GameRoomScreen.Views;
using TienLen.Application.Voice;
using VContainer;
using VContainer.Unity;

namespace TienLen.Presentation.GameRoomScreen
{
    public class GameRoomLifetimeScope : LifetimeScope
    {
        [UnityEngine.SerializeField] private TienLen.Infrastructure.Config.AvatarRegistry _avatarRegistry;

        protected override void Configure(IContainerBuilder builder)
        {
            if (_avatarRegistry != null)
            {
                builder.RegisterInstance(_avatarRegistry);
            }

            // Register GameRoomView component in hierarchy so it gets injected
            builder.RegisterComponentInHierarchy<GameRoomView>();
            // Register CardDealer component in hierarchy so it gets injected
            builder.RegisterComponentInHierarchy<CardDealer>();

            // Register Local Hand View
            builder.RegisterComponentInHierarchy<TienLen.Presentation.GameRoomScreen.Views.LocalHandView>();

            // Register Hand Counter Manager
            builder.RegisterComponentInHierarchy<TienLen.Presentation.GameRoomScreen.Components.OpponentHandCounterManagerView>();

            // Register Timer Manager
            builder.RegisterComponentInHierarchy<TurnTimerManagerView>();

            // Register Voice Chat View
            builder.RegisterComponentInHierarchy<VoiceChatView>();

            // Register Winner Badge Manager
            builder.RegisterComponentInHierarchy<TienLen.Presentation.GameRoomScreen.Views.WinnerBadgeManagerView>();

            // Register Balance Manager
            builder.RegisterComponentInHierarchy<TienLen.Presentation.GameRoomScreen.Views.GameEndBalanceManagerView>();

            // Register Rigged Deck Debug View
            builder.RegisterComponentInHierarchy<RiggedDeckDebugView>();

            // Register Voice Chat Handler
            builder.Register<VoiceChatHandler>(Lifetime.Scoped);

            // Register Presenter
            builder.Register<GameRoomPresenter>(Lifetime.Scoped);
        }
    }
}
