using System;
using System.Collections.Generic;
using TienLen.Application;
using TienLen.Domain.Enums;
using UnityEngine;

namespace TienLen.Presentation.GameRoomScreen.DebugTools
{
    /// <summary>
    /// ScriptableObject preset used to define rigged deck hands in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "TienLen/Rigged Deck Preset", fileName = "RiggedDeckPreset")]
    public sealed class RiggedDeckPreset : ScriptableObject
    {
        [SerializeField] private List<RiggedDeckSeat> _seats = new List<RiggedDeckSeat>();

        /// <summary>Seat configurations included in the preset.</summary>
        public IReadOnlyList<RiggedDeckSeat> Seats => _seats ?? new List<RiggedDeckSeat>();

        /// <summary>
        /// Builds a rigged deck request payload and validates it.
        /// </summary>
        /// <param name="matchId">Match identifier.</param>
        /// <param name="request">Constructed rigged deck request.</param>
        /// <param name="error">Validation error when invalid.</param>
        /// <returns>True when the request is valid.</returns>
        public bool TryBuildRequest(string matchId, out RiggedDeckRequestDto request, out string error)
        {
            var hands = new List<RiggedHandDto>();
            var seats = _seats ?? new List<RiggedDeckSeat>();

            foreach (var seat in seats)
            {
                if (seat == null)
                {
                    continue;
                }

                var cards = new List<RiggedCardDto>();
                if (seat.Cards != null)
                {
                    foreach (var card in seat.Cards)
                    {
                        cards.Add(new RiggedCardDto((int)card.Rank, (int)card.Suit));
                    }
                }

                hands.Add(new RiggedHandDto(seat.Seat, cards));
            }

            request = new RiggedDeckRequestDto(matchId, hands);
            if (!RiggedDeckValidator.TryValidate(request, out error))
            {
                request = null;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Seat-specific card configuration for a rigged deck preset.
    /// </summary>
    [Serializable]
    public sealed class RiggedDeckSeat
    {
        [SerializeField] private int _seat;
        [SerializeField] private List<RiggedDeckCard> _cards = new List<RiggedDeckCard>();

        /// <summary>Seat index (0-based).</summary>
        public int Seat => _seat;
        /// <summary>Cards assigned to the seat.</summary>
        public IReadOnlyList<RiggedDeckCard> Cards => _cards ?? new List<RiggedDeckCard>();
    }

    /// <summary>
    /// Serializable card entry for rigged deck presets.
    /// </summary>
    [Serializable]
    public struct RiggedDeckCard
    {
        /// <summary>Rank of the card.</summary>
        public Rank Rank;
        /// <summary>Suit of the card.</summary>
        public Suit Suit;
    }
}
