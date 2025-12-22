using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TienLen.Presentation.GameRoomScreen
{
    /// <summary>
    /// Renders a player's avatar and display name in the GameRoom UI.
    /// </summary>
    public class PlayerProfileUI : MonoBehaviour
    {
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private TMP_Text turnDeadlineText;
        [SerializeField] private Sprite[] avatarSprites; // Assign your avatar sprites here in the Inspector
        private ILogger<PlayerProfileUI> _logger = NullLogger<PlayerProfileUI>.Instance;

        /// <summary>
        /// Seat index assigned to this profile (-1 when unassigned).
        /// </summary>
        public int SeatIndex { get; private set; } = -1;

        /// <summary>
        /// Assigns a logger instance used for diagnostics.
        /// </summary>
        /// <param name="logger">Logger for this profile component.</param>
        public void SetLogger(ILogger<PlayerProfileUI> logger)
        {
            _logger = logger ?? NullLogger<PlayerProfileUI>.Instance;
        }

        /// <summary>
        /// Updates the profile display with the provided name, avatar index, and seat index.
        /// </summary>
        /// <param name="displayName">Name to show on the profile card.</param>
        /// <param name="avatarIndex">Index used to select the avatar sprite.</param>
        /// <param name="seatIndex">Seat index assigned to this profile.</param>
        public void SetProfile(string displayName, int avatarIndex, int seatIndex)
        {
            SeatIndex = seatIndex;
            HideTurnDeadlineTick();
            if (avatarImage == null)
            {
                _logger.LogError("PlayerProfileUI: Avatar Image is not assigned.");
                return;
            }
            if (displayNameText == null)
            {
                _logger.LogError("PlayerProfileUI: Display Name Text is not assigned.");
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
                _logger.LogWarning("PlayerProfileUI: No avatar sprites assigned or array is empty. Using default.");
                // Optionally set a default sprite or hide the image
            }
        }

        /// <summary>
        /// Returns the avatar image transform used as the visual anchor for animations.
        /// </summary>
        /// <returns>Avatar rect when available, otherwise the profile root rect.</returns>
        public RectTransform GetAvatarAnchor()
        {
            if (avatarImage != null) return avatarImage.rectTransform;
            return transform as RectTransform;
        }

        /// <summary>
        /// Shows the provided turn countdown value above the avatar.
        /// </summary>
        /// <param name="secondsRemaining">Seconds remaining before the turn expires.</param>
        public void ShowTurnCountdownSeconds(int secondsRemaining)
        {
            if (turnDeadlineText == null) return;

            var clampedSeconds = Mathf.Max(0, secondsRemaining);
            turnDeadlineText.text = clampedSeconds.ToString();
            turnDeadlineText.gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides the turn deadline tick display.
        /// </summary>
        public void HideTurnDeadlineTick()
        {
            if (turnDeadlineText == null) return;

            turnDeadlineText.text = string.Empty;
            turnDeadlineText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Clears the profile visuals back to empty defaults.
        /// </summary>
        public void ClearProfile()
        {
            displayNameText.text = "";
            avatarImage.sprite = null; // Or set a default placeholder
            SeatIndex = -1;
            HideTurnDeadlineTick();
        }

        /// <summary>
        /// Toggles the profile UI object active state.
        /// </summary>
        /// <param name="isActive">True to show the profile, false to hide it.</param>
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
    }
}
