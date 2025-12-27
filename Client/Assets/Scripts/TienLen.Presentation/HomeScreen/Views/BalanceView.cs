using TMPro;
using UnityEngine;
using VContainer;
using TienLen.Application; // For IAuthenticationService
using TienLen.Application.Session; // For IGameSessionContext

namespace TienLen.Presentation.HomeScreen.Views
{
    public class BalanceView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _balanceText;

        private IGameSessionContext _sessionContext;
        private IAuthenticationService _authService;

        [Inject]
        public void Construct(IGameSessionContext sessionContext, IAuthenticationService authService)
        {
            _sessionContext = sessionContext;
            _authService = authService;
        }

        private void Start()
        {
            if (_authService != null)
            {
                _authService.OnAuthenticated += HandleAuthenticated;
            }
            
            RefreshBalance();
        }

        private void OnDestroy()
        {
            if (_authService != null)
            {
                _authService.OnAuthenticated -= HandleAuthenticated;
            }
        }

        private void HandleAuthenticated()
        {
            RefreshBalance();
        }

        private void RefreshBalance()
        {
            if (_balanceText == null) return;

            long balance = 0;
            if (_sessionContext != null && _sessionContext.Identity != null)
            {
                balance = _sessionContext.Identity.Balance;
            }

            _balanceText.text = balance.ToString("N0");
        }
    }
}
