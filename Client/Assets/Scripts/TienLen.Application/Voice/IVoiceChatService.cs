using System;
using Cysharp.Threading.Tasks;

namespace TienLen.Application.Voice
{
    public interface IVoiceChatService
    {
        event Action<string, string, bool> OnSpeechMessageReceived; // senderId, message, isFromSelf
        UniTask InitializeAsync();
        UniTask JoinChannelAsync(string matchId);
        UniTask LeaveChannelAsync();
        UniTask<string> RequestAuthTokenAsync(string matchId);
        UniTask EnableSpeechToTextAsync(bool active);
    }
}
