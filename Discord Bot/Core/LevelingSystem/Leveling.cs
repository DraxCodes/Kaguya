﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord_Bot.Core.UserAccounts;

namespace Discord_Bot.Core.LevelingSystem
{
    internal static class Leveling
    {

        internal static async void UserSentMessage(SocketGuildUser user, SocketTextChannel channel)
        {
            Color Pink = new Color(252, 132, 255);

            var userAccount = UserAccounts.UserAccounts.GetAccount(user);
            Random RNGTimeout = new Random();
            if (!CanReceiveExperience(userAccount, RNGTimeout.Next(110, 130)))
                return;
            uint oldLevel = userAccount.LevelNumber;
            Random random = new Random();
            uint newExp = (uint)random.Next(7, 11);
            userAccount.EXP = userAccount.EXP + newExp;
            userAccount.LastReceivedEXP = DateTime.Now;
            UserAccounts.UserAccounts.SaveAccounts();
            uint newLevel = userAccount.LevelNumber;
            if (oldLevel != userAccount.LevelNumber)
            {
                EmbedBuilder embed = new EmbedBuilder();
                embed.WithTitle("Level up!");
                embed.WithDescription($"{user.Username} just leveled up!");
                embed.AddField("Level", newLevel);
                embed.AddField("EXP", userAccount.EXP);
                embed.WithColor(Pink);
               
                await channel.SendMessageAsync("", false, embed.Build());
            }
        }

        internal static bool CanReceiveExperience(UserAccount user, int timeout)
        {
            var difference = DateTime.Now - user.LastReceivedEXP;
            return difference.TotalSeconds > timeout;
        }
    }
}