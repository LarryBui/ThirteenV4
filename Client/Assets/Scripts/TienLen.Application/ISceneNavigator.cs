using Cysharp.Threading.Tasks;

namespace TienLen.Application
{
    public interface ISceneNavigator
    {
        UniTask LoadGameRoomAsync();
        UniTask UnloadGameRoomAsync();
    }
}
