using System;
using TienLen.Presentation.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TienLen.Presentation.ErrorScreen
{
    /// <summary>
    /// Presenter for the Error Screen.
    /// Manages error handling, navigation, and UI data for the Error Scene.
    /// </summary>
    public sealed class ErrorPresenter
    {
        private const string ErrorSceneName = "ErrorScene";
        private readonly ErrorSceneState _state;

        public string ErrorMessage => _state.Message;
        public string PreviousSceneName => _state.PreviousSceneName;

        public ErrorPresenter(ErrorSceneState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            UnityEngine.Debug.Log($"[ErrorPresenter] Initialized. Message: '{_state.Message}'");
        }

        public void GoBack()
        {
            UnityEngine.Debug.Log($"[ErrorPresenter] GoBack called. Target Scene: {_state.PreviousSceneName}");
            string target = string.IsNullOrWhiteSpace(_state.PreviousSceneName) ? "Home" : _state.PreviousSceneName;
            _state.Clear();

            var previousScene = SceneManager.GetSceneByName(target);
            if (previousScene.IsValid() && previousScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousScene);
                SceneManager.UnloadSceneAsync(ErrorSceneName);
                return;
            }

            SceneManager.LoadScene(target);
        }
    }
}
