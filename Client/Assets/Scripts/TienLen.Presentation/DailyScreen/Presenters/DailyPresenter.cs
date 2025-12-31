using TienLen.Presentation.DailyScreen.Views;
using VContainer.Unity;

namespace TienLen.Presentation.DailyScreen.Presenters
{
    public class DailyPresenter : IStartable
    {
        private readonly DailyView _view;

        public DailyPresenter(DailyView view)
        {
            _view = view;
        }

        public void Start()
        {
            _view.Initialize();
            _view.OnCloseClicked += HandleClose;
        }

        private void HandleClose()
        {
            // Logic to unload the scene is handled by the parent scope or scene manager,
            // but usually the View might trigger a self-unload or the Presenter asks a service.
            // For this simple implementation, we'll let the View handle the Unity-specific unload 
            // or bubble up an event.
            // However, strictly sticking to MVP, the Presenter should decide.
            
            // For now, we will simply log or invoke a callback if we had a navigation service.
            // Since we are loading additively, we might want to just unload the scene here.
            
            UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("Daily");
        }
    }
}
