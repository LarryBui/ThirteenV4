using System;
using TienLen.Presentation.Shared;
using UnityEngine.SceneManagement;

namespace TienLen.Presentation.ErrorScreen
{
    /// <summary>
    /// Presenter for the Error Screen.
    /// Manages the logic of retrieving error data and handling navigation.
    /// </summary>
    public sealed class ErrorPresenter : IDisposable
    {
        public string ErrorMessage => ErrorContext.CurrentErrorMessage;
        public string PreviousSceneName => ErrorContext.PreviousSceneName;

        public void GoBack()
        {
            string target = string.IsNullOrEmpty(PreviousSceneName) ? "Home" : PreviousSceneName;
            ErrorContext.Clear();

            var previousScene = SceneManager.GetSceneByName(target);
            if (previousScene.IsValid() && previousScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousScene);
                SceneManager.UnloadSceneAsync(ErrorContext.ErrorSceneName);
                return;
            }

            SceneManager.LoadScene(target);
        }

        public void Dispose()
        {
            // No resources to release yet
        }
    }
}
