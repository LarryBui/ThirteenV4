using System.Collections.Generic;
using TienLen.Domain.ValueObjects;

namespace TienLen.Domain.Aggregates
{
    public class Player
    {
        public string UserID { get; set; }
        public int Seat { get; set; } // 1-based seat number
        public bool IsOwner { get; set; } // true if match owner
        public List<Card> Hand { get; set; }
        public bool HasPassed { get; set; }
        public bool Finished { get; set; }
    }
}
