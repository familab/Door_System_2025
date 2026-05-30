namespace DoorApp.Familab.Domain;

/// <summary>Abstraction over the clock so time-dependent services can be tested deterministically.</summary>
public interface ISystemClock
{
    DateTimeOffset Now { get; }
}

/// <summary>Default clock backed by the wall clock.</summary>
public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
