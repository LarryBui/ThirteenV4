using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace TienLen.Application
{
    public class BootstrapFlow : IStartable
    {
        public void Start()
        {
            LoadHomeAsync().Forget();
        }

        private async UniTask LoadHomeAsync()
        {
            Debug.Log("BootstrapFlow: Loading Home scene additively...");
            // Load Home scene
            await SceneManager.LoadSceneAsync("Home", LoadSceneMode.Additive);
            Debug.Log("BootstrapFlow: Home scene loaded.");
        }
    }
}
