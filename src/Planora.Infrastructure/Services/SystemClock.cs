using Planora.Application.Abstractions.Common;

namespace Planora.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}