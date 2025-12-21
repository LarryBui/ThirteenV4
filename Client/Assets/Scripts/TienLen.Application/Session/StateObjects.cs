namespace TienLen.Application.Session
{
    // Simple Data Holders (POCOs)
    
    public class IdentityState
    {
        public string UserId { get; private set; }
        public string DisplayName { get; private set; }
        public int AvatarIndex { get; private set; }
        public long Balance { get; private set; }
        public bool IsLoggedIn => !string.IsNullOrEmpty(UserId);

        public IdentityState(string id, string name, int avatar, long balance) 
        { 
            UserId = id; 
            DisplayName = name; 
            AvatarIndex = avatar;
            Balance = balance;
        }

        public static IdentityState Empty => new IdentityState(null, null, 0, 0);
    }

    public class MatchState
    {
        public string MatchId { get; private set; }
        public int SeatIndex { get; private set; }
        public bool IsInMatch => !string.IsNullOrEmpty(MatchId);

        public MatchState(string matchId, int seat)
        {
            MatchId = matchId; 
            SeatIndex = seat;
        }

        public static MatchState Empty => new MatchState(null, -1);
    }
}
