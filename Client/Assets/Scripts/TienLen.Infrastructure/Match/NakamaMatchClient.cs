using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nakama;
using TienLen.Domain.Services;
using TienLen.Domain.ValueObjects;
using TienLen.Infrastructure.Services;
using UnityEngine;
using Google.Protobuf;
using Proto = Tienlen.V1;

namespace TienLen.Infrastructure.Match
{
    /// <summary>
    /// Nakama implementation of IMatchNetworkClient.
    /// </summary>
    public sealed class NakamaMatchClient : IMatchNetworkClient
    {
        private readonly NakamaAuthenticationService _authService;
        private string _matchId;

        // --- IMatchNetworkClient Events ---
        public event Action<string> OnPlayerJoined;
        public event Action<string, List<Card>> OnCardsPlayed;
        public event Action<string> OnPlayerSkippedTurn;
        public event Action OnGameStarted;
        public event Action<string> OnPlayerFinished;

        public NakamaMatchClient(NakamaAuthenticationService authService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        private ISocket Socket => _authService.Socket;
        private IClient Client => _authService.Client;

        // --- IMatchNetworkClient Implementation ---

        public async UniTask<string> FindMatchAsync()
        {
            if (Socket == null) throw new InvalidOperationException("Not connected to Nakama socket.");
            if (!Socket.IsConnected) throw new InvalidOperationException("Nakama socket is not connected.");

            // 1. Call server-side RPC to find or create a match
            var rpcId = "find_match";
            var rpcResponse = await Client.RpcAsync(_authService.Session, rpcId);

            if (rpcResponse == null || string.IsNullOrEmpty(rpcResponse.Payload))
            {
                throw new InvalidOperationException($"RPC '{rpcId}' returned no match ID.");
            }

            var matchId = rpcResponse.Payload;
            
            // 2. Join the match using the ID returned from the RPC
            await SendJoinMatchAsync(matchId);
            
            return matchId;
        }

        public async UniTask SendJoinMatchAsync(string matchId)
        {
            if (Socket == null) throw new InvalidOperationException("Not connected to Nakama socket.");
            if (!Socket.IsConnected) throw new InvalidOperationException("Nakama socket is not connected.");

            // Join the match on Nakama
            var match = await Socket.JoinMatchAsync(matchId);
            _matchId = match.Id;
            
            // Subscribe to events specific to this match
            Socket.ReceivedMatchState += HandleMatchState;
            Socket.ReceivedMatchPresence += HandleMatchPresence;

            Debug.Log($"MatchClient: Joined match: {_matchId}");
        }

        public async UniTask SendStartGameAsync()
        {
            var request = new Proto.StartGameRequest();
            await SendAsync((long)Proto.OpCode.StartGame, request.ToByteArray());
            Debug.Log("MatchClient: Sent StartGameRequest.");
        }

        public UniTask SendPlayCardsAsync(List<Card> cards)
        {
            // return SendAsync(TienLenOpcodes.PlayCards, ProtoMatchCodec.EncodePlayCards(cards));
            throw new NotImplementedException("ProtoMatchCodec is removed.");
        }

        public UniTask SendPassTurnAsync()
        {
             // return SendAsync(TienLenOpcodes.PassTurn, ProtoMatchCodec.EncodePassTurn());
             throw new NotImplementedException("ProtoMatchCodec is removed.");
        }

        public UniTask SendRequestNewGameAsync()
        {
            // return SendAsync(TienLenOpcodes.RequestNewGame, ProtoMatchCodec.EncodeRequestNewGame());
            throw new NotImplementedException("ProtoMatchCodec is removed.");
        }

        // --- Event Handlers ---

        private void HandleMatchPresence(IMatchPresenceEvent presenceEvent)
        {
            if (presenceEvent.MatchId != _matchId) return;

            foreach (var joiner in presenceEvent.Joins)
            {
                OnPlayerJoined?.Invoke(joiner.UserId);
            }
        }

        private void HandleMatchState(IMatchState state)
        {
            if (state.MatchId != _matchId) return;

            switch (state.OpCode)
            {
                case (long)Proto.OpCode.GameStarted:
                    try 
                    {
                        var payload = Proto.GameStartedEvent.Parser.ParseFrom(state.State);
                        Debug.Log($"MatchClient: Game Started! Phase: {payload.Phase}, First Turn: {payload.FirstTurnUserId}");
                        OnGameStarted?.Invoke();
                    } 
                    catch (Exception e) 
                    {
                        Debug.LogError($"Error parsing GameStartedEvent: {e}");
                    }
                    break;
            }
        }

        private async UniTask SendAsync(long opcode, byte[] payload)
        {
            if (Socket == null || !Socket.IsConnected) return;
            await Socket.SendMatchStateAsync(_matchId, opcode, payload);
        }
    }
}
