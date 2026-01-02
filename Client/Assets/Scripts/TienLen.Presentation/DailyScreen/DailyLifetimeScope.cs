using TienLen.Application.Ads;
using TienLen.Application.Economy;
using TienLen.Infrastructure.Services;
using TienLen.Presentation.DailyScreen.Presenters;
using TienLen.Presentation.DailyScreen.Views;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TienLen.Presentation
{
    public class DailyLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Register View found in hierarchy and ensure injection
            builder.RegisterComponentInHierarchy<DailyView>();
            
            // Register Services
            builder.Register<IAdService, MockAdService>(Lifetime.Scoped);
            builder.Register<ICurrencyNKService, MockCurrencyNKService>(Lifetime.Scoped);

            // Register Presenter (as regular dependency)
            builder.Register<DailyPresenter>(Lifetime.Scoped);
        }
    }
}