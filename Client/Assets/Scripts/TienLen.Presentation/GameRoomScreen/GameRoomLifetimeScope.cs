using TienLen.Presentation.GameRoomScreen.Views;
using TienLen.Presentation.GameRoomScreen.Services;
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

            // Register Presenter
            builder.Register<GameRoomPresenter>(Lifetime.Scoped);
        }
    }
}