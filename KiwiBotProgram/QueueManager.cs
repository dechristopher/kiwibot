using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace KiwiBotProgram
{
    public class Queue
    {
        public List<string> qPlayers { get; set; }
        public List<string> qNotified = new List<string> { };

        public bool qEnabled { get; set; }
        public bool displayQueue { get; set; }
        public int numQ { get; set; }
        public int qSize { get; set; }
        public int dcCount { get; set; }
        public int playersNeeded { get; set; }        
    }

    public class QueueManager
    {
        public bool isRunning { get; set; }
        public Queue CurrentQueue { get; set; }
        Helpers Helper = new Helpers();
        bool availableServer { get; set; }


        public List<SteamBot> steamBots { get; set; }

        //List of available servers
        public List<string> qServers = new List<string> { 
                "208.43.245.195:27020",
                "208.43.245.195:27021",
                "208.43.245.195:27022",
                "208.43.245.195:27023",
                "208.43.245.195:27024"
                //ADD MORE PLEASE
        };

        //List of available servers
        List<string> qServersEU = new List<string> { 
                "208.43.245.195:27020",
                "208.43.245.195:27021",
                "208.43.245.195:27022",
                "208.43.245.195:27023",
                "208.43.245.195:27024"
                //ADD MORE PLEASE
        };

        //List of active duty queue servers. Subject to change.
        string[] allServers = new string[]{
                "208.43.245.195:27020",
                "208.43.245.195:27021",
                "208.43.245.195:27022",
                "208.43.245.195:27023",
                "208.43.245.195:27024"
                //ADD MORE PLEASE
        };

        //List of active duty queue servers. Subject to change.
        string[] allServersEU = new string[]{
                "208.43.245.195:27020",
                "208.43.245.195:27021",
                "208.43.245.195:27022",
                "208.43.245.195:27023",
                "208.43.245.195:27024"
                //ADD MORE PLEASE
        };

        public QueueManager()
        {
            steamBots = new List<SteamBot>();
            CreateNewQueue();
        }

        public Queue CreateNewQueue()
        {
            CurrentQueue = new Queue()
            {
                dcCount = 0,
                numQ = 0,
                playersNeeded = 10,
                qEnabled = true,
                qNotified = new List<string>(),
                qPlayers = new List<string>(),
                qSize = 10
            };

            //return the newest queue we created
            return CurrentQueue;
        }



        #region async tasks
        public async void qTimer()
        {
            await PutTaskDelay();
            parseServers(allServers, qServers);
            parseQ();
        }

        public void parseServers(string[] allsrv, List<string> srv)
        {
            for (int i = 0; i < allsrv.Length; i++)
            {
                String apiHost = "http://kiirus.net/api.php/?key=2F6E713BD4BA889A21166251DEDE9&ip=" + allsrv[i] + "&cmd=host";
                String apiPlayers = "http://kiirus.net/api.php/?key=2F6E713BD4BA889A21166251DEDE9&ip=" + allsrv[i] + "&cmd=players";
                WebClient client = new WebClient();

                string hostname = client.DownloadString(apiHost);
                string players = client.DownloadString(apiPlayers);

                if ((hostname.Contains("offline") || hostname.Contains("LIVE") || (hostname.Contains("NEED") && !hostname.Contains("10"))) && srv.Contains(allsrv[i]))
                {
                    srv.Remove(allsrv[i]);
                    Console.WriteLine("[SERVER] REMOVED :|: " + allsrv[i] + " :|: due to server offline.\n");
                }

                if ((hostname.Equals("KIWI :: ") || hostname.Contains("NEED 10")) && players == "0" && !srv.Contains(allsrv[i]))
                {
                    srv.Add(allsrv[i]);
                    Console.WriteLine("[SERVER] ADDED :|: " + allsrv[i] + " :|: to available server pool.\n");
                }
            }
            Console.WriteLine("╔═══════════════════════════════════╗");
            Console.WriteLine("║ AVAILABLE SERVERS:                ║");
            for (int s = 0; s < srv.Count; s++)
            {
                Console.WriteLine("║ SRV :|: " + srv[s] + "      ║");
            }
            Console.WriteLine("╚═══════════════════════════════════╝\n");
        }


        public void parseQ()
        {
            int numQ = CurrentQueue.qPlayers.Count;

            string[] gameList = new string[CurrentQueue.qSize];
            if (qServers.Count >= 1)
            {
                availableServer = true;
                if (numQ >= CurrentQueue.qSize)
                {
                    for (int i = 0; i < CurrentQueue.qSize; i++)
                    {
                        int index = (Helper.RandomNumber(0, CurrentQueue.qPlayers.Count));
                        string id = CurrentQueue.qPlayers[index];
                        CurrentQueue.qPlayers.RemoveAt(index);
                        gameList[i] = id;
                    }
                    int randServ = Helper.RandomNumber(0, qServers.Count);
                    //int randServ = 0; //For testing some dumbass shit on server #1
                    string serverIP = qServers[randServ];
                    string serverPass = Helper.genQPass(serverIP);

                    qServers.Remove(serverIP);
                    Console.WriteLine("[SERVER] REMOVED :|: " + serverIP + " :|: from available server pool.\n");

                    switch (serverPass)
                    {
                        case "208.43.245.195:27020":
                            serverIP = "na-e.kiirus.net:27020";
                            break;
                        case "208.43.245.195:27021":
                            serverIP = "na-e.kiirus.net:27021";
                            break;
                        case "208.43.245.195:27022":
                            serverIP = "na-e.kiirus.net:27022";
                            break;
                        case "208.43.245.195:27023":
                            serverIP = "na-e.kiirus.net:27023";
                            break;
                        case "208.43.245.195:27024":
                            serverIP = "na-e.kiirus.net:27024";
                            break;
                        case "188.138.41.151:27015":
                            serverIP = "eu-w.kiirus.net:27015";
                            break;
                    }


                    for (int i = 0; i < CurrentQueue.qPlayers.Count(); i++)
                    {
                        foreach (SteamBot steambot in steamBots)
                        {
                            for (int j = 0; j < steambot.steamFriends.GetFriendCount(); j++)
                            {
                                if (gameList[i] == steambot.steamFriends.GetFriendByIndex(j).ToString())
                                {
                                    steambot.steamFriends.SendChatMessage(steambot.steamFriends.GetFriendByIndex(j), EChatEntryType.ChatMsg, "[PUG] ! >>>");
                                    steambot.steamFriends.SendChatMessage(steambot.steamFriends.GetFriendByIndex(j), EChatEntryType.ChatMsg, "[PUG] 10 Players found! Starting match. Paste this in your console:");
                                    steambot.steamFriends.SendChatMessage(steambot.steamFriends.GetFriendByIndex(j), EChatEntryType.ChatMsg, "[PUG] password " + serverPass + "; connect " + serverIP + ";");
                                    steambot.steamFriends.SendChatMessage(steambot.steamFriends.GetFriendByIndex(j), EChatEntryType.ChatMsg, "[PUG] ! >>>");
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (CurrentQueue.displayQueue && CurrentQueue.qEnabled)
                    {
                        Console.WriteLine("[PUG] (" + numQ + "/10) Waiting for 10 players...\n");
                    }
                    else if (CurrentQueue.displayQueue && !CurrentQueue.qEnabled)
                    {
                        Console.WriteLine("[PUG] (" + numQ + "/10) QUEUE DISABLED...\n");
                    }
                }
            }
            else
            {
                if (CurrentQueue.displayQueue)
                {
                    availableServer = false;
                    Console.WriteLine("[PUG] (" + numQ + "/10) Waiting for an available server...\n");
                }
            }
        }

        async Task PutTaskDelay()
        {
            await Task.Delay(5000);
        }

        async Task ReadyDelayInitial()
        {
            await Task.Delay(20000);
        }

        async Task ReadyDelayWarning()
        {
            await Task.Delay(10000);
        }
        #endregion


    }
}
