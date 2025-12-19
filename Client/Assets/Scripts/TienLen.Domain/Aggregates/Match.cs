using System;
using System.Collections.Generic;
using TienLen.Domain.ValueObjects;

namespace TienLen.Domain.Aggregates
{
    /// <summary>
    /// Client-side match state modeled to mirror the server's MatchState.
    /// </summary>
    public class Match
    {
        /// <summary>Match identifier.</summary>
        public string Id { get; }
        /// <summary>Lifecycle phase (e.g., lobby, playing, ended).</summary>
        public string Phase { get; set; }
        /// <summary>UserId -> Player lookup.</summary>
        public Dictionary<string, Player> Players { get; }
        /// <summary>Seats indexed 0..N-1 containing userIds or empty strings.</summary>
        public string[] Seats { get; }
        /// <summary>Seat of the current owner (0-based).</summary>
        public int OwnerSeat { get; set; }
        /// <summary>Seat whose turn it is (0-based).</summary>
        public int CurrentTurnSeat { get; set; }
        /// <summary>Seat that last played cards (0-based).</summary>
        public int LastPlaySeat { get; set; }
        /// <summary>Finish order (userIds) as players empty their hands.</summary>
        public List<string> FinishOrder { get; }
        /// <summary>Cards currently on the board.</summary>
        public List<Card> CurrentBoard { get; private set; }

        /// <summary>
        /// Initialize a new match with the given seat count (defaults to 4, matching server).
        /// </summary>
        /// <param name="id">Match identifier.</param>
        /// <param name="seatCount">Total seats available in the match.</param>
        public Match(string id, int seatCount = 4)
        {
            Id = id;
            Players = new Dictionary<string, Player>();
            Seats = new string[seatCount];
            FinishOrder = new List<string>();
            CurrentBoard = new List<Card>();
            Phase = "Lobby";
            OwnerSeat = -1;
        }

        /// <summary>
        /// Adds a player to the match, ensuring the seat array mirrors server-side state.
        /// </summary>
        /// <param name="player">Player to register.</param>
        public void RegisterPlayer(Player player)
        {
            if (Players.ContainsKey(player.UserID))
            {
                throw new InvalidOperationException($"Player {player.UserID} is already registered.");
            }

            // Client uses 0-based internally for seats array, but let's check input
            if (player.Seat < 0 || player.Seat >= Seats.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(player.Seat), "Seat number is out of range.");
            }

            if (!string.IsNullOrEmpty(Seats[player.Seat]))
            {
                 // Replace placeholder or error? Assuming replacement/update allowed for now if ID matches
                 if (Seats[player.Seat] != player.UserID)
                    throw new InvalidOperationException($"Seat {player.Seat} is already occupied by {Seats[player.Seat]}.");
            }

            Players.Add(player.UserID, player);
            Seats[player.Seat] = player.UserID;
        }

        /// <summary>
        /// Starts the game, resetting state and setting up for a new round.
        /// </summary>
        /// <param name="firstTurnSeat">The seat index (0-based) starting the game.</param>
        public void StartGame(int firstTurnSeat)
        {
            // Clear existing hands
            foreach (var player in Players.Values)
            {
                player.Hand.Clear();
                player.CardsRemaining = 13; // Standard Tien Len
                player.HasPassed = false;
                player.Finished = false;
            }

            CurrentBoard.Clear();
            FinishOrder.Clear();
            Phase = "Playing";
            CurrentTurnSeat = firstTurnSeat;
        }

        /// <summary>
        /// Updates state based on a server CardPlayed event.
        /// </summary>
        /// <param name="seat">Seat that played the cards (0-based).</param>
        /// <param name="cards">Cards played.</param>
        /// <param name="nextTurnSeat">Next turn seat (0-based).</param>
        /// <param name="newRound">Whether this play starts a new round (clears board).</param>
        public void PlayTurn(int seat, List<Card> cards, int nextTurnSeat, bool newRound)
        {
            if (Phase != "Playing") return; // Or throw

            var userId = Seats[seat];
            if (string.IsNullOrEmpty(userId) || !Players.TryGetValue(userId, out var player)) return;

            // Update hand if local or tracking known cards
            if (player.Hand.Cards.Count > 0 && player.Hand.HasCards(cards)) 
            {
                player.Hand.RemoveCards(cards);
            }
            
            player.CardsRemaining -= cards.Count;
            if (player.CardsRemaining < 0) player.CardsRemaining = 0;

            if (newRound)
            {
                CurrentBoard.Clear();
                // Reset passes? Usually handled by specific event or implied by new round start
                // Server sends "new_round" on the PLAY itself only if that play CLEARS the previous board?
                // Actually, if I play and it's a new round, it means I'm leading.
                // If I play on top of someone, newRound is false.
                // Re-reading server logic: newRound is true if "LastPlayedCombination.Type == Invalid".
                // This implies I am STARTING a round.
            }
            
            // If newRound is true, it means the board was empty (or cleared) BEFORE this play.
            // So this play IS the board.
            if (newRound) CurrentBoard.Clear();
            
            // Add cards to board? Tien Len usually replaces the "top" combo.
            // But visually we might want to stack. For logic, we just track the current combo.
            CurrentBoard = new List<Card>(cards);

            LastPlaySeat = seat;
            CurrentTurnSeat = nextTurnSeat;

            if (player.CardsRemaining == 0)
            {
                player.Finished = true;
                if (!FinishOrder.Contains(userId)) FinishOrder.Add(userId);
            }
        }

        /// <summary>
        /// Updates state based on a server TurnPassed event.
        /// </summary>
        /// <param name="seat">Seat that passed (0-based).</param>
        /// <param name="nextTurnSeat">Next turn seat (0-based).</param>
        /// <param name="newRound">Whether this pass triggered a round reset.</param>
        public void HandleTurnPassed(int seat, int nextTurnSeat, bool newRound)
        {
             if (Phase != "Playing") return;
             
             var userId = Seats[seat];
             if (!string.IsNullOrEmpty(userId) && Players.TryGetValue(userId, out var player))
             {
                 player.HasPassed = true;
             }

             CurrentTurnSeat = nextTurnSeat;

             if (newRound)
             {
                 CurrentBoard.Clear();
                 foreach(var p in Players.Values) p.HasPassed = false;
             }
        }
    }
}
