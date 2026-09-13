namespace Perch.Services;

public sealed class PageProvider : IServiceProvider
{
    private readonly Dictionary<Type, object> _pages = new();

    public object? GetService(Type serviceType)
    {
        if (_pages.TryGetValue(serviceType, out var existing)) return existing;

        var page = Activator.CreateInstance(serviceType);
        if (page is not null) _pages[serviceType] = page;

        return page;
    }
}
