using System.Collections.Generic;
using UnityEngine;
using VContainer;
using TienLen.Application;
using TienLen.Presentation.GameRoomScreen.Components; // For calculating relative seat index if needed, or we implement logic here

namespace TienLen.Presentation.GameRoomScreen.Views
{
    /// <summary>
    /// Manages the display of balance changes at the end of the game.
    /// Listens to OnGameEnded and instantiates BalanceChangeView at the correct seat anchors.
    /// </summary>
    public class GameEndBalanceManagerView : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private BalanceChangeView _balanceChangePrefab;

        [Header("Anchors")]
        [Tooltip("Anchor for Local player (South / Relative Index 0)")]
        [SerializeField] private RectTransform _southAnchor;

        [Tooltip("Anchor for East seat (Relative Index 1)")]
        [SerializeField] private RectTransform _eastAnchor;
        
        [Tooltip("Anchor for North seat (Relative Index 2)")]
        [SerializeField] private RectTransform _northAnchor;
        
        [Tooltip("Anchor for West seat (Relative Index 3)")]
        [SerializeField] private RectTransform _westAnchor;

        [Tooltip("Distance from anchor towards center to start animation")]
        [SerializeField] private float _startDistance = 100f;

        private GameRoomPresenter _presenter;
        
        // Keep track of instantiated views to clean them up on new game or restart
        private readonly List<GameObject> _activeViews = new List<GameObject>();

        [Inject]
        public void Construct(GameRoomPresenter presenter)
        {
            _presenter = presenter;
        }

        private void Start()
        {
            if (_presenter != null)
            {
                _presenter.OnGameEnded += HandleGameEnded;
                _presenter.OnGameStarted += HandleGameStarted;
            }
        }

        private void OnDestroy()
        {
            if (_presenter != null)
            {
                _presenter.OnGameEnded -= HandleGameEnded;
                _presenter.OnGameStarted -= HandleGameStarted;
            }
        }

        private void HandleGameStarted()
        {
            ClearViews();
        }

        private void HandleGameEnded(GameEndedResultDto result)
        {
            ClearViews();

            if (result == null || result.BalanceChanges == null) return;
            if (_presenter == null) return;

            var match = _presenter.CurrentMatch;
            if (match == null) return;

            int localSeatIndex = match.LocalSeatIndex;

            foreach (var kvp in result.BalanceChanges)
            {
                string userId = kvp.Key;
                long amount = kvp.Value;

                int seatIndex = _presenter.FindSeatByUserId(userId);
                if (seatIndex == -1) continue;

                // Calculate relative index (0=South/Local, 1=East, 2=North, 3=West)
                // Formula: (seatIndex - localSeatIndex + 4) % 4
                int relativeIndex = (seatIndex - localSeatIndex + 4) % 4;

                RectTransform targetAnchor = GetAnchorByRelativeIndex(relativeIndex);
                if (targetAnchor != null)
                {
                    CreateBalanceView(targetAnchor, amount, relativeIndex);
                }
            }
        }

        private RectTransform GetAnchorByRelativeIndex(int relativeIndex)
        {
            switch (relativeIndex)
            {
                case 0: return _southAnchor;
                case 1: return _eastAnchor;
                case 2: return _northAnchor;
                case 3: return _westAnchor;
                default: return null;
            }
        }

        private void CreateBalanceView(RectTransform anchor, long amount, int relativeIndex)
        {
            if (_balanceChangePrefab == null || anchor == null) return;

            var instance = Instantiate(_balanceChangePrefab, anchor);
            var rt = instance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.zero;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
            }

            Vector2 offset = GetStartOffset(relativeIndex);
            instance.SetAmount(amount, offset);
            _activeViews.Add(instance.gameObject);
        }

        private Vector2 GetStartOffset(int relativeIndex)
        {
            // Calculate offset towards center (assuming standard table layout)
            // 0=South (Bottom) -> Start Up (0, +dist)
            // 1=East (Right)   -> Start Left (-dist, 0)
            // 2=North (Top)    -> Start Down (0, -dist)
            // 3=West (Left)    -> Start Right (+dist, 0)
            switch (relativeIndex)
            {
                case 0: return new Vector2(0, _startDistance);
                case 1: return new Vector2(-_startDistance, 0);
                case 2: return new Vector2(0, -_startDistance);
                case 3: return new Vector2(_startDistance, 0);
                default: return Vector2.zero;
            }
        }

        private void ClearViews()
        {
            foreach (var view in _activeViews)
            {
                if (view != null) Destroy(view);
            }
            _activeViews.Clear();
        }
    }
}
