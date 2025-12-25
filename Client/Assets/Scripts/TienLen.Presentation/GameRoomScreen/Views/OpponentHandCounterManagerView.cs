using UnityEngine;
using VContainer;
using TienLen.Presentation.GameRoomScreen.Views;

namespace TienLen.Presentation.GameRoomScreen.Components
{
    /// <summary>
    /// Manages the lifecycle and updates of opponent hand counters.
    /// Dynamically instantiates counters at specified anchors.
    /// </summary>
    public class OpponentHandCounterManagerView : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private OpponentHandCounterView _counterPrefab;

        [Header("Anchors")]
        [Tooltip("Anchor for East seat (Relative Index 1)")]
        [SerializeField] private RectTransform _eastAnchor;
        
        [Tooltip("Anchor for North seat (Relative Index 2)")]
        [SerializeField] private RectTransform _northAnchor;
        
        [Tooltip("Anchor for West seat (Relative Index 3)")]
        [SerializeField] private RectTransform _westAnchor;

        private GameRoomPresenter _presenter;
        
        private OpponentHandCounterView _eastInstance;
        private OpponentHandCounterView _northInstance;
        private OpponentHandCounterView _westInstance;

        [Inject]
        public void Construct(GameRoomPresenter presenter)
        {
            _presenter = presenter;
        }

        private void Awake()
        {
            InitializeInstances();
        }

        private void Start()
        {
            if (_presenter != null)
            {
                _presenter.OnSeatCardCountUpdated += HandleSeatCardCountUpdated;
                _presenter.OnGameStarted += HandleGameStarted;
            }
            
            SetAllCounts(0);
        }

        private void OnDestroy()
        {
            if (_presenter != null)
            {
                _presenter.OnSeatCardCountUpdated -= HandleSeatCardCountUpdated;
                _presenter.OnGameStarted -= HandleGameStarted;
            }
        }

        private void InitializeInstances()
        {
            if (_counterPrefab == null) return;

            _eastInstance = CreateInstance(_eastAnchor);
            _northInstance = CreateInstance(_northAnchor);
            _westInstance = CreateInstance(_westAnchor);
        }

        private OpponentHandCounterView CreateInstance(RectTransform anchor)
        {
            if (anchor == null) return null;
            
            var instance = Instantiate(_counterPrefab, anchor);
            var rt = instance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.zero;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
            }
            return instance;
        }

        private void HandleGameStarted()
        {
            SetAllCounts(0);
        }

        private void HandleSeatCardCountUpdated(int seatIndex, int newCount)
        {
            // seatIndex is the Relative Index passed from GameRoomView
            // 1 = East, 2 = North, 3 = West
            switch (seatIndex)
            {
                case 1:
                    _eastInstance?.SetCount(newCount);
                    break;
                case 2:
                    _northInstance?.SetCount(newCount);
                    break;
                case 3:
                    _westInstance?.SetCount(newCount);
                    break;
            }
        }

        private void SetAllCounts(int count)
        {
            _eastInstance?.SetCount(count);
            _northInstance?.SetCount(count);
            _westInstance?.SetCount(count);
        }
    }
}