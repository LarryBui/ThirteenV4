namespace TienLen.Application
{
    // DTO to hold player display information
    public struct PlayerAvatar
    {
        public string UserId;
        public string DisplayName;
        public int AvatarIndex;

        public PlayerAvatar(string userId, string displayName, int avatarIndex)
        {
            UserId = userId;
            DisplayName = displayName;
            AvatarIndex = avatarIndex;
        }
    }
}
