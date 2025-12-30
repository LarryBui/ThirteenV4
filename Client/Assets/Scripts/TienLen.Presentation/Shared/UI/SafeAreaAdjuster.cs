using UnityEngine;

namespace TienLen.Presentation.Shared.UI
{
    /// <summary>
    /// Adjusts the RectTransform to match the device's Safe Area (notches, home bars).
    /// Place this on a root container within your Canvas. Immersive backgrounds should remain 
    /// outside this container, while functional UI (buttons, text) should be children of it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class SafeAreaAdjuster : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea = new Rect(0, 0, 0, 0);
        private Vector2 _lastScreenSize = new Vector2(0, 0);
        private ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            Rect safeArea = Screen.safeArea;

            // Only apply changes if the safe area or screen metrics have changed
            if (safeArea != _lastSafeArea ||
                Screen.width != _lastScreenSize.x ||
                Screen.height != _lastScreenSize.y ||
                Screen.orientation != _lastOrientation)
            {
                ApplySafeArea(safeArea);
            }
        }

        private void ApplySafeArea(Rect r)
        {
            _lastSafeArea = r;
            _lastScreenSize.x = Screen.width;
            _lastScreenSize.y = Screen.height;
            _lastOrientation = Screen.orientation;

            // Convert safe area rectangle from pixels to normalized anchor coordinates (0..1)
            Vector2 anchorMin = r.position;
            Vector2 anchorMax = r.position + r.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            // Handle potential edge cases where Screen.width/height might be 0 during initialization
            if (anchorMin.x >= 0 && anchorMax.x <= 1 && anchorMin.y >= 0 && anchorMax.y <= 1)
            {
                _rectTransform.anchorMin = anchorMin;
                _rectTransform.anchorMax = anchorMax;
                
                // Ensure offsets are zeroed out so it perfectly matches the anchors
                _rectTransform.offsetMin = Vector2.zero;
                _rectTransform.offsetMax = Vector2.zero;
            }
        }
    }
}
