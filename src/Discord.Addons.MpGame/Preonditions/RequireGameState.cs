﻿//using System;
//using System.Threading.Tasks;
//using Microsoft.Extensions.DependencyInjection;
//using Discord.Commands;

//namespace Discord.Addons.MpGame
//{
//    public abstract partial class MpGameModuleBase<TService, TGame, TPlayer>
//    {
//        private sealed class RequireGameStateAttribute<TState> //: PreconditionAttribute
//            where TState : struct//, Enum
//        {
//            private readonly TState _state;

//            public RequireGameStateAttribute(TState state)
//            {
//                _state = state;
//            }

//            public /*override*/ Task<PreconditionResult> CheckPermissions(
//                ICommandContext context,
//                CommandInfo command,
//                IServiceProvider services)
//            {
//                var service = services.GetService<TService>();
//                if (service != null)
//                {
//                    var game = service.GetGameFromChannel(context.Channel);
//                    if (game != null)
//                    {
//                        //return (data.GameOrganizer.Id == context.User.Id)
//                        //    ? Task.FromResult(PreconditionResult.FromSuccess())
//                        //    : Task.FromResult(PreconditionResult.FromError("Command can only be used by the user that intialized the game."));
//                    }
//                    return Task.FromResult(PreconditionResult.FromError("No game."));
//                }
//                return Task.FromResult(PreconditionResult.FromError("No service."));
//            }
//        }
//    }
//}
