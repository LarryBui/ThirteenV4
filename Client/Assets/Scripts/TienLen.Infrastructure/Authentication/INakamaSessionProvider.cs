using Nakama;

namespace TienLen.Infrastructure.Authentication
{
    /// <summary>
    /// Exposes the active Nakama session for services that need the raw transport session.
    /// </summary>
    public interface INakamaSessionProvider
    {
        ISession Session { get; }
    }
}
