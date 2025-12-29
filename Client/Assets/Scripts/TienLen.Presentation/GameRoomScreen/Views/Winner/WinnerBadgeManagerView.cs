using System.Collections.Generic;
using UnityEngine;
using VContainer;
using TienLen.Domain.ValueObjects; // For Card

namespace TienLen.Presentation.GameRoomScreen.Views
{
    public sealed class WinnerBadgeManagerView : MonoBehaviour
    {
        [Header("Anchors (0=South, 1=East, 2=North, 3=West)")]
        [SerializeField] private RectTransform[] _anchors;

        [Header("Prefabs")]
        [SerializeField] private WinnerBadgeView _badgePrefab;

        [Header("Settings")]
        [Tooltip("Offset applied to the North badge to position it to the left of the avatar.")]
        [SerializeField] private Vector2 _northOffset = new Vector2(-100, 0); 

        private GameRoomPresenter _presenter;
        private readonly List<WinnerBadgeView> _activeBadges = new();

        [Inject]
        public void Construct(GameRoomPresenter presenter)
        {
            _presenter = presenter;
        }

        private void Start()
        {
            if (_presenter == null) 
            {
                Debug.LogError("[WinnerBadgeManager] Presenter is NULL! Injection failed.");
                return;
            }
            Debug.Log("[WinnerBadgeManager] Started and listening for events.");
            _presenter.OnPlayerFinished += HandlePlayerFinished;
            _presenter.OnGameStarted += HandleGameStarted;
        }

        private void OnDestroy()
        {
            if (_presenter != null)
            {
                _presenter.OnPlayerFinished -= HandlePlayerFinished;
                _presenter.OnGameStarted -= HandleGameStarted;
            }
        }

        private void HandleGameStarted()
        {
            Debug.Log("[WinnerBadgeManager] Game Started - Clearing badges.");
            ClearBadges();
        }

        private void HandlePlayerFinished(int seatIndex, int rank)
        {
            Debug.Log($"[WinnerBadgeManager] Player Finished: Seat {seatIndex}, Rank {rank}");
            var match = _presenter.CurrentMatch;
            if (match == null) return;

            int localSeat = match.LocalSeatIndex >= 0 ? match.LocalSeatIndex : 0;

            // Calculate relative index: 0=South (Local), 1=East, 2=North, 3=West
            int relativeIndex = (seatIndex - localSeat + 4) % 4;
            Debug.Log($"[WinnerBadgeManager] Mapping Seat {seatIndex} -> Relative {relativeIndex}");

            ShowBadge(relativeIndex, rank);
        }

        private void ShowBadge(int relativeIndex, int rank)
        {
            if (relativeIndex < 0 || relativeIndex >= _anchors.Length)
            {
                Debug.LogError($"[WinnerBadgeManager] Invalid Relative Index: {relativeIndex}. Anchors count: {_anchors.Length}");
                return;
            }
            
            var anchor = _anchors[relativeIndex];
            if (anchor == null)
            {
                Debug.LogError($"[WinnerBadgeManager] Anchor at {relativeIndex} is NULL!");
                return;
            }

            if (_badgePrefab == null)
            {
                Debug.LogError("[WinnerBadgeManager] Badge Prefab is NULL!");
                return;
            }

            var badge = Instantiate(_badgePrefab, anchor);
            Debug.Log($"[WinnerBadgeManager] Spawning Badge for Relative {relativeIndex} (Rank {rank}) at {anchor.name}");
            
            // Default Position (Centered on anchor)
            badge.transform.localPosition = Vector3.zero;
            badge.transform.localRotation = Quaternion.identity;
            badge.transform.localScale = Vector3.one;
            
            // Specific Logic for North (Relative Index 2)
            if (relativeIndex == 2)
            {
                if (badge.TryGetComponent<RectTransform>(out var rect))
                {
                    rect.anchoredPosition += _northOffset;
                }
            }

            // Visual Logic
            if (rank == 1)
            {
                Debug.Log($"[WinnerBadgeManager] CALLING ShowFirstPlace for Relative Index {relativeIndex}");
                badge.ShowFirstPlace();
            }
            else
            {
                string label = rank == 2 ? "Second" : (rank == 3 ? "Third" : "Last");
                badge.ShowRank(label);
            }

            _activeBadges.Add(badge);
        }

        private void ClearBadges()
        {
            foreach (var badge in _activeBadges)
            {
                if (badge != null) Destroy(badge.gameObject);
            }
            _activeBadges.Clear();
        }
    }
}
