using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TienLen.Domain.ValueObjects; // Still needed for Card

namespace TienLen.Application
{
    /// <summary>
    /// Interface for networking operations related to a game match.
    /// Acts as an output port for the Application layer to send commands,
    /// and an input port for Infrastructure to notify the Application of incoming data.
    /// </summary>
    public interface IMatchNetworkClient
    {
        // --- Events for incoming match data from the network ---

        /// <summary>
        /// Fired when cards are played by a player in the match.
        /// </summary>
        event Action<int, List<Card>, int, bool> OnCardsPlayed; // seat, cards, nextTurnSeat, newRound

        /// <summary>
        /// Fired when a player skips their turn.
        /// </summary>
        event Action<int, int, bool> OnTurnPassed; // seat, nextTurnSeat, newRound

        /// <summary>
        /// Fired when the game starts within the match (e.g., initial deal).
        /// </summary>
        event Action<List<Card>, int> OnGameStarted; // hand, firstTurnSeat

        /// <summary>
        /// Fired when a match state snapshot is received from the server (OP_CODE_PLAYER_JOINED).
        /// </summary>
        event Action<MatchStateSnapshotDto> OnPlayerJoinedOP;
        
        /// <summary>
        /// Fired when the game ends.
        /// </summary>
        event Action<List<int>> OnGameEnded; // finishOrderSeats

        /// <summary>
        /// Fired when a game error occurs.
        /// </summary>
        event Action<int, string> OnGameError; // code, message


        // --- Methods for sending match actions to the network ---
        /// <summary>
        /// Sends a request to join a specific match.
        /// </summary>
        UniTask SendJoinMatchAsync(string matchId);

        /// <summary>
        /// Sends a request to start the game in the current match.
        /// </summary>
        UniTask SendStartGameAsync();

        /// <summary>
        /// Sends a request to play cards in the current turn.
        /// </summary>
        UniTask SendPlayCardsAsync(List<Card> cards);

        /// <summary>
        /// Sends a request to skip the current turn.
        /// </summary>
        UniTask SendPassTurnAsync();

        /// <summary>
        /// Sends a request to ask for a new game.
        /// </summary>
        UniTask SendRequestNewGameAsync();

        /// <summary>
        /// Initiates matchmaking and returns the found match ID upon success.
        /// </summary>
        UniTask<string> FindMatchAsync();
    }
}
