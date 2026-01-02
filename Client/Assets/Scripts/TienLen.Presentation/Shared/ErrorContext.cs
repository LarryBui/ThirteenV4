using UnityEngine.SceneManagement;

namespace TienLen.Presentation.Shared
{
    /// <summary>
    /// Static context for handling critical error navigation and state.
    /// Allows any part of the application to trigger the Error Scene.
    /// </summary>
    public static class ErrorContext
    {
        public const string ErrorSceneName = "ErrorScene";

        /// <summary>
        /// The error message to display.
        /// </summary>
        public static string CurrentErrorMessage { get; private set; }

        /// <summary>
        /// The name of the scene that triggered the error, allowing for a "Back" action.
        /// </summary>
        public static string PreviousSceneName { get; private set; }

        /// <summary>
        /// Navigates to the Error Scene with a specific message.
        /// Automatically records the current scene as the "Previous Scene".
        /// </summary>
        /// <param name="message">The friendly error message to display to the user.</param>
        public static void ShowError(string message)
        {
            CurrentErrorMessage = message;
            PreviousSceneName = SceneManager.GetActiveScene().name;

            var errorScene = SceneManager.GetSceneByName(ErrorSceneName);
            if (errorScene.IsValid() && errorScene.isLoaded)
            {
                SceneManager.SetActiveScene(errorScene);
                return;
            }

            var loadOperation = SceneManager.LoadSceneAsync(ErrorSceneName, LoadSceneMode.Additive);
            if (loadOperation == null)
            {
                return;
            }

            loadOperation.completed += _ =>
            {
                var loadedScene = SceneManager.GetSceneByName(ErrorSceneName);
                if (loadedScene.IsValid())
                {
                    SceneManager.SetActiveScene(loadedScene);
                }
            };
        }

        /// <summary>
        /// Clears the error context.
        /// </summary>
        public static void Clear()
        {
            CurrentErrorMessage = string.Empty;
            PreviousSceneName = string.Empty;
        }
    }
}
