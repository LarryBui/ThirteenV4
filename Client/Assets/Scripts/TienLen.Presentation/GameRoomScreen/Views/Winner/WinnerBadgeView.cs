using UnityEngine;
using TMPro;

namespace TienLen.Presentation.GameRoomScreen.Views
{
    public sealed class WinnerBadgeView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _animationRoot;
        [SerializeField] private GameObject _textRoot;
        [SerializeField] private TMP_Text _rankText;

        public void ShowFirstPlace()
        {
            gameObject.SetActive(true);
            if (_animationRoot != null) _animationRoot.SetActive(true);
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
