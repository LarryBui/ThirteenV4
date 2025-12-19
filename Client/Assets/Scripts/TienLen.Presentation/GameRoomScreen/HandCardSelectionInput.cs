using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TienLen.Presentation.GameRoomScreen
{
    /// <summary>
    /// Forwards UI pointer clicks on a card visual to a callback.
    /// The hosting object must be under a Canvas with a GraphicRaycaster, and there must be an EventSystem in the scene.
    /// </summary>
    public sealed class HandCardSelectionInput : MonoBehaviour, IPointerClickHandler
    {
        private Action _onClick;

        /// <summary>
        /// Binds a click callback. Calling this again replaces the previous callback.
        /// </summary>
        /// <param name="onClick">Callback invoked when the card is clicked.</param>
        public void Bind(Action onClick)
        {
            _onClick = onClick;
        }

        /// <inheritdoc />
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _onClick?.Invoke();
        }
    }
}
