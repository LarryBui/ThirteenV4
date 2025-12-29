using UnityEngine;
using TMPro;

namespace TienLen.Presentation.GameRoomScreen.Views
{
    public sealed class WinnerBadgeView : MonoBehaviour
    {
        [Header("References")]
        // Updated field to GameObject to support legacy prefabs
        [SerializeField] private GameObject _animationRoot;
        [SerializeField] private GameObject _textRoot;
        [SerializeField] private TMP_Text _rankText;

        [Header("Settings")]
        [SerializeField] private string _animationStateName = "Winner_Badge";

        public void ShowFirstPlace()
        {
            Debug.Log($"[WinnerBadgeView] ShowFirstPlace called on {name}");
            gameObject.SetActive(true);
            if (_animationRoot != null) 
            {
                _animationRoot.SetActive(true);
                // Use GetComponentInChildren to find the Animator if it's on a child object (like 'Visuals')
                var anim = _animationRoot.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    Debug.Log($"[WinnerBadgeView] Animator found on {anim.gameObject.name}. Playing '{_animationStateName}'...");
                    anim.enabled = true;
                    anim.Play(_animationStateName, 0, 0f);
                }
                else
                {
                    Debug.LogWarning($"[WinnerBadgeView] No Animator found on {_animationRoot.name}!");
                }
            }
            else
            {
                Debug.LogWarning("[WinnerBadgeView] _animationRoot is NULL!");
            }
            if (_textRoot != null) _textRoot.SetActive(false);
        }

        public void ShowRank(string rankLabel)
        {
            gameObject.SetActive(true);
            if (_animationRoot != null) _animationRoot.SetActive(false);
            if (_textRoot != null) 
            {
                _textRoot.SetActive(true);
                if (_rankText != null) _rankText.text = rankLabel;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
