using VContainer;
using VContainer.Unity;
using UnityEngine;
using TienLen.Presentation.HomeScreen.Views; // Required for MonoBehaviour related registrations

namespace TienLen.Presentation.HomeScreen
{
    public class HomeLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Register Presenter
            builder.Register<TienLen.Presentation.HomeScreen.Presenters.HomePresenter>(Lifetime.Scoped);

            // Register View (Component in Hierarchy)
            builder.RegisterComponentInHierarchy<HomeView>();
            builder.RegisterComponentInHierarchy<HomeChatView>();
        }
    }
}
