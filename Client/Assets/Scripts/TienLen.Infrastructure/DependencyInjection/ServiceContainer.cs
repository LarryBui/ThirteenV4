using System;
using System.Collections.Generic;

namespace TienLen.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Very small global container for registering and resolving singleton services.
    /// Keep usage limited to composition root to avoid hidden coupling.
    /// </summary>
    public sealed class ServiceContainer
    {
        private static readonly Lazy<ServiceContainer> LazyInstance = new(() => new ServiceContainer());
        private readonly Dictionary<Type, object> _singletons = new();
        private readonly object _lock = new();

        public static ServiceContainer Instance => LazyInstance.Value;

        private ServiceContainer()
        {
        }

        public void RegisterSingleton<TService>(TService instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var key = typeof(TService);
            lock (_lock)
            {
                if (_singletons.ContainsKey(key))
                {
                    throw new InvalidOperationException($"Service {key.Name} is already registered.");
                }

                _singletons[key] = instance;
            }
        }

        public TService Resolve<TService>()
        {
            if (TryResolve<TService>(out var service))
            {
                return service;
            }

            throw new InvalidOperationException($"Service {typeof(TService).Name} is not registered.");
        }

        public bool TryResolve<TService>(out TService service)
        {
            var key = typeof(TService);
            lock (_lock)
            {
                if (_singletons.TryGetValue(key, out var instance) && instance is TService typed)
                {
                    service = typed;
                    return true;
                }
            }

            service = default;
            return false;
        }
    }
}
