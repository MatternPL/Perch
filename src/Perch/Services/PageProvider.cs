namespace Perch.Services;

/// <summary>
/// What NavigationView asks for a page instance. Perch has no DI container and its
/// pages talk to the static services on <see cref="App"/>, so this just builds each
/// page once and hands the same instance back — which also keeps scroll position and
/// list selection when you switch tabs and come back.
/// </summary>
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
