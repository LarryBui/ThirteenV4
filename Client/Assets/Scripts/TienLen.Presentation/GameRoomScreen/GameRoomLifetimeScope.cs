using VContainer;
using VContainer.Unity;

namespace TienLen.Presentation.GameRoomScreen
{
    public class GameRoomLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Register GameRoomController component in hierarchy so it gets injected
            builder.RegisterComponentInHierarchy<GameRoomController>();
            // Register CardDealer component in hierarchy so it gets injected
            builder.RegisterComponentInHierarchy<CardDealer>();
        }
    }
}