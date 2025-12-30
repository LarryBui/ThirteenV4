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
        event Action<int, List<Card>, int, bool, long> OnCardsPlayed; // seat, cards, nextTurnSeat, newRound, turnSecondsRemaining

        /// <summary>
        /// Fired when a player skips their turn.
        /// </summary>
        event Action<int, int, bool, long> OnTurnPassed; // seat, nextTurnSeat, newRound, turnSecondsRemaining

        /// <summary>
        /// Fired when the game starts within the match (e.g., initial deal).
        /// </summary>
        event Action<List<Card>, int, long> OnGameStarted; // hand, firstTurnSeat, turnSecondsRemaining

        /// <summary>
        /// Fired when a match state snapshot is received from the server (OP_CODE_PLAYER_JOINED).
        /// </summary>
        event Action<MatchStateSnapshotDto> OnPlayerJoinedOP;

        /// <summary>
        /// Fired when a player leaves the match (OP_CODE_PLAYER_LEFT).
        /// </summary>
        event Action<int, string> OnPlayerLeft; // seat, userId

        /// <summary>
        /// Fired when match presence changes (join/leave) with username info when available.
        /// </summary>
        event Action<IReadOnlyList<PresenceChange>> OnMatchPresenceChanged;
        
        /// <summary>
        /// Fired when the game ends with a full result payload.
        /// </summary>
        event Action<GameEndedResultDto> OnGameEnded;

        /// <summary>
        /// Fired when a player finishes their hand.
        /// </summary>
        event Action<int, int> OnPlayerFinished; // seat, rank

        /// <summary>
        /// Fired when a game error occurs.
        /// </summary>
        event Action<int, string> OnGameError; // code, message

        /// <summary>
        /// Fired when an in-game chat message is received.
        /// </summary>
        event Action<int, string> OnInGameChatReceived; // seatIndex, message


        // --- Methods for sending match actions to the network ---
        /// <summary>
        /// Sends a request to join a specific match.
        /// </summary>
        UniTask SendJoinMatchAsync(string matchId);

        /// <summary>
        /// Sends a request to leave the current match.
        /// </summary>
        UniTask SendLeaveMatchAsync();

        /// <summary>
        /// Sends a request to start the game in the current match.
        /// </summary>
        UniTask SendStartGameAsync();

        /// <summary>
        /// Sends a request to start a rigged game using a predefined deck.
        /// </summary>
        /// <param name="request">Rigged deck payload.</param>
        UniTask SendStartGameTestAsync(RiggedDeckRequestDto request);

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
        /// Sends an in-game chat message.
        /// </summary>
        UniTask SendInGameChatAsync(string message);

        /// <summary>
        /// Initiates matchmaking and returns the found match ID upon success.
        /// </summary>
        UniTask<string> FindMatchAsync();
    }

    /// <summary>
    /// Represents a single user presence change in a match.
    /// </summary>
    public sealed class PresenceChange
    {
        /// <summary>
        /// User id for the presence.
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// Username for the presence, when supplied by the server.
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// True when the presence joined; false when it left.
        /// </summary>
        public bool Joined { get; }

        public PresenceChange(string userId, string username, bool joined)
        {
            UserId = userId;
            Username = username;
            Joined = joined;
        }
    }
}
