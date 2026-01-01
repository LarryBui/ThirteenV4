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
            // Register HomeView within this scope.
            // Using RegisterComponentInHierarchy as it is already placed in the scene.
            builder.RegisterComponentInHierarchy<HomeView>();
            builder.RegisterComponentInHierarchy<HomeChatView>();
        }
    }
}
