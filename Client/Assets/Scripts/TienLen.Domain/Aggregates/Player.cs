using System.Collections.Generic;
using TienLen.Domain.ValueObjects;

namespace TienLen.Domain.Aggregates
{
    public class Player
    {
        public string UserID { get; set; }
        public string DisplayName { get; set; } // Added for UI display
        public int AvatarIndex { get; set; }   // Added for avatar selection
        public int Seat { get; set; } // 1-based seat number
        public bool IsOwner { get; set; } // true if match owner
        public Hand Hand { get; private set; } = new Hand();
        public bool HasPassed { get; set; }
        public bool Finished { get; set; }
    }
}
