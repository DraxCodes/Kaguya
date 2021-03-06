﻿using Discord.WebSocket;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using Kaguya.Core.LevelingSystem;
using Kaguya.Core.UserAccounts;
using Kaguya.Core.Server_Files;
using Discord;
using Kaguya.Core.CommandHandler;
using Kaguya.Core;
using Kaguya.Core.Command_Handler;

#pragma warning disable

namespace Kaguya
{
    class CommandHandler
    {

        DiscordSocketClient _client;
        CommandService _service;
        private IServiceProvider _services;
        readonly Color Yellow = new Color(255, 255, 102);
        readonly Color SkyBlue = new Color(63, 242, 255);
        readonly Color Red = new Color(255, 0, 0);
        readonly Color Violet = new Color(238, 130, 238);
        readonly Color Pink = new Color(252, 132, 255);
        readonly KaguyaLogMethods logger = new KaguyaLogMethods();
        readonly Timers timers = new Timers();
        readonly Logger consoleLogger = new Logger();

        public string osuApiKey = Config.bot.OsuApiKey;
        public string tillerinoApiKey = Config.bot.TillerinoApiKey;
        public async Task InitializeAsync(DiscordSocketClient client)
        {
            try
            {
                _client = client;
                _service = new CommandService();
                _service.AddTypeReader(typeof(List<SocketGuildUser>), new ListSocketGuildUserTR());
                await _service.AddModulesAsync(
                  Assembly.GetExecutingAssembly(),
                  _services);
                _client.Connected += logger.ClientConnected;

                _client.Ready += logger.OnReady;
                _client.Ready += timers.CheckChannelPermissions;
                _client.Ready += timers.ServerInformationUpdate;
                _client.Ready += timers.GameTimer;
                _client.Ready += timers.VerifyMessageReceived;
                _client.Ready += timers.ServerMessageLogCheck;

                _client.MessageReceived += HandleCommandAsync;
                _client.MessageReceived += logger.osuLinkParser;
                _client.JoinedGuild += logger.JoinedNewGuild;
                _client.LeftGuild += logger.LeftGuild;
                _client.MessageReceived += logger.MessageCache;
                _client.MessageDeleted += logger.LoggingDeletedMessages;
                _client.MessageUpdated += logger.LoggingEditedMessages;
                _client.UserJoined += logger.LoggingUserJoins;
                _client.UserLeft += logger.LoggingUserLeaves;
                _client.UserBanned += logger.LoggingUserBanned;
                _client.UserUnbanned += logger.LoggingUserUnbanned;
                _client.MessageReceived += logger.LogChangesToLogSettings;
                _client.MessageReceived += logger.UserSaysFilteredPhrase;
                _client.UserVoiceStateUpdated += logger.UserConnectsToVoice;
                _client.Disconnected += logger.ClientDisconnected;
            }
            catch (ReflectionTypeLoadException ex)
            {
                StringBuilder sb = new StringBuilder();
                foreach (Exception exSub in ex.LoaderExceptions)
                {
                    sb.AppendLine(exSub.Message);
                    FileNotFoundException exFileNotFound = exSub as FileNotFoundException;
                    if (exFileNotFound != null)
                    {
                        if (!string.IsNullOrEmpty(exFileNotFound.FusionLog))
                        {
                            sb.AppendLine("Fusion Log:");
                            sb.AppendLine(exFileNotFound.FusionLog);
                        }
                    }
                    sb.AppendLine();
                }
                string errorMessage = sb.ToString();
                Console.WriteLine(errorMessage);
                return;
            }
            catch (Exception e)
            {
                consoleLogger.ConsoleCriticalAdvisory(e, "InitializeAsync method threw an exception. CommandHandler.cs lines (42, 92).");
                return;
            }
        }

        private async Task HandleCommandAsync(SocketMessage s)
        {
            var msg = s as SocketUserMessage;
            var user = msg.Author as SocketGuildUser;
            if (msg is null || user is null || user.IsBot) return;

            var guild = Servers.GetServer(user.Guild);
            var userAccount = UserAccounts.GetAccount(user);

            if (userAccount.Blacklisted == 1) { return; }
            if (guild.IsBlacklisted) { return; }


            var context = new SocketCommandContext(_client, msg);

            foreach (string phrase in guild.FilteredWords)
            {
                if (msg.Content.Contains(phrase))
                {
                    await logger.UserSaysFilteredPhrase(msg);
                    consoleLogger.ConsoleCommandLog(context);
                }
            }

            Leveling.UserSentMessage(user, msg.Channel as SocketTextChannel);

            string oldUsername = userAccount.Username;
            string newUsername = context.User.Username;

            if ($"{oldUsername}#{user.Discriminator}" != $"{newUsername}#{user.Discriminator}")
                userAccount.Username = $"{newUsername}#{user.Discriminator}";


            int argPos = 0;

            if (!msg.HasStringPrefix(guild.commandPrefix, ref argPos)
                && !msg.HasMentionPrefix(_client.CurrentUser, ref argPos)) { return; }

            var embed = new EmbedBuilder();
            var result = await _service.ExecuteAsync(context, argPos, null);

            if (!result.IsSuccess)
            {
                switch (result.Error)
                {
                    case CommandError.UnknownCommand:
                        embed.WithDescription($"**Error: The command `{context.Message.Content}` does not exist!**");
                        embed.WithFooter($"Use {guild.commandPrefix}h for the full commands list! Tag me with \"prefix <symbol>\" to edit my prefix!");
                        embed.WithColor(Red);
                        await context.Channel.SendMessageAsync(embed: embed.Build());
                        consoleLogger.ConsoleCommandLog(context, CommandError.UnknownCommand, $"The command {context.Message.Content} does not exist!");
                        break;
                    case CommandError.BadArgCount:
                        embed.WithDescription("**Error: I need a different set of information than what you've given me!**");
                        embed.WithFooter($"Bad argument count! Use {guild.commandPrefix}h <command> to see the proper syntax!");
                        embed.WithColor(Red);
                        await context.Channel.SendMessageAsync(embed: embed.Build());
                        consoleLogger.ConsoleCommandLog(context, CommandError.BadArgCount, "User attempted to use invalid parameters for a command.");
                        break;
                    case CommandError.ParseFailed:
                        embed.WithDescription("**Error: I failed to parse a specified value!**");
                        embed.WithFooter($"You may be using text instead of a number. Review {guild.commandPrefix}h <command> for the proper usage!");
                        embed.WithColor(Red);
                        await context.Channel.SendMessageAsync(embed: embed.Build());
                        consoleLogger.ConsoleCommandLog(context, CommandError.BadArgCount, "Failed to parse given value specified in command.");
                        break;
                    case CommandError.UnmetPrecondition:
                        embed.WithDescription($"**Error: {result.ErrorReason}**");
                        embed.WithFooter("Review $h <command> for the proper usage!");
                        embed.WithColor(Red);
                        await context.Channel.SendMessageAsync(embed: embed.Build());
                        consoleLogger.ConsoleCommandLog(context, CommandError.BadArgCount, $"{result.ErrorReason}");
                        break;
                    case CommandError.MultipleMatches:
                        embed.WithDescription("**Error: I found multiple matches for the task you were trying to execute!**");
                        embed.WithFooter($"Review {guild.commandPrefix}h <command> for the proper usage! I can only do one thing at a time!");
                        embed.WithColor(Red);
                        await context.Channel.SendMessageAsync(embed: embed.Build());
                        consoleLogger.ConsoleCommandLog(context, CommandError.BadArgCount, "Multiple matches found.");
                        break;
                    default:
                        embed.WithDescription("**Error: I failed to execute this command for an unknown reason.**");
                        embed.WithFooter($"Error reason: {result.ErrorReason}");
                        embed.WithColor(Red);
                        await context.Channel.SendMessageAsync(embed: embed.Build());
                        consoleLogger.ConsoleCommandLog(context, CommandError.Unsuccessful, $"{result.ErrorReason}");
                        break;
                }
            }
        }
    }
}
