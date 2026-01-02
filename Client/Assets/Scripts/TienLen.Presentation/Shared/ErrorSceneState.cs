namespace TienLen.Presentation.Shared
{
    /// <summary>
    /// Holds the latest error scene payload for display.
    /// </summary>
    public sealed class ErrorSceneState
    {
        public string Message { get; private set; } = string.Empty;
        public string PreviousSceneName { get; private set; } = string.Empty;

        public void Set(string message, string previousSceneName)
        {
            Message = message ?? string.Empty;
            PreviousSceneName = previousSceneName ?? string.Empty;
        }

        public void Clear()
        {
            Message = string.Empty;
            PreviousSceneName = string.Empty;
        }
    }
}
