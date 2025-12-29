using System.Collections;
using TMPro;
using UnityEngine;

namespace TienLen.Presentation.GameRoomScreen.Views
{
    /// <summary>
    /// Displays a single balance change (e.g. "+500", "-200").
    /// Typically instantiated at a player's seat anchor at the end of the game.
    /// </summary>
    public sealed class BalanceChangeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _amountText;
        [SerializeField] private Color _winColor = Color.green;
        [SerializeField] private Color _loseColor = Color.red;
        [SerializeField] private float _animationDuration = 1.0f;

        public void SetAmount(long amount, Vector2 startOffset)
        {
            if (_amountText == null) return;

            if (amount > 0)
            {
                _amountText.text = $"+{amount:N0}";
                _amountText.color = _winColor;
            }
            else
            {
                _amountText.text = $"{amount:N0}";
                _amountText.color = _loseColor;
            }
            
            gameObject.SetActive(true);
            StartCoroutine(AnimateArrivalRoutine(startOffset));
        }

        private IEnumerator AnimateArrivalRoutine(Vector2 startOffset)
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector2 endPos = Vector2.zero;
            Vector2 startPos = startOffset; // Start away from anchor
            
            float elapsed = 0f;
            while (elapsed < _animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _animationDuration);
                // Ease Out Quad
                t = t * (2 - t);
                
                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                yield return null;
            }
            rt.anchoredPosition = endPos;
        }
    }
}
