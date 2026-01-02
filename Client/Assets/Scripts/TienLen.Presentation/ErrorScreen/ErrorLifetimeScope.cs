using TienLen.Presentation.ErrorScreen;
using VContainer;
using VContainer.Unity;

namespace TienLen.Presentation
{
    public class ErrorLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Register Presenter
            builder.Register<ErrorPresenter>(Lifetime.Scoped);

            // Register View (Component in Hierarchy)
            builder.RegisterComponentInHierarchy<ErrorView>();
        }
    }
}
