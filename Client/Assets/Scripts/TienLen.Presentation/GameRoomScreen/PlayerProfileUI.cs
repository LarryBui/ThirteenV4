using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TienLen.Presentation
{
    public class PlayerProfileUI : MonoBehaviour
    {
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private Sprite[] avatarSprites; // Assign your avatar sprites here in the Inspector

        public void SetProfile(string displayName, int avatarIndex)
        {
            if (avatarImage == null)
            {
                Debug.LogError("PlayerProfileUI: Avatar Image is not assigned.");
                return;
            }
            if (displayNameText == null)
            {
                Debug.LogError("PlayerProfileUI: Display Name Text is not assigned.");
                return;
            }

            displayNameText.text = displayName;

            if (avatarSprites != null && avatarSprites.Length > 0)
            {
                int effectiveIndex = avatarIndex % avatarSprites.Length;
                if (effectiveIndex < 0) effectiveIndex += avatarSprites.Length; // Ensure positive index

                avatarImage.sprite = avatarSprites[effectiveIndex];
            }
            else
            {
                Debug.LogWarning("PlayerProfileUI: No avatar sprites assigned or array is empty. Using default.");
                // Optionally set a default sprite or hide the image
            }
        }

        public void ClearProfile()
        {
            displayNameText.text = "";
            avatarImage.sprite = null; // Or set a default placeholder
        }

        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
    }
}
