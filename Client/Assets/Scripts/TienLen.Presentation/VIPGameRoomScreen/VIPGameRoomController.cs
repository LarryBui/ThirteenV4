using System;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TienLen.Infrastructure.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace TienLen.Presentation.VIPGameRoomScreen
{
    public class VIPGameRoomController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button muteButton;
        [SerializeField] private TMPro.TMP_Text statusText;

        private IVivoxService _vivoxService;
        private ILogger<VIPGameRoomController> _logger;
        private string _roomName = "VIPRoom"; // Could be dynamic

        [Inject]
        public void Construct(IVivoxService vivoxService, ILogger<VIPGameRoomController> logger)
        {
            _vivoxService = vivoxService;
            _logger = logger ?? NullLogger<VIPGameRoomController>.Instance;
        }

        private void Awake()
        {
            leaveButton?.onClick.AddListener(HandleLeaveClicked);
            muteButton?.onClick.AddListener(HandleMuteClicked);
        }

        private async void Start()
        {
            if (statusText) statusText.text = "Connecting to Voice...";

            try
            {
                await _vivoxService.LoginAsync();
                await _vivoxService.JoinChannelAsync(_roomName);
                if (statusText) statusText.text = "Connected. Speak now.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to join voice channel.");
                if (statusText) statusText.text = "Voice Connection Failed.";
            }
        }

        private async void OnDestroy()
        {
            try
            {
                await _vivoxService.LeaveChannelAsync(_roomName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving channel.");
            }
        }

        private void HandleLeaveClicked()
        {
            // Unload this scene
            SceneManager.UnloadSceneAsync(gameObject.scene);
        }

        private void HandleMuteClicked()
        {
            // Toggle mute logic here
            // _vivoxService.ToggleMute(); // Needs implementation in service
            _logger.LogInformation("Mute clicked (Not implemented)");
        }
    }
}
