﻿using Discord.WebSocket;
using Discord.Commands;
using System;
using System.Linq;
using System.Threading.Tasks;
using Kaguya.Core.Server_Files;
using Discord;
using Kaguya.Modules.osu;
using System.Diagnostics;
using System.Timers;
using System.Collections.Generic;
using System.Reflection;

namespace Kaguya.Core.Command_Handler
{
    public class Timers
    {
        readonly DiscordSocketClient _client = Global.Client;
        readonly public IServiceProvider _services;
        readonly Logger logger = new Logger();

        public Task GameTimer()
        {
            Timer timer = new Timer(600000); //10 minutes
            timer.Elapsed += Game_Timer_Elapsed;
            timer.AutoReset = true;
            timer.Enabled = true;
            return Task.CompletedTask;
        }

        int displayIndex = 0;

        private void Game_Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            string[] games = { "Support Server: yhcNC97", "$help | @Kaguya#2708 help",
            $"Servicing {_client.Guilds.Count()} guilds", $"Serving {UserAccounts.UserAccounts.GetAllAccounts().Count().ToString("N0")} users" };
            displayIndex++;
            if (displayIndex >= games.Length)
            {
                displayIndex = 0;
            }

            _client.SetGameAsync(games[displayIndex]);
            logger.ConsoleTimerElapsed($"Game updated to \"{games[displayIndex]}\"");
        }

        public Task CheckChannelPermissions()
        {
            Timer timer = new Timer(86400000); //24 hours
            timer.Elapsed += Check_Channel_Permissions_Elapsed;
            timer.AutoReset = true;
            timer.Enabled = true;
            return Task.CompletedTask;
        }

        private void Check_Channel_Permissions_Elapsed(object sender, ElapsedEventArgs e)
        {
            var kID = ulong.TryParse(Config.bot.botUserID, out ulong ID);
            SocketUser kaguya = _client.GetUser(ID);
            if (kaguya != null)
            {
                var servers = Servers.GetAllServers();
                foreach (var server in servers.ToList())
                {
                    if (server.IsBlacklisted == false)
                    {
                        var id = server.ID;
                        var guild = _client.GetGuild(id);
                        if (guild == null) //If the server returns null, delete it from the database.
                        {
                            logger.ConsoleCriticalAdvisory($"Guild returned null for {server.ID} [REMOVING!!], Timers.cs line 109.");
                            Servers.RemoveServer(server.ID);
                            continue;
                        }

                        int i = 0;

                        foreach (SocketTextChannel channel in guild.TextChannels)
                        {
                            if (!channel.GetPermissionOverwrite(kaguya).HasValue && server.IsBlacklisted == false)
                            {
                                try
                                {
                                    channel.AddPermissionOverwriteAsync(kaguya, OverwritePermissions.AllowAll(channel));
                                    i++;
                                }
                                catch (Exception exception)
                                {
                                    logger.ConsoleStatusAdvisory($"Could not overwrite permissions for #{channel.Name} in guild \"{channel.Guild.Name}\"");
                                    logger.ConsoleCriticalAdvisory(exception, $"Guild {guild.Name} has been blacklisted.");
                                    server.IsBlacklisted = true;
                                }
                            }
                        }
                        logger.ConsoleGuildAdvisory($"Kaguya has been granted permissions for {i} new channels.");
                        continue;
                    }
                }
            }
            else
            {
                return;
            }
        }

        public Task ServerInformationUpdate()
        {
            Timer timer = new Timer(86400000); //24 hours
            timer.Elapsed += Server_Information_Update_Elapsed;
            timer.AutoReset = true;
            timer.Enabled = true;
            return Task.CompletedTask;
        }

        private void Server_Information_Update_Elapsed(object sender, ElapsedEventArgs e) //Updates every server's name (in case it changes) every 24 hours.
        {
            var servers = Servers.GetAllServers();
            int i = 0;


            foreach (var server in servers.ToList())
            {
                var oldServerName = server.ServerName;

                var guild = _client.GetGuild(server.ID); //If the server returns null, delete it from the database.
                if (guild == null)
                {
                    logger.ConsoleCriticalAdvisory($"Guild returned null for {server.ID} [REMOVING!!], Timers.cs line 109."); 
                    Servers.RemoveServer(server.ID);
                    continue;
                }
                if (guild != null && oldServerName != guild.Name)
                {
                    server.ServerName = guild.Name;
                    Servers.SaveServers();
                    i++;
                }
            }
            logger.ConsoleStatusAdvisory($"Updated server names for {i} guilds.");
        }

        public Task VerifyUsers()
        {
            Timer timer = new Timer(360000); //1 hour
            timer.Elapsed += Verify_Users_Elapsed;
            timer.AutoReset = true;
            timer.Enabled = true;
            return Task.CompletedTask;
        }

        private void Verify_Users_Elapsed(object sender, ElapsedEventArgs e)
        {
            var servers = Servers.GetAllServers();
            var users = UserAccounts.UserAccounts.GetAllAccounts();


            foreach(var user in users)
            {
                foreach (var ID in user.IsInServerIDs)
                {
                    List<string> oldSNames = user.IsInServers;
                    var guild = _client.GetGuild(ID);

                    if (!(oldSNames.Contains(guild.Name)))
                    {
                        user.IsInServers.Add(guild.Name);
                        UserAccounts.UserAccounts.SaveAccounts();
                    }
                }
            }
        }

        public Task VerifyMessageReceived()
        {
            Timer timer = new Timer(60000); //Every 60 seconds, make sure the bot is seeing messages. If it hasn't seen a message in 15 seconds, restart!
            timer.Elapsed += Verify_Message_Received_Elapsed;
            timer.AutoReset = true;
            timer.Enabled = true;
            return Task.CompletedTask;
        }

        private void Verify_Message_Received_Elapsed(object sender, ElapsedEventArgs e) //Restarts bot if no messages have been seen for 60 seconds.
        {
            var difference = DateTime.Now - Config.bot.LastSeenMessage;

            if(difference.TotalMilliseconds >= 60000)
            {
                var filePath = Assembly.GetExecutingAssembly().Location;
                Process.Start(filePath);
                Environment.Exit(0);
            }
        }
    }
}
