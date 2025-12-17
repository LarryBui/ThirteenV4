namespace TienLen.Application.Session
{
    public class GameSessionContext : IGameSessionContext
    {
        public IdentityState Identity { get; private set; } = IdentityState.Empty;
        public MatchState CurrentMatch { get; private set; } = MatchState.Empty;

        public void SetIdentity(string userId, string displayName, int avatarIndex)
        {
            Identity = new IdentityState(userId, displayName, avatarIndex);
        }

        public void SetMatch(string matchId, int seatIndex)
        {
            CurrentMatch = new MatchState(matchId, seatIndex);
        }

        public void ClearMatch()
        {
            CurrentMatch = MatchState.Empty;
        }

        public void ClearSession()
        {
            Identity = IdentityState.Empty;
            CurrentMatch = MatchState.Empty;
        }
    }
}
