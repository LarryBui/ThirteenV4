using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using TienLen.Application;
using TienLen.Presentation; // To access HomeUIController

namespace TienLen.Infrastructure.Services
{
    public class SceneNavigator : ISceneNavigator
    {
        private readonly HomeUIController _homeUIController;

        // VContainer will inject HomeUIController from the scene hierarchy if registered/found.
        public SceneNavigator(HomeUIController homeUIController)
        {
            _homeUIController = homeUIController;
        }

        public async UniTask LoadGameRoomAsync()
        {
            Debug.Log("SceneNavigator: Loading GameRoom scene...");

            // Hide Home UI before loading GameRoom
            _homeUIController?.SetHomeUIVisibility(false);

            await SceneManager.LoadSceneAsync("GameRoom", LoadSceneMode.Additive);
            Debug.Log("SceneNavigator: GameRoom scene loaded.");
        }

        public async UniTask UnloadGameRoomAsync()
        {
            Debug.Log("SceneNavigator: Unloading GameRoom scene...");

            // Unload GameRoom
            await SceneManager.UnloadSceneAsync("GameRoom");

            // Show Home UI after unloading GameRoom
            _homeUIController?.SetHomeUIVisibility(true);
            Debug.Log("SceneNavigator: GameRoom scene unloaded. Home UI shown.");
        }
    }
}
