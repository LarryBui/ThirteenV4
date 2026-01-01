using Cysharp.Threading.Tasks;

namespace TienLen.Application.Voice
{
    public interface IVoiceChatService
    {
        UniTask InitializeAsync();
        UniTask JoinChannelAsync(string matchId);
        UniTask LeaveChannelAsync();
        UniTask<string> RequestAuthTokenAsync(string matchId);
    }
}
