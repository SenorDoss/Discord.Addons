﻿Extra considerations
====================

For your Game logic, you will likely want to limit some commands
to certain times or players. This can be accomplished using Preconditions.

For example, if a certain command can only be used by the Turn Player,
you will need to make a class like this:
```cs
using System;
using System.Threading.Tasks;
using Discord.Commands;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class RequireTurnPlayerAttribute : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckPermissions(ICommandContext context, CommandInfo command, IDependencyMap map)
    {
        var service = map.Get<CardGameService>();
        if (service != null)
        {
            var authorId = context.User.Id;
            var game = service.GameList.Values
                .Where(g => g.PlayerChannels.Select(c => c.Id).Contains(context.Channel.Id))
                .FirstOrDefault();

            if (game != null || service.GameList.TryGetValue(context.Channel.Id, out game))
            {
                return (game.TurnPlayer.Value.User.Id == authorId)
                    ? Task.FromResult(PreconditionResult.FromSuccess())
                    : Task.FromResult(PreconditionResult.FromError("Cannot use command at this time."));
            }
            return Task.FromResult(PreconditionResult.FromError("No game active."));
        }
        return Task.FromResult(PreconditionResult.FromError("No service found."));
    }
}
```

You can then apply `[RequireTurnPlayer]` on any Command method in your Module that
is only allowed to be used by the Turn Player. Other preconditions, for example,
checking if the game is in a particular state, can be made in a similar way.


[<- Part 6 - Final step](6-FinalStep.md) - Extra considerations