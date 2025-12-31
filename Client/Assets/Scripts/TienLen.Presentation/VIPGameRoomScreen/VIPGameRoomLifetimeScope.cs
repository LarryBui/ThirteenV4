using TienLen.Presentation.VIPGameRoomScreen.Views;
using TienLen.Presentation.VIPGameRoomScreen;
using VContainer;
using VContainer.Unity;

namespace TienLen.Presentation
{
    public class VIPGameRoomLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<VoiceChatPresenter>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<VIPGameRoomView>();
        }
    }
}
