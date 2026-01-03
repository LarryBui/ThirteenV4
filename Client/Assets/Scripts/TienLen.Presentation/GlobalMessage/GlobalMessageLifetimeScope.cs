using TienLen.Presentation.GlobalMessage.Presenters;
using TienLen.Presentation.GlobalMessage.Views;
using VContainer;
using VContainer.Unity;

namespace TienLen.Presentation
{
    public class GlobalMessageLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<GlobalMessagePresenter>(Lifetime.Scoped);
            builder.RegisterComponentInHierarchy<GlobalMessageView>();
        }
    }
}
