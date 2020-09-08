using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace Discord_Bot___Bob.CommandFolder
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        [Command("spam")]
        public async Task Spam([Remainder]string text)
        {
           for(int i = 0;i< 10;i++)
            {
                await ReplyAsync(text);
                await PutTaskDelay();
            }
        }

        [Command("mostbeautifulgirl")]
        public async Task princess()
        {
            await ReplyAsync("jessie");
        }

        [Command("game")]
        public async Task chooseGame([Remainder]string text)
        {
            var rand = new Random();
            string[] games = text.Split(',');
            await ReplyAsync($"The Game Options are: ");
            for (int i = 0; i < games.Length; i++)
            {
                await ReplyAsync($"{games[i]}");
                await PutTaskDelay();
            }
            await PutTaskDelay();
            await ReplyAsync($"\nChoosing........");
            await PutTaskDelay();
            await ReplyAsync($"\n.......................");
            await PutTaskDelay();
            await ReplyAsync($"\n..................................");
            int choice = rand.Next(0, games.Length);
            await ReplyAsync($"\nGAME IS: {games[choice]}");
        }

        [Command("shee")]
        public async Task sheeee()
        {
            await ReplyAsync("!sheeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
        }

        [Command("play shee")]
        public async Task sheeee2()
        {
            await ReplyAsync("!play molly remix playboi");
        }
 
        async Task PutTaskDelay()
        {
            await Task.Delay(1000);
        }

    }
}

