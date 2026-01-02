using Cysharp.Threading.Tasks;
using TienLen.Application.Ads;
using UnityEngine;

namespace TienLen.Infrastructure.Services
{
    public class MockAdService : IAdService
    {
        public async UniTask<bool> ShowRewardedAdAsync(string placementId)
        {
            Debug.Log($"[MockAdService] Showing rewarded ad for placement: {placementId}");
            
            // Simulate ad viewing delay
            await UniTask.Delay(1000);
            
            Debug.Log("[MockAdService] Ad completed successfully.");
            return true;
        }
    }
}
