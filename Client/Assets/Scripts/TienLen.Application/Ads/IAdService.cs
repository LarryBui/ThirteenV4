using Cysharp.Threading.Tasks;

namespace TienLen.Application.Ads
{
    public interface IAdService
    {
        /// <summary>
        /// Shows a rewarded ad to the player.
        /// </summary>
        /// <param name="placementId">The ID of the ad placement.</param>
        /// <returns>True if the ad was completed successfully, false otherwise.</returns>
        UniTask<bool> ShowRewardedAdAsync(string placementId);
    }
}
