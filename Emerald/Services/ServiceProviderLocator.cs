using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonServiceLocator;

namespace Emerald.Services;

//Bruh I can't get uno services to work. help me.

public class ServiceProviderLocator : IServiceLocator
{
    private readonly IServiceProvider _serviceProvider;

    public ServiceProviderLocator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public object GetService(Type serviceType)
    {
        return _serviceProvider.GetService(serviceType)
               ?? throw new InvalidOperationException($"Service of type {serviceType.FullName} not found.");
    }

    public object GetInstance(Type serviceType)
    {
        return GetService(serviceType);
    }

    public object GetInstance(Type serviceType, string key)
    {
        // For simplicity, ignoring `key`.
        return GetInstance(serviceType);
    }

    public IEnumerable<object> GetAllInstances(Type serviceType)
    {
        // I asked AI and told me this
        var services = (IEnumerable<object>)_serviceProvider.GetService(typeof(IEnumerable<>).MakeGenericType(serviceType));
        return services ?? Array.Empty<object>();
    }

    public TService GetInstance<TService>()
    {
        return (TService)GetInstance(typeof(TService));
    }

    public TService GetInstance<TService>(string key)
    {
        return (TService)GetInstance(typeof(TService), key);
    }

    public IEnumerable<TService> GetAllInstances<TService>()
    {
        return (IEnumerable<TService>)GetAllInstances(typeof(TService));
    }
}
