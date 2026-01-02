using Cysharp.Threading.Tasks;

namespace TienLen.Application.Economy
{
    public interface ICurrencyNKService
    {
        /// <summary>
        /// Adds gold to the player's account via the backend.
        /// </summary>
        UniTask AddGoldAsync(long amount);

        /// <summary>
        /// Retrieves the current gold balance from the backend.
        /// </summary>
        UniTask<long> GetCurrentGoldAsync();
    }
}
