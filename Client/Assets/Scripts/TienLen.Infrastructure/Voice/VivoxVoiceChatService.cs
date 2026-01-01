using Cysharp.Threading.Tasks;
using TienLen.Application.Voice;

namespace TienLen.Infrastructure.Voice
{
    public class VivoxVoiceChatService : IVoiceChatService
    {
        public UniTask InitializeAsync() => UniTask.CompletedTask;

        public UniTask JoinChannelAsync(string matchId) => UniTask.CompletedTask;

        public UniTask LeaveChannelAsync() => UniTask.CompletedTask;
    }
}
