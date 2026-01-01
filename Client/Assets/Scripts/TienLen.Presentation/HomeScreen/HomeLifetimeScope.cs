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
            // Using RegisterComponentInHierarchy as it is already placed in the scene.
            builder.RegisterComponentInHierarchy<HomeUIController>();
            builder.RegisterComponentInHierarchy<HomeChatController>();
        }
    }
}
