using System;
using UnityEngine;
using UnityEngine.UI;

namespace TienLen.Presentation.GameRoomScreen.Views
{
    /// <summary>
    /// Dumb view component that manages the state and visuals of the action buttons.
    /// It exposes C# events for user interactions and methods for the Presenter/Root to update its state.
    /// </summary>
    public sealed class ActionButtonsView : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _startGameButton;
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _passButton;
        [SerializeField] private Button _leaveButton;

        [Header("Validation Visuals")]
        [Tooltip("Tint applied to the Play button when the current selection is invalid.")]
        [SerializeField] private Color _playButtonInvalidTint = new Color(1f, 0.75f, 0.75f, 1f);
        [Tooltip("Blend amount for the invalid selection tint.")]
        [Range(0f, 1f)]
        [SerializeField] private float _playButtonInvalidTintStrength = 0.35f;
        [Tooltip("Tint applied to the Pass button when no valid play can beat the board.")]
        [SerializeField] private Color _passButtonHighlightTint = new Color(0.85f, 1f, 0.85f, 1f);
        [Tooltip("Blend amount for the pass highlight tint.")]
        [Range(0f, 1f)]
        [SerializeField] private float _passButtonHighlightStrength = 0.35f;

        /// <summary>Raised when the Start Game button is clicked.</summary>
        public event Action StartGameClicked;
        /// <summary>Raised when the Play button is clicked.</summary>
        public event Action PlayClicked;
        /// <summary>Raised when the Pass button is clicked.</summary>
        public event Action PassClicked;
        /// <summary>Raised when the Leave button is clicked.</summary>
        public event Action LeaveClicked;

        private ColorBlock _playButtonDefaultColors;
        private bool _playButtonColorsCached;
        private ColorBlock _passButtonDefaultColors;
        private bool _passButtonColorsCached;

        private void Awake()
        {
            _startGameButton?.onClick.AddListener(() => StartGameClicked?.Invoke());
            _playButton?.onClick.AddListener(() => PlayClicked?.Invoke());
            _passButton?.onClick.AddListener(() => PassClicked?.Invoke());
            _leaveButton?.onClick.AddListener(() => LeaveClicked?.Invoke());
        }

        public void SetStartButtonVisible(bool visible) => _startGameButton?.gameObject.SetActive(visible);
        public void SetStartButtonInteractable(bool interactable)
        {
            if (_startGameButton != null) _startGameButton.interactable = interactable;
        }

        public void SetActionButtonsVisible(bool visible)
        {
            _playButton?.gameObject.SetActive(visible);
            _passButton?.gameObject.SetActive(visible);
        }

        public void SetPlayButtonInteractable(bool interactable)
        {
            if (_playButton != null) _playButton.interactable = interactable;
        }

        public void SetPassButtonInteractable(bool interactable)
        {
            if (_passButton != null) _passButton.interactable = interactable;
        }

        public void SetLeaveButtonInteractable(bool interactable)
        {
            if (_leaveButton != null) _leaveButton.interactable = interactable;
        }

        /// <summary>
        /// Updates the visual state of the Play button based on whether the current card selection is valid.
        /// </summary>
        public void SetPlayButtonValidationVisual(bool isValid)
        {
            if (_playButton == null) return;
            EnsurePlayButtonColorsCached();

            if (isValid)
            {
                _playButton.colors = _playButtonDefaultColors;
                return;
            }

            var invalidColors = _playButtonDefaultColors;
            invalidColors.normalColor = TintColor(_playButtonDefaultColors.normalColor, _playButtonInvalidTint, _playButtonInvalidTintStrength);
            invalidColors.highlightedColor = TintColor(_playButtonDefaultColors.highlightedColor, _playButtonInvalidTint, _playButtonInvalidTintStrength);
            invalidColors.pressedColor = TintColor(_playButtonDefaultColors.pressedColor, _playButtonInvalidTint, _playButtonInvalidTintStrength);
            invalidColors.selectedColor = TintColor(_playButtonDefaultColors.selectedColor, _playButtonInvalidTint, _playButtonInvalidTintStrength);
            _playButton.colors = invalidColors;
        }

        /// <summary>
        /// Updates the visual state of the Pass button, potentially highlighting it if it's the recommended move.
        /// </summary>
        public void SetPassButtonHighlight(bool highlight)
        {
            if (_passButton == null) return;
            EnsurePassButtonColorsCached();

            if (!highlight)
            {
                _passButton.colors = _passButtonDefaultColors;
                return;
            }

            var colors = _passButtonDefaultColors;
            colors.normalColor = TintColor(_passButtonDefaultColors.normalColor, _passButtonHighlightTint, _passButtonHighlightStrength);
            colors.highlightedColor = TintColor(_passButtonDefaultColors.highlightedColor, _passButtonHighlightTint, _passButtonHighlightStrength);
            colors.pressedColor = TintColor(_passButtonDefaultColors.pressedColor, _passButtonHighlightTint, _passButtonHighlightStrength);
            colors.selectedColor = TintColor(_passButtonDefaultColors.selectedColor, _passButtonHighlightTint, _passButtonHighlightStrength);
            _passButton.colors = colors;
        }

        private void EnsurePlayButtonColorsCached()
        {
            if (_playButtonColorsCached || _playButton == null) return;
            _playButtonDefaultColors = _playButton.colors;
            _playButtonColorsCached = true;
        }

        private void EnsurePassButtonColorsCached()
        {
            if (_passButtonColorsCached || _passButton == null) return;
            _passButtonDefaultColors = _passButton.colors;
            _passButtonColorsCached = true;
        }

        private static Color TintColor(Color baseColor, Color tint, float strength)
        {
            return Color.Lerp(baseColor, tint, Mathf.Clamp01(strength));
        }
    }
}
