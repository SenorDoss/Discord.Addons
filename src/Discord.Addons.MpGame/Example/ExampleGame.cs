﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.MpGame;
using Discord.WebSocket;

namespace Example
{
    public sealed class ExampleGame : GameBase<Player>
    {
        //Example way to keep track of the game state
        private int _turn = 0;
        private GameState _state = GameState.Setup;

        //The base constructor will automatically sub a handler to DiscordSocketClient.MessageReceived
        public ExampleGame(IMessageChannel channel, IEnumerable<Player> players, DiscordSocketClient client)
            : base(channel, players, client)
        {
        }

        //Gets called if the MessageReceived handler found it received a PM from a player
        protected override async Task OnDmMessage(IMessage msg)
        {
            if (msg.Author.Id == TurnPlayer.Value.User.Id && _state == GameState.MainPhase)
            {
                await msg.Channel.SendMessageAsync("PM received.");
            }
        }

        //Gets called if the MessageReceived handler found it received a message in the public channel
        protected override async Task OnPublicMessage(IMessage msg)
        {
            if (msg.Author.Id == TurnPlayer.Value.User.Id && _state == GameState.SpecialPhase)
            {
                await Channel.SendMessageAsync("Message acknowledged.");
            }
        }

        //Call SetupGame() to do the one-time setup happeining prior to a game (think of: shuffling (a) card deck(s))
        public override async Task SetupGame()
        {
            await Channel.SendMessageAsync("Asserting randomized starting parameters.");
        }

        //Call StartGame() to do the things that start the game off (think of: dealing cards)
        public override async Task StartGame()
        {
            await Channel.SendMessageAsync("Dealing .");
            TurnPlayer = Players.Head;
        }

        //Call NextTurn() to do the things happening with a new turn
        public override async Task NextTurn()
        {
            await Channel.SendMessageAsync("Next turn commencing.");
            TurnPlayer = TurnPlayer.Next;
            _turn++;
            _state = GameState.StartOfTurn;
        }

        //Create a string that represents the current state of the game
        public override string GetGameState()
        {
            var sb = new StringBuilder($"State of the game at turn {_turn}")
                .AppendLine($"The current turn player is **{TurnPlayer.Value.User.Username}**.")
                .AppendLine($"The current phase is **{_state.ToString()}**");

            return sb.ToString();
        }

        //Example way to keep track of the game state
        private enum GameState
        {
            Setup,
            StartOfTurn,
            MainPhase,
            SpecialPhase,
            EndPhase
        }
    }
}
