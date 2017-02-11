﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.Commands;
using CommandParam = Discord.Commands.ParameterInfo;

namespace Discord.Addons.SimplePermissions
{
    [Name(PermModuleName), DontAutoLoad]
    public sealed class PermissionsModule : ModuleBase<ICommandContext>
    {
        public const string PermModuleName = "Permissions";
        private static readonly Regex _dbgRegex = new Regex(@"(?<name>(.*)), Version=(?<version>(.*)), Culture=(?<culture>(.*)), PublicKeyToken=(?<token>(.*))", RegexOptions.Compiled);
        //private static readonly Type hiddenAttr = typeof(HiddenAttribute);

        protected PermissionsService PermService { get; }

        /// <summary> </summary>
        public PermissionsModule(PermissionsService permService)
        {
            PermService = permService ?? throw new ArgumentNullException(nameof(permService));
            //_cmdService = permService.CService;
        }

        /// <summary> Special debug command. </summary>
        [Command("debug"), Permission(MinimumPermission.BotOwner)]
        [Alias("dbg"), Hidden]
        public async Task DebugCmd()
        {
            var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
            var asms = (from xa in AppDomain.CurrentDomain.GetAssemblies()
                        where !xa.IsDynamic
                        let m = _dbgRegex.Match(xa.FullName)
                        where m.Success
                        select new
                        {
                            Name = m.Groups["name"].Value,
                            Version = m.Groups["version"].Value
                        }).Where(s =>
            (!(s.Name.StartsWith("System.") || s.Name.StartsWith("Microsoft.") || s.Name == "mscorlib")));

            var info = new EmbedBuilder()
                .WithAuthor(a => a.WithName("Debug information")
                    .WithIconUrl(app.IconUrl))
                .WithTitle($"{app.Name} - Created: {app.CreatedAt.ToString("d MMM yyyy, HH:mm UTC")}")
                .WithDescription($"{app.Description}\nLoaded (non-System) assemblies:")
                .AddFieldSequence(asms, (fb, asm) => fb.WithIsInline(true)
                    .WithName(asm.Name)
                    .WithValue(asm.Version))
                .WithFooter(fb => fb.WithText($"Up for {(DateTime.Now - Process.GetCurrentProcess().StartTime).ToNiceString()}."))
                .WithCurrentTimestamp()
                .Build();

            await ReplyAsync("", embed: info).ConfigureAwait(false);
        }

        /// <summary> Display commands you can use. </summary>
        [Command("help"), Permission(MinimumPermission.Everyone)]
        [Summary("Display commands you can use.")]
        public async Task HelpCmd()
        {
            var cmds = (await PermService.CService.Commands
                .Where(c => !c.Preconditions.Any(p => p is HiddenAttribute))
                .CheckConditions(Context, PermService.Map).ConfigureAwait(false));

            if (await UseFancy().ConfigureAwait(false))
            {
                var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
                PermService.AddNewFancy(await new FancyHelpMessage(Context.Channel, Context.User, cmds, app).SendMessage().ConfigureAwait(false));
            }
            else
            {
                var grouped = cmds.GroupBy(c => c.Module.Name)
                    .Select(g => $"{g.Key}:\n\t`{String.Join("`, `", g.Select(c => c.Aliases.FirstOrDefault()).Distinct())}`");

                var sb = new StringBuilder("You can use the following commands:\n")
                    .AppendLine($"{String.Join("\n", grouped)}\n")
                    .Append("You can use `help <command>` for more information on that command.");

                await ReplyAsync(sb.ToString()).ConfigureAwait(false);
            }
        }

        private async Task<bool> UseFancy()
        {
            using (var config = PermService.ConfigStore.Load())
            {
                bool fancyEnabled = Context.Guild != null && await config.GetFancyHelpValue(Context.Guild).ConfigureAwait(false);
                return fancyEnabled //&& PermissionsService.GetMessageCacheSize(Context.Client) > 0
                    && (await Context.Guild.GetCurrentUserAsync()).HasPerms(Context.Channel as ITextChannel,
                        PermissionsExtensions.DiscordPermissions.ADD_REACTIONS
                        | PermissionsExtensions.DiscordPermissions.MANAGE_MESSAGES);
            }
        }

        /// <summary> Display how you can use a command. </summary>
        /// <param name="cmdname"></param>
        [Command("help"), Permission(MinimumPermission.Everyone)]
        [Summary("Display how you can use a command.")]
        public async Task HelpCmd(string cmdname)
        {
            var sb = new StringBuilder();
            var cmds = (await PermService.CService.Commands.CheckConditions(Context, PermService.Map).ConfigureAwait(false))
                .Where(c => c.Aliases.FirstOrDefault().Equals(cmdname, StringComparison.OrdinalIgnoreCase)
                    && !c.Preconditions.Any(p => p is HiddenAttribute));

            if (cmds.Any())
            {
                sb.AppendLine($"`{cmds.First().Aliases.FirstOrDefault()}`");
                foreach (var cmd in cmds)
                {
                    sb.AppendLine('\t' + cmd.Summary);
                    if (cmd.Parameters.Count > 0)
                        sb.AppendLine($"\t\tParameters: {String.Join(" ", cmd.Parameters.Select(p => formatParam(p)))}");
                }
            }
            else
            {
                return;
            }

            await ReplyAsync(sb.ToString()).ConfigureAwait(false);
        }

        private string formatParam(CommandParam param)
        {
            var sb = new StringBuilder();
            if (param.IsMultiple)
            {
                sb.Append($"`[({param.Type?.Name}): {param.Name}...]`");
            }
            else if (param.IsRemainder) //&& IsOptional - decided not to check for the combination
            {
                sb.Append($"`<({param.Type?.Name}): {param.Name}...>`");
            }
            else if (param.IsOptional)
            {
                sb.Append($"`[({param.Type?.Name}): {param.Name}]`");
            }
            else
            {
                sb.Append($"`<({param.Type?.Name}): {param.Name}>`");
            }

            if (!String.IsNullOrWhiteSpace(param.Summary))
            {
                sb.Append($" ({param.Summary})");
            }
            return sb.ToString();
        }

        /// <summary> List this server's roles and their ID. </summary>
        [Command("roles"), Permission(MinimumPermission.GuildOwner)]
        [RequireContext(ContextType.Guild)]
        [Summary("List this server's roles and their ID.")]
        public Task ListRoles()
        {
            return (Context.Channel is IGuildChannel ch)
                ? ReplyAsync(
                    $"This server's roles:\n {String.Join("\n", Context.Guild.Roles.Where(r => r.Id != Context.Guild.EveryoneRole.Id).Select(r => $"{r.Name} : {r.Id}"))}")
                : Task.CompletedTask;
        }

        /// <summary> "List all the modules loaded in the bot. </summary>
        [Command("modules"), Permission(MinimumPermission.ModRole)]
        [RequireContext(ContextType.Guild)]
        [Summary("List all the modules loaded in the bot.")]
        public Task ListModules()
        {
            if (Context.Channel is IGuildChannel ch)
            {
                var mods = PermService.CService.Modules
                    .Where(m => m.Name != PermModuleName)
                    .Select(m => m.Name);
                var index = 1;
                var sb = new StringBuilder("All loaded modules:\n```");
                foreach (var m in mods)
                {
                    sb.AppendLine($"{index,3}: {m}");
                    index++;
                }
                sb.Append("```");

                return ReplyAsync(sb.ToString());
            }
            return Task.CompletedTask;
        }

        /// <summary> Set the admin role for this server. </summary>
        /// <param name="role"></param>
        [Command("setadmin"), Permission(MinimumPermission.GuildOwner)]
        [RequireContext(ContextType.Guild)]
        [Summary("Set the admin role for this server.")]
        public async Task SetAdminRole(IRole role)
        {
            if (Context.Channel is IGuildChannel ch)
            {
                if (role.Id == Context.Guild.EveryoneRole.Id)
                {
                    await ReplyAsync($"Not allowed to set `everyone` as the admin role.").ConfigureAwait(false);
                    return;
                }

                if (await PermService.SetGuildAdminRole(Context.Guild, role).ConfigureAwait(false))
                    await ReplyAsync($"Set **{role.Name}** as the admin role for this server.").ConfigureAwait(false);
            }
        }

        /// <summary> Set the moderator role for this server. </summary>
        /// <param name="role"></param>
        [Command("setmod"), Permission(MinimumPermission.GuildOwner)]
        [RequireContext(ContextType.Guild)]
        [Summary("Set the moderator role for this server.")]
        public async Task SetModRole(IRole role)
        {
            if (Context.Channel is IGuildChannel ch)
            {
                if (role.Id == Context.Guild.EveryoneRole.Id)
                {
                    await ReplyAsync($"Not allowed to set `everyone` as the mod role.").ConfigureAwait(false);
                    return;
                }

                if (await PermService.SetGuildModRole(Context.Guild, role).ConfigureAwait(false))
                    await ReplyAsync($"Set **{role.Name}** as the mod role for this server.").ConfigureAwait(false);
            }
        }

        /// <summary> Give someone special command privileges in this channel. </summary>
        /// <param name="user"></param>
        [Command("addspecial"), Permission(MinimumPermission.ModRole)]
        [Alias("addsp"), RequireContext(ContextType.Guild)]
        [Summary("Give someone special command privileges in this channel.")]
        public async Task AddSpecialUser(IGuildUser user)
        {
            if (user.HasPerms(Context.Channel as ITextChannel, PermissionsExtensions.DiscordPermissions.READ_MESSAGES | PermissionsExtensions.DiscordPermissions.SEND_MESSAGES))
            {
                if (await PermService.AddSpecialUser(Context.Channel, user).ConfigureAwait(false))
                    await ReplyAsync($"Gave **{user.Username}** Special command privileges.").ConfigureAwait(false);
            }
            else
            {
                await ReplyAsync("That user has no read/write permissions in this channel.").ConfigureAwait(false);
            }
        }

        /// <summary> Remove someone's special command privileges in this channel. </summary>
        /// <param name="user"></param>
        [Command("remspecial"), Permission(MinimumPermission.ModRole)]
        [Alias("remsp"), RequireContext(ContextType.Guild)]
        [Summary("Remove someone's special command privileges in this channel.")]
        public async Task RemoveSpecialUser(IGuildUser user)
        {
            if (await PermService.RemoveSpecialUser(Context.Channel, user).ConfigureAwait(false))
                await ReplyAsync($"Removed **{user.Username}** Special command privileges.").ConfigureAwait(false);
        }

        /// <summary> Whitelist a module for this channel. </summary>
        /// <param name="modName"></param>
        [Command("whitelist"), Permission(MinimumPermission.ModRole)]
        [Alias("wl"), RequireContext(ContextType.Guild)]
        [Summary("Whitelist a module for this channel or guild.")]
        public async Task WhitelistModule(string modName, [OverrideTypeReader(typeof(SpecialBoolTypeReader))] bool guildwide = false)
        {
            var ch = Context.Channel as IGuildChannel;
            var mod = PermService.CService.Modules.SingleOrDefault(m => m.Name == modName);
            if (mod != null)
            {
                if (guildwide)
                {
                    if (await PermService.WhitelistModuleGuild(ch.Guild, mod.Name).ConfigureAwait(false))
                        await ReplyAsync($"Module `{mod.Name}` is now whitelisted in this server.").ConfigureAwait(false);
                }
                else
                {
                    if (await PermService.WhitelistModule(ch, mod.Name).ConfigureAwait(false))
                        await ReplyAsync($"Module `{mod.Name}` is now whitelisted in this channel.").ConfigureAwait(false);
                }
            }
        }

        /// <summary> Blacklist a module for this channel. </summary>
        /// <param name="modName"></param>
        [Command("blacklist"), Permission(MinimumPermission.ModRole)]
        [Alias("bl"), RequireContext(ContextType.Guild)]
        [Summary("Blacklist a module for this channel or guild.")]
        public async Task BlacklistModule(string modName, [OverrideTypeReader(typeof(SpecialBoolTypeReader))] bool guildwide = false)
        {
            var ch = Context.Channel as IGuildChannel;
            var mod = PermService.CService.Modules.SingleOrDefault(m => m.Name == modName);
            if (mod != null)
            {
                if (mod.Name == PermModuleName)
                {
                    await ReplyAsync($"Not allowed to blacklist {nameof(PermModuleName)}.").ConfigureAwait(false);
                }
                else
                {
                    if (guildwide)
                    {
                        if (await PermService.BlacklistModuleGuild(ch.Guild, mod.Name).ConfigureAwait(false))
                            await ReplyAsync($"Module `{mod.Name}` is now blacklisted in this server.").ConfigureAwait(false);
                    }
                    else
                    {
                        if (await PermService.BlacklistModule(ch, mod.Name).ConfigureAwait(false))
                            await ReplyAsync($"Module `{mod.Name}` is now blacklisted in this channel.").ConfigureAwait(false);
                    }
                }
            }
        }

    }

    ///// <summary> </summary>
    //public sealed class SocketPermissionsModule : PermissionModule<SocketCommandContext>
    //{
    //    public SocketPermissionsModule(PermissionsService permService)
    //        : base(permService)
    //    {
    //    }

    //    protected async override Task<bool> UseFancy()
    //    {
    //        using (var config = PermService.ConfigStore.Load())
    //        {
    //            bool fancyEnabled = Context.Guild != null && await config.GetFancyHelpValue(Context.Guild).ConfigureAwait(false);
    //            return fancyEnabled && PermissionsService.GetMessageCacheSize(Context.Client) > 0
    //                && Context.Guild.CurrentUser.HasPerms(Context.Channel as ITextChannel,
    //                    PermissionsExtensions.DiscordPermissions.ADD_REACTIONS
    //                    | PermissionsExtensions.DiscordPermissions.MANAGE_MESSAGES);
    //        }
    //    }
    //}

    /// <summary> </summary>
    //public sealed class ShardedPermissionsModule : PermissionModule<ShardedCommandContext>
    //{
    //    /// <summary> </summary>
    //    public ShardedPermissionsModule(PermissionsService permService)
    //        : base(permService)
    //    {
    //    }

    //    protected async override Task<bool> UseFancy()
    //    {
    //        using (var config = PermService.ConfigStore.Load())
    //        {
    //            bool fancyEnabled = Context.Guild != null && await config.GetFancyHelpValue(Context.Guild).ConfigureAwait(false);
    //            return fancyEnabled && PermissionsService.GetMessageCacheSize(Context.Client.GetShard(0)) > 0
    //                && Context.Guild.CurrentUser.HasPerms(Context.Channel as ITextChannel,
    //                    PermissionsExtensions.DiscordPermissions.ADD_REACTIONS
    //                    | PermissionsExtensions.DiscordPermissions.MANAGE_MESSAGES);
    //        }
    //    }
    //}
}
