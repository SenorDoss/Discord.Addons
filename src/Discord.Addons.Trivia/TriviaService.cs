﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Addons.Core;

namespace Discord.Addons.TriviaGames
{
    /// <summary> Manages Trivia games. </summary>
    public sealed class TriviaService
    {
        public IReadOnlyDictionary<string, string[]> TriviaData { get; }

        internal IReadOnlyDictionary<ulong, TriviaGame> TriviaGames => _triviaGames;

        private readonly ConcurrentDictionary<ulong, TriviaGame> _triviaGames =
            new ConcurrentDictionary<ulong, TriviaGame>();
        private readonly Func<LogMessage, Task> _logger;

        private TriviaService(
            IReadOnlyDictionary<string, string[]> triviaData,
            Func<LogMessage, Task> logger = null)
        {
            _logger = logger ?? Extensions.NoOpLogger;
            Log(LogSeverity.Info, "Creating Trivia service.");

            TriviaData = triviaData ?? throw new ArgumentNullException(nameof(triviaData));
        }

        internal TriviaService(
            IReadOnlyDictionary<string, string[]> triviaData,
            DiscordSocketClient client,
            Func<LogMessage, Task> logger = null) : this(triviaData, logger)
        {
            client.MessageReceived += CheckMessage;
        }

        internal TriviaService(
            IReadOnlyDictionary<string, string[]> triviaData,
            DiscordShardedClient client,
            Func<LogMessage, Task> logger = null) : this(triviaData, logger)
        {
            client.MessageReceived += CheckMessage;
        }

        private Task CheckMessage(SocketMessage msg)
        {
            return (_triviaGames.TryGetValue(msg.Channel.Id, out var game))
                ? game.CheckTrivia(msg)
                : Task.CompletedTask;
        }

        internal Task Log(LogSeverity severity, string msg)
        {
            return _logger(new LogMessage(severity, "Trivia", msg));
        }

        /// <summary> Add a new game to the list of active games. </summary>
        /// <param name="channelId">Public facing channel of this game.</param>
        /// <param name="game">Instance of the game.</param>
        public bool AddNewGame(IMessageChannel channel, TriviaGame game)
        {
            bool r = _triviaGames.TryAdd(channel.Id, game);
            if (r)
                game.GameEnd += _onGameEnd;

            return r;
        }

        private Task _onGameEnd(IMessageChannel channel)
        {
            if (_triviaGames.TryRemove(channel.Id, out var game))
            {
                game.GameEnd -= _onGameEnd;
            }
            return Task.CompletedTask;
        }
    }

    public static class TriviaExtensions
    {
        /// <summary> Add a service for trivia games to the <see cref="CommandService"/>
        /// using the <see cref="DiscordSocketClient"/>. </summary>
        /// <typeparam name="TTrivia">Type of the Trivia Module.</typeparam>
        /// <param name="client">The <see cref="DiscordSocketClient"/> instance.</param>
        /// <param name="map">The <see cref="IDependencyMap"/> instance.</param>
        /// <param name="triviaData">A set of questions and answers to use as trivia.</param>
        /// <param name="logger">Optional: A method that handles logging.</param>
        public static Task AddTrivia<TTrivia>(
            this CommandService cmdService,
            DiscordSocketClient client,
            IServiceCollection map,
            IReadOnlyDictionary<string, string[]> triviaData,
            Func<LogMessage, Task> logger = null)
            where TTrivia : TriviaModuleBase
        {
            map.AddSingleton(new TriviaService(triviaData, client, logger ?? (m => Task.CompletedTask)));
            return cmdService.AddModuleAsync<TTrivia>();
        }

        /// <summary> Add a service for trivia games to the <see cref="CommandService"/>
        /// using the <see cref="DiscordShardedClient"/>. </summary>
        /// <typeparam name="TTrivia">Type of the Trivia Module.</typeparam>
        /// <param name="client">The <see cref="DiscordShardedClient"/> instance.</param>
        /// <param name="map">The <see cref="IDependencyMap"/> instance.</param>
        /// <param name="triviaData">A set of questions and answers to use as trivia.</param>
        /// <param name="logger">Optional: A method that handles logging.</param>
        public static Task AddTrivia<TTrivia>(
            this CommandService cmdService,
            DiscordShardedClient client,
            IServiceCollection map,
            IReadOnlyDictionary<string, string[]> triviaData,
            Func<LogMessage, Task> logger = null)
            where TTrivia : TriviaModuleBase
        {
            map.AddSingleton(new TriviaService(triviaData, client, logger ?? (m => Task.CompletedTask)));
            return cmdService.AddModuleAsync<TTrivia>();
        }
    }
}
