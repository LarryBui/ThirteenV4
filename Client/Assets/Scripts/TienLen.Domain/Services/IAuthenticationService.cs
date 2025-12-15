using System.Threading.Tasks;
using Nakama;

namespace TienLen.Domain.Services
{
    public interface IAuthenticationService
    {
        ISession Session { get; }
        IClient Client { get; }
        ISocket Socket { get; }

        Task AuthenticateAndConnectAsync();
    }
}
