using Cysharp.Threading.Tasks;
using TienLen.Application.Economy;
using UnityEngine;

namespace TienLen.Infrastructure.Services
{
    public class MockCurrencyNKService : ICurrencyNKService
    {
        private const string GoldKey = "Mock_Gold_Balance";

        public UniTask AddGoldAsync(long amount)
        {
            long current = GetGoldFromPrefs();
            current += amount;
            PlayerPrefs.SetString(GoldKey, current.ToString());
            PlayerPrefs.Save();
            
            Debug.Log($"[MockCurrencyNKService] Added {amount} gold. New balance: {current}");
            return UniTask.CompletedTask;
        }

        public UniTask<long> GetCurrentGoldAsync()
        {
            return UniTask.FromResult(GetGoldFromPrefs());
        }

        private long GetGoldFromPrefs()
        {
            string val = PlayerPrefs.GetString(GoldKey, "0");
            if (long.TryParse(val, out long balance))
            {
                return balance;
            }
            return 0;
        }
    }
}
