using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace CDUniTap.Extensions;

public static class DependencyInjectionExtensions
{
    public static void AddServicesImplements<TInterface>(this IServiceCollection service, Assembly? assembly = null)
    {
        if (assembly is null) assembly = Assembly.GetExecutingAssembly();
        foreach (var b in assembly.GetTypes().Where(t=>!t.IsAbstract && !t.IsInterface && typeof(TInterface).IsAssignableFrom(t)).ToList())
        {
            service.AddSingleton(typeof(TInterface), b);
        }
    }
    
    public static void AddServicesAsSelfImplements<TInterface>(this IServiceCollection service, Assembly? assembly = null)
    {
        if (assembly is null) assembly = Assembly.GetExecutingAssembly();
        foreach (var b in assembly.GetTypes().Where(t=>!t.IsAbstract && !t.IsInterface && typeof(TInterface).IsAssignableFrom(t)).ToList())
        {
            service.AddSingleton(b);
        }
    }
}