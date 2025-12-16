using System;
using UnityEngine;
using VContainer;
using TienLen.Domain.Services;
using Cysharp.Threading.Tasks;

namespace TienLen.Presentation
{
    public class GameRoomController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private CardDealer _cardDealer;

        private IMatchNetworkClient _matchClient;

        [Inject]
        public void Construct(IMatchNetworkClient matchClient)
        {
            _matchClient = matchClient;
        }

        private void Start()
        {
            if (_matchClient != null)
            {
                _matchClient.OnGameStarted += HandleGameStarted;
            }
            else
            {
                Debug.LogWarning("GameRoomController: IMatchNetworkClient not injected.");
            }
        }

        private void OnDestroy()
        {
            if (_matchClient != null)
            {
                _matchClient.OnGameStarted -= HandleGameStarted;
            }
        }

        private void HandleGameStarted()
        {
            Debug.Log("GameRoomController: Game Started! Triggering Deal Animation.");
            // 52 cards, 2.0 seconds duration
            _cardDealer.AnimateDeal(52, 2.0f).Forget();
        }
    }
}
