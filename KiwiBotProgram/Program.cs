using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Linq;
using System.Web;
using System.Xml.XPath;
using System.Text.RegularExpressions;
using System.Net;
using KiwiBotProgram;
using System.Threading;

/*
*  built by andrew dechristopher
  * 
    *  KIWIBot PUG Build
  * 
*  for use with the kiwi pug network
*/

/*
* TODO 
*  - >>>>>> Implement a reporting system using SteamIDs and ingame instructions
*  - >>>> Add 5 more queue servers <-
*  - >> Implement stats using backup_roundXX.txt in server files
*  - Implement total number of PUGs played (Currently at 14)
*  - RELEASE TO REDDIT AND MAKE ABSOLUTELY NO BANK!
*/

namespace InstructionsForTut
{
    class Program
    {

        string version = "4.3.1";
        int build = 1182;
        string buildDate = "1/20/2016 @ 12:46 PM EST";

        static string user, pass;
        static string authCode = "";

        static bool displayQueue = false;
        static bool availableServer = true;



        static string whitelistPath = "C:\\KIWI\\kiwiPUG\\csgo\\addons\\sourcemod\\configs\\whitelist\\whitelist.txt";
        //static string whitelistPath = "C:\\Users\\Drew\\Desktop\\whitelist.txt";

        public string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo info = Console.ReadKey(true);
            while (info.Key != ConsoleKey.Enter)
            {
                if (info.Key != ConsoleKey.Backspace)
                {
                    Console.Write("*");
                    password += info.KeyChar;
                } 
                else if (info.Key == ConsoleKey.Backspace)
                {
                    if (!string.IsNullOrEmpty(password))
                    {
                        password = password.Substring(0, password.Length - 1);
                        int pos = Console.CursorLeft;
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                        Console.Write(" ");
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                    }
                }
                info = Console.ReadKey(true);
            }
            Console.WriteLine();
            return password;
        }


        static void Main(string[] args)
        {
            Program program = new Program();
            List<Thread> Threads = new List<Thread>();

            /*
            if (!File.Exists("chat.txt"))
            {
                File.Create("chat.txt").Close();
                File.WriteAllText("chat.txt", "abc | 123");
            }
            if (!File.Exists("admin.txt"))
            {
                File.Create("admin.txt").Close();
                File.WriteAllText("admin.txt", "76561198060315636"); 
            }
            */

            //reloadConfig();

            Console.Title = ">> KIWI Bot (v" + program.version + ") <<";

            var arr = new[]
            {
                    @"      /$$   /$$ /$$$$$$ /$$      /$$ /$$$$$$ /$$$$$$$              /$$    ", 
                    @"     | $$  /$$/|_  $$_/| $$  /$ | $$|_  $$_/| $$__  $$            | $$    ", 
                    @"     | $$ /$$/   | $$  | $$ /$$$| $$  | $$  | $$  \ $$  /$$$$$$  /$$$$$$  ", 
                    @"     | $$$$$/    | $$  | $$/$$ $$ $$  | $$  | $$$$$$$  /$$__  $$|_  $$_/  ", 
                    @"     | $$  $$    | $$  | $$$$_  $$$$  | $$  | $$__  $$| $$  \ $$  | $$    ", 
                    @"     | $$\  $$   | $$  | $$$/ \  $$$  | $$  | $$  \ $$| $$  | $$  | $$ /$$", 
                    @"     | $$ \  $$ /$$$$$$| $$/   \  $$ /$$$$$$| $$$$$$$/|  $$$$$$/  |  $$$$/", 
                    @"     |__/  \__/|______/|__/     \__/|______/|_______/  \______/    \___/  ", 
                    @"                                                                          ", 
                    @"      v" + program.version + " - Copyright KIWI Gaming 2016                           ", 
            };

            Console.WindowWidth = 80;
            Console.WindowHeight = 21;

            Console.WriteLine("\n\n");

            foreach (string line in arr)
                Console.WriteLine(line);

            Console.WriteLine("\n\n");

            Console.WriteLine(">> CTRL+C quits the program.");

            Console.Write(">> Number of bots");
            int botNumber = Int32.Parse(program.ReadPassword());

            List<SteamBot> SteamBots = new List<SteamBot>();
            QueueManager queueManager = new QueueManager();

            for (int i = 0; i < botNumber; i++)
            {

                Console.Write(">> Username: ");

                user = program.ReadPassword();

                Console.Write(">> Password: ");

                pass = program.ReadPassword();

                SteamBot steamBot = new SteamBot(user, pass, authCode, queueManager);
                Threads.Add(new Thread(new ThreadStart(steamBot.SteamLogIn)));
                SteamBots.Add(steamBot);
                queueManager.steamBots.Add(steamBot);
            }

            Threads.ForEach(t => t.Start());

            //wait for stuff to happennnnnnn
            queueManager.qTimer();
            Console.ReadKey();
        }
    }

}