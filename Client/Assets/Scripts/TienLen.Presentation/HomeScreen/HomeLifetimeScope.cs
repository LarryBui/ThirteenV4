using VContainer;
using VContainer.Unity;
using UnityEngine; // Required for MonoBehaviour related registrations

namespace TienLen.Presentation.HomeScreen
{
    public class HomeLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Register HomeUIController within this scope.
            // VContainer will find the component in the current scene hierarchy (the Home scene)
            // and inject its dependencies.
            builder.RegisterComponentInHierarchy<HomeUIController>();
            builder.RegisterComponentInHierarchy<HomeChatController>();
        }
    }
}
