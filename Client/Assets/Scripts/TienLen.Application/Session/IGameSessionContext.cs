namespace TienLen.Application.Session
{
    // The "Big Aggregate" Interface
    public interface IGameSessionContext
    {
        // 1. Identity (Authentication Results)
        IdentityState Identity { get; }

        // 2. Match (Current Room Info)
        MatchState CurrentMatch { get; }

        // Methods to mutate state
        void SetIdentity(string userId, string displayName, int avatarIndex);
        void SetMatch(string matchId, int seatIndex);
        void ClearMatch();
        void ClearSession(); // Logout
    }
}
