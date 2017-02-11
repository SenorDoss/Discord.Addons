﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace Discord.Addons.SimplePermissions
{
    /// <summary> </summary>
    public sealed class PermissionsService
    {
        private readonly DiscordSocketClient _sockClient;
        private readonly DiscordShardedClient _shardClient;
        private readonly Func<LogMessage, Task> _logger;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        internal readonly ConcurrentDictionary<ulong, FancyHelpMessage> Helpmsgs = new ConcurrentDictionary<ulong, FancyHelpMessage>();
        internal readonly CommandService CService;
        internal readonly IConfigStore<IPermissionConfig> ConfigStore;
        internal readonly IDependencyMap Map;

        /// <summary> </summary>
        /// <param name="configstore"></param>
        /// <param name="commands"></param>
        /// <param name="client"></param>
        internal PermissionsService(
            IConfigStore<IPermissionConfig> configstore,
            CommandService commands,
            DiscordSocketClient client,
            IDependencyMap map,
            Func<LogMessage, Task> logAction)
        {
            ConfigStore = configstore ?? throw new ArgumentNullException(nameof(configstore));
            CService = commands ?? throw new ArgumentNullException(nameof(commands));
            _sockClient = client ?? throw new ArgumentNullException(nameof(client));

            _logger = logAction;
            Map = map;

            client.Connected += checkDuplicateModuleNames;

            client.GuildAvailable += async guild =>
            {
                using (var config = ConfigStore.Load())
                {
                    await config.AddNewGuild(guild);
                    config.Save();
                }
            };
            client.UserJoined += async user =>
            {
                using (var config = ConfigStore.Load())
                {
                    await config.AddUser(user);
                    config.Save();
                }
            };
            client.ChannelCreated += async chan =>
            {
                using (var config = ConfigStore.Load())
                {
                    await config.AddChannel(chan);
                    config.Save();
                }
            };
            client.ChannelDestroyed += async chan =>
            {
                using (var config = ConfigStore.Load())
                {
                    await config.RemoveChannel(chan);
                    config.Save();
                }
            };

            client.ReactionAdded += OnReactionAdded;
            client.MessageDeleted += (id, msg) => Task.FromResult(Helpmsgs.TryRemove(id, out var _));

            Log(LogSeverity.Info, "Created Permission service.");
        }

        internal PermissionsService(
            IConfigStore<IPermissionConfig> configstore,
            CommandService commands,
            DiscordShardedClient client,
            IDependencyMap map,
            Func<LogMessage, Task> logAction)
        {
            ConfigStore = configstore ?? throw new ArgumentNullException(nameof(configstore));
            CService = commands ?? throw new ArgumentNullException(nameof(commands));
            _shardClient = client ?? throw new ArgumentNullException(nameof(client));

            _logger = logAction;
            Map = map;

            client.GetShard(0).Connected += checkDuplicateModuleNames;

            client.GuildAvailable += async guild =>
            {
                using (var config = ConfigStore.Load())
                {
                    await config.AddNewGuild(guild);
                    config.Save();
                }
            };
            client.UserJoined += async user =>
            {
                using (var config = ConfigStore.Load())
                {
                    await config.AddUser(user);
                    config.Save();
                }
            };
            client.ChannelCreated += async chan =>
            {
                using (var config = ConfigStore.Load())
                {
                    await config.AddChannel(chan);
                    config.Save();
                }
            };
            client.ChannelDestroyed += async chan =>
            {
                using (var config = ConfigStore.Load())
                {
                    await config.RemoveChannel(chan);
                    config.Save();
                }
            };

            client.ReactionAdded += OnReactionAdded;
            client.MessageDeleted += (id, msg) => Task.FromResult(Helpmsgs.TryRemove(id, out var _));

            Log(LogSeverity.Info, "Created Permission service.");
        }

        private async Task OnReactionAdded(ulong id, Optional<SocketUserMessage> message, SocketReaction reaction)
        {
            var msg = message.GetValueOrDefault();
            if (msg == null)
            {
                await Log(LogSeverity.Debug, $"Message with id {id} was not in cache.");
                return;
            }
            if (!reaction.User.IsSpecified)
            {
                await Log(LogSeverity.Debug, $"Message with id {id} had invalid user.");
                return;
            }
            if (Helpmsgs.TryGetValue(msg.Id, out var fhm))
            {
                if (reaction.UserId == _sockClient?.CurrentUser.Id
                    || reaction.UserId == _shardClient.CurrentUser.Id) return;

                if (reaction.UserId != fhm.UserId)
                {
                    var _ = msg.RemoveReactionAsync(reaction.Emoji.Name, reaction.User.Value);
                    return;
                }

                switch (reaction.Emoji.Name)
                {
                    case FancyHelpMessage.EFirst:
                        await fhm.First();
                        break;
                    case FancyHelpMessage.EBack:
                        await fhm.Back();
                        break;
                    case FancyHelpMessage.ENext:
                        await fhm.Next();
                        break;
                    case FancyHelpMessage.ELast:
                        await fhm.Last();
                        break;
                    case FancyHelpMessage.EDelete:
                        await fhm.Delete();
                        break;
                    default:
                        break;
                }
            }
        }

        private async Task checkDuplicateModuleNames()
        {
            var modnames = CService.Modules.Select(m => m.Name).ToList();
            var multiples = modnames.Where(name => modnames.Count(str => str.Equals(name, StringComparison.OrdinalIgnoreCase)) > 1);

            if (multiples.Any())
            {
                await Log(LogSeverity.Error, "Multiple modules with the same Name have been registered, SimplePermissions cannot function.");
                throw new Exception(
$@"Multiple modules with the same Name have been registered, SimplePermissions cannot function.
Duplicate names: {String.Join(", ", multiples.Distinct())}.");
            }

            if (_sockClient != null)
                _sockClient.Connected -= checkDuplicateModuleNames;

            if (_shardClient != null)
                _shardClient.GetShard(0).Connected -= checkDuplicateModuleNames;
        }

        internal Task Log(LogSeverity severity, string msg)
        {
            return _logger(new LogMessage(severity, "SimplePermissions", msg));
        }

        internal Task AddNewFancy(FancyHelpMessage fhm)
        {
            Helpmsgs.TryAdd(fhm.MsgId, fhm);
            return Task.CompletedTask;
        }

        //private void RemovePermissionsModule(IMessageChannel channel)
        //{
        //    ConfigStore.Load().BlacklistModule(channel, PermissionsModule.PermModuleName);
        //    ConfigStore.Save();
        //    Console.WriteLine($"{DateTime.Now}: Removed permission management from {channel.Name}.");
        //}

        //private void AddPermissionsModule(IMessageChannel channel)
        //{
        //    ConfigStore.Load().WhitelistModule(channel, PermissionsModule.PermModuleName);
        //    ConfigStore.Save();
        //    Console.WriteLine($"{DateTime.Now}: Added permission management to {channel.Name}.");
        //}

        internal async Task<bool> SetGuildAdminRole(IGuild guild, IRole role)
        {
            using (var config = ConfigStore.Load())
            {
                await _lock.WaitAsync();
                var result = await config.SetGuildAdminRole(guild, role);
                config.Save();
                _lock.Release();
                return result;
            }
        }

        internal async Task<bool> SetGuildModRole(IGuild guild, IRole role)
        {
            using (var config = ConfigStore.Load())
            {
                await _lock.WaitAsync();
                var result = await config.SetGuildModRole(guild, role);
                config.Save();
                _lock.Release();
                return result;
            }
        }

        internal async Task<bool> AddSpecialUser(IChannel channel, IGuildUser user)
        {
            using (var config = ConfigStore.Load())
            {
                await _lock.WaitAsync();
                var result = await config.AddSpecialUser(channel, user);
                config.Save();
                _lock.Release();
                return result;
            }
        }

        internal async Task<bool> RemoveSpecialUser(IChannel channel, IGuildUser user)
        {
            using (var config = ConfigStore.Load())
            {
                await _lock.WaitAsync();
                var result = await config.RemoveSpecialUser(channel, user);
                config.Save();
                _lock.Release();
                return result;
            }
        }

        internal async Task<bool> WhitelistModule(IChannel channel, string modName)
        {
            using (var config = ConfigStore.Load())
            {
                await _lock.WaitAsync();
                var result = await config.WhitelistModule(channel, modName);
                config.Save();
                _lock.Release();
                return result;
            }
        }

        internal async Task<bool> BlacklistModule(IChannel channel, string modName)
        {
            using (var config = ConfigStore.Load())
            {
                await _lock.WaitAsync();
                var result = await config.BlacklistModule(channel, modName);
                config.Save();
                _lock.Release();
                return result;
            }
        }

        internal async Task<bool> WhitelistModuleGuild(IGuild guild, string modName)
        {
            using (var config = ConfigStore.Load())
            {
                await _lock.WaitAsync();
                var result = await config.WhitelistModuleGuild(guild, modName);
                config.Save();
                _lock.Release();
                return result;
            }
        }

        internal async Task<bool> BlacklistModuleGuild(IGuild guild, string modName)
        {
            using (var config = ConfigStore.Load())
            {
                await _lock.WaitAsync();
                var result = await config.BlacklistModuleGuild(guild, modName);
                config.Save();
                _lock.Release();
                return result;
            }
        }

        internal static int GetMessageCacheSize(DiscordSocketClient client)
        {
            var p = typeof(DiscordSocketClient).GetProperty("MessageCacheSize", BindingFlags.Instance | BindingFlags.NonPublic);
            return (int)p.GetMethod.Invoke(client, Array.Empty<object>());
        }
    }

    public static class PermissionsExtensions
    {
        /// <summary> Add SimplePermissions to your <see cref="CommandService"/> using a <see cref="DiscordSocketClient"/>. </summary>
        /// <param name="client">The <see cref="DiscordSocketClient"/> instance.</param>
        /// <param name="configStore">The <see cref="IConfigStore{TConfig}"/> instance.</param>
        /// <param name="map">The <see cref="IDependencyMap"/> instance.</param>
        /// <param name="logAction">Optional: A delegate or method that will log messages.</param>
        public static Task UseSimplePermissions(
            this CommandService cmdService,
            DiscordSocketClient client,
            IConfigStore<IPermissionConfig> configStore,
            IDependencyMap map,
            Func<LogMessage, Task> logAction = null)
        {
            map.Add(new PermissionsService(configStore, cmdService, client, map, logAction ?? (msg => Task.CompletedTask)));
            return cmdService.AddModuleAsync<PermissionsModule>();
        }

        ///// <summary> Add SimplePermissions to your <see cref="CommandService"/> using a <see cref="DiscordShardedClient"/>. </summary>
        ///// <param name="client">The <see cref="DiscordShardedClient"/> instance.</param>
        ///// <param name="configStore">The <see cref="IConfigStore{TConfig}"/> instance.</param>
        ///// <param name="map">The <see cref="IDependencyMap"/> instance.</param>
        ///// <param name="logAction">Optional: A delegate or method that will log messages.</param>
        //public static Task UseSimplePermissions(
        //    this CommandService cmdService,
        //    DiscordShardedClient client,
        //    IConfigStore<IPermissionConfig> configStore,
        //    IDependencyMap map,
        //    Func<LogMessage, Task> logAction = null)
        //{
        //    map.Add(new PermissionsService(configStore, cmdService, client, map, logAction ?? (msg => Task.CompletedTask)));
        //    return cmdService.AddModuleAsync<ShardedPermissionsModule>();
        //}

        internal static bool HasPerms(this IGuildUser user, IGuildChannel channel, DiscordPermissions perms)
        {
            var clientPerms = (DiscordPermissions)user.GetPermissions(channel).RawValue;
            return (clientPerms & perms) == perms;
        }

        [Flags]
        internal enum DiscordPermissions : ulong
        {
            CREATE_INSTANT_INVITE = 0x00000001,
            KICK_MEMBERS = 0x00000002,
            BAN_MEMBERS = 0x00000004,
            ADMINISTRATOR = 0x00000008,
            MANAGE_CHANNELS = 0x00000010,
            MANAGE_GUILD = 0x00000020,
            ADD_REACTIONS = 0x00000040,
            READ_MESSAGES = 0x00000400,
            SEND_MESSAGES = 0x00000800,
            SEND_TTS_MESSAGES = 0x00001000,
            MANAGE_MESSAGES = 0x00002000,
            EMBED_LINKS = 0x00004000,
            ATTACH_FILES = 0x00008000,
            READ_MESSAGE_HISTORY = 0x00010000,
            MENTION_EVERYONE = 0x00020000,
            USE_EXTERNAL_EMOJIS = 0x00040000,
            CONNECT = 0x00100000,
            SPEAK = 0x00200000,
            MUTE_MEMBERS = 0x00400000,
            DEAFEN_MEMBERS = 0x00800000,
            MOVE_MEMBERS = 0x01000000,
            USE_VAD = 0x02000000,
            CHANGE_NICKNAME = 0x04000000,
            MANAGE_NICKNAMES = 0x08000000,
            MANAGE_ROLES = 0x10000000,
            MANAGE_WEBHOOKS = 0x20000000,
            MANAGE_EMOJIS = 0x40000000,
        }
    }
}
