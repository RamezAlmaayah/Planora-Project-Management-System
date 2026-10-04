using Planora.Web.Presence;

namespace Planora.IntegrationTests;

public sealed class UserPresenceRegistryTests
{
    [Fact]
    public void PresenceTracksMultipleConnectionsAndThrottlesLastSeenUpdates()
    {
        var registry = new UserPresenceRegistry();
        Assert.True(registry.Connect("user", "connection-1"));
        Assert.False(registry.Connect("user", "connection-2"));
        Assert.True(registry.IsOnline("user"));
        Assert.True(registry.TryTouch("user", TimeSpan.FromMinutes(2)));
        Assert.False(registry.TryTouch("user", TimeSpan.FromMinutes(2)));
        Assert.False(registry.Disconnect("user", "connection-1"));
        Assert.True(registry.IsOnline("user"));
        Assert.True(registry.Disconnect("user", "connection-2"));
        Assert.False(registry.IsOnline("user"));
    }
}
