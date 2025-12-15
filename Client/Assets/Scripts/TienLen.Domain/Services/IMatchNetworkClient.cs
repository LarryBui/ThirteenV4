using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TienLen.Domain.ValueObjects;

namespace TienLen.Domain.Services
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
        /// Fired when a player successfully joins the match.
        /// </summary>
        event Action<string> OnPlayerJoined;

        /// <summary>
        /// Fired when cards are played by a player in the match.
        /// </summary>
        event Action<string, List<Card>> OnCardsPlayed; // userId, cards

        /// <summary>
        /// Fired when a player skips their turn.
        /// </summary>
        event Action<string> OnPlayerSkippedTurn; // userId

        /// <summary>
        /// Fired when the match starts (e.g., initial deal).
        /// </summary>
        event Action OnMatchStarted;
        
        /// <summary>
        /// Fired when a player finishes their hand.
        /// </summary>
        event Action<string> OnPlayerFinished; // userId


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
    }
}
