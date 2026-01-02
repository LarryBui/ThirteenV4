using TienLen.Application.Ads;
using TienLen.Application.Economy;
using UnityEngine.SceneManagement;

namespace TienLen.Presentation.DailyScreen.Presenters
{
    public class DailyPresenter
    {
        private readonly IAdService _adService;
        private readonly ICurrencyNKService _currencyService;

        public DailyPresenter(
            IAdService adService,
            ICurrencyNKService currencyService)
        {
            _adService = adService;
            _currencyService = currencyService;
        }

        public async void HandleAdClicked(int index, System.Action<bool, string> onStateUpdate)
        {
            onStateUpdate?.Invoke(false, "Watching...");
            
            bool success = await _adService.ShowRewardedAdAsync($"daily_ad_{index}");
            
            if (success)
            {
                await _currencyService.AddGoldAsync(1000);
                onStateUpdate?.Invoke(false, "Claimed");
            }
            else
            {
                onStateUpdate?.Invoke(true, "Watch Ad");
            }
        }

        public void HandleClose()
        {
            SceneManager.UnloadSceneAsync("Daily");
        }
    }
}