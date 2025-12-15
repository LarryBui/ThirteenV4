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
        /// <summary>UserId of the current owner.</summary>
        public string OwnerUserID { get; set; }
        /// <summary>Seat whose turn it is (1-based).</summary>
        public int CurrentTurnSeat { get; set; }
        /// <summary>Seat that led the current round (1-based).</summary>
        public int RoundLeaderSeat { get; set; }
        /// <summary>Seat that last played cards (1-based).</summary>
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

            if (player.Seat < 1 || player.Seat > Seats.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(player.Seat), "Seat number is out of range.");
            }

            if (!string.IsNullOrEmpty(Seats[player.Seat - 1]))
            {
                 throw new InvalidOperationException($"Seat {player.Seat} is already occupied.");
            }

            Players.Add(player.UserID, player);
            Seats[player.Seat - 1] = player.UserID;
        }

        /// <summary>
        /// Deals a shuffled deck evenly across occupied seats in seating order.
        /// </summary>
        public void DealCards()
        {
            if (Phase != "Lobby")
            {
                throw new InvalidOperationException("Cannot deal cards outside of Lobby phase.");
            }

            if (Players.Count < 2)
            {
                throw new InvalidOperationException("Need at least 2 players to start.");
            }

            var deck = new Deck();
            var allCards = new Queue<Card>(deck.DrawAll());
            
            // Clear existing hands
            foreach (var player in Players.Values)
            {
                // player.Hand = new Hand();
                player.HasPassed = false;
                player.Finished = false;
            }

            CurrentBoard.Clear();
            FinishOrder.Clear();

            // Distribute 13 cards to each player (or max possible)
            // Standard Tien Len is 13 cards each.
            int cardsPerPlayer = 13; 
            
            // Simple round-robin deal based on seat order
            for (int i = 0; i < cardsPerPlayer; i++)
            {
                for (int s = 0; s < Seats.Length; s++)
                {
                    var userId = Seats[s];
                    if (!string.IsNullOrEmpty(userId) && allCards.Count > 0)
                    {
                        Players[userId].Hand.AddCards(new[] { allCards.Dequeue() });
                    }
                }
            }

            Phase = "Playing";
            
            // Simple rule: Owner starts, or logic to find 3 of Spades could go here.
            // For now, let's set it to the first occupied seat.
            for (int s = 0; s < Seats.Length; s++)
            {
                if (!string.IsNullOrEmpty(Seats[s]))
                {
                    CurrentTurnSeat = s + 1;
                    RoundLeaderSeat = CurrentTurnSeat;
                    break;
                }
            }
        }

        /// <summary>
        /// Plays a turn for the specified player.
        /// </summary>
        /// <param name="userId">User identifier.</param>
        /// <param name="cards">Cards to play.</param>
        public void PlayTurn(string userId, List<Card> cards)
        {
            if (Phase != "Playing") throw new InvalidOperationException("Match is not in Playing phase.");
            if (!Players.TryGetValue(userId, out var player)) throw new ArgumentException("Player not found.");
            
            // Turn validation
            if (player.Seat != CurrentTurnSeat) throw new InvalidOperationException("Not this player's turn.");
            
            // Hand validation
            if (!player.Hand.HasCards(cards)) throw new InvalidOperationException("Player does not have these cards.");

            // Basic Rule: If board is not empty, new play must be valid against it. 
            // (Skipping complex rule validation for this refactor step, assuming client/server validation happens elsewhere or later)

            // Execute Play
            player.Hand.RemoveCards(cards);
            CurrentBoard = new List<Card>(cards); // Replace board (Tien Len overwrites, doesn't stack usually)
            LastPlaySeat = player.Seat;

            // Check Win
            if (player.Hand.Cards.Count == 0)
            {
                player.Finished = true;
                FinishOrder.Add(userId);
                // If everyone finished, end game? (Left as exercise)
            }

            AdvanceTurn();
        }

        /// <summary>
        /// Skips the turn for the specified player.
        /// </summary>
        /// <param name="userId">User identifier.</param>
        public void SkipTurn(string userId)
        {
             if (Phase != "Playing") throw new InvalidOperationException("Match is not in Playing phase.");
             if (!Players.TryGetValue(userId, out var player)) throw new ArgumentException("Player not found.");
             if (player.Seat != CurrentTurnSeat) throw new InvalidOperationException("Not this player's turn.");

             if (CurrentBoard.Count == 0) throw new InvalidOperationException("Cannot skip when you are leading the round.");

             player.HasPassed = true;
             AdvanceTurn();
        }

        private void AdvanceTurn()
        {
            int checkSeat = CurrentTurnSeat;
            int seatsChecked = 0;

            while (seatsChecked < Seats.Length)
            {
                checkSeat = (checkSeat % Seats.Length) + 1; // 1-based next seat
                seatsChecked++;

                // If we wrapped back to the LastPlaySeat (everyone else passed)
                // OR if the LastPlaySeat player finished, and we wrapped back to them, logic gets tricky.
                // Simplified: If we found the person who played last, they win the round.
                if (checkSeat == LastPlaySeat && CurrentBoard.Count > 0)
                {
                    // Round End: Winner of round starts new round
                    StartNewRound(checkSeat);
                    return;
                }

                var userId = Seats[checkSeat - 1];
                if (string.IsNullOrEmpty(userId)) continue; // Empty seat

                var player = Players[userId];
                
                // If player is still in game and hasn't passed (or it's a new round so passes don't count)
                if (!player.Finished && !player.HasPassed)
                {
                    CurrentTurnSeat = checkSeat;
                    return;
                }
            }

            // If we get here, it might mean everyone finished or edge case with LastPlaySeat finishing.
            // If LastPlaySeat finished, we need to pass lead to next active player.
            if (CurrentBoard.Count > 0)
            {
                 // The person who last played has finished. 
                 // We need to find the next person after them to start the new round.
                 StartNewRound(LastPlaySeat);
            }
        }

        private void StartNewRound(int winnerSeat)
        {
            CurrentBoard.Clear();
            
            // Reset passes for all active players
            foreach (var p in Players.Values)
            {
                p.HasPassed = false;
            }

            // If the winner of the round has finished, the lead passes to the next person
            int nextLeader = winnerSeat;
            
            // Check if winnerSeat is valid/active. If finished, find next active.
            var winnerId = Seats[winnerSeat - 1];
            if (!string.IsNullOrEmpty(winnerId) && Players[winnerId].Finished)
            {
                 int seatsChecked = 0;
                 while(seatsChecked < Seats.Length)
                 {
                     nextLeader = (nextLeader % Seats.Length) + 1;
                     seatsChecked++;
                     var uid = Seats[nextLeader - 1];
                     if(!string.IsNullOrEmpty(uid) && !Players[uid].Finished)
                     {
                         break;
                     }
                 }
            }

            CurrentTurnSeat = nextLeader;
            RoundLeaderSeat = nextLeader;
            LastPlaySeat = 0; // Reset last play since it's a fresh round
        }
    }
}
