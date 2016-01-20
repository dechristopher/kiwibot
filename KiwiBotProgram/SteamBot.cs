using SteamKit2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace KiwiBotProgram
{
    public class SteamBot
    {
        string version = "4.3.1";
        int build = 1182;
        string buildDate = "1/20/2016 @ 12:46 PM EST";
        int dcCount = 0;
        string whitelistPath = "C:\\KIWI\\kiwiPUG\\csgo\\addons\\sourcemod\\configs\\whitelist\\whitelist.txt";

        static string user, pass, authCode;

        Helpers Helper;
        RootObject config;
        SteamClient steamClient;
        CallbackManager manager;
        SteamUser steamUser;
        public SteamFriends steamFriends;
        QueueManager queueManager;

        bool isRunning = false;

        public SteamBot(string username, string password, string authcode, QueueManager queueManager)
        {
            this.Helper = new Helpers();
            user = username;
            pass = password;
            authCode = authcode;
            this.queueManager = queueManager;
        }

        public void SteamLogIn()
        {
            steamClient = new SteamClient();
            manager = new CallbackManager(steamClient);
            steamUser = steamClient.GetHandler<SteamUser>();
            steamFriends = steamClient.GetHandler<SteamFriends>();

            new Callback<SteamClient.ConnectedCallback>(OnConnected, manager);
            new Callback<SteamClient.DisconnectedCallback>(OnDisconnected, manager);
            new Callback<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth, manager);
            new Callback<SteamUser.LoggedOnCallback>(OnLoggedOn, manager);
            new Callback<SteamFriends.FriendsListCallback>(OnFriendsList, manager);
            new Callback<SteamUser.AccountInfoCallback>(OnAccountInfo, manager);
            new Callback<SteamFriends.FriendMsgCallback>(OnChatMessage, manager);

            isRunning = true;
            Console.WriteLine("\n>> Connecting to Steam...\n");

            steamClient.Connect();

            isRunning = true;
            while (isRunning)
            {
                manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
        }

        #region callbacks
        //connect our bot to the steam network
        void OnConnected(SteamClient.ConnectedCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                Console.WriteLine(">> Unable to connect to steam: {0}\n", callback.Result);
                isRunning = false;
                return;
            }

            Console.WriteLine(">> Connected to Steam. \nLogging in '{0}'...\n", user);

            byte[] sentryHash = null;
            if (File.Exists("sentry.bin"))
            {
                byte[] sentryFile = File.ReadAllBytes("sentry.bin");
                sentryHash = CryptoHelper.SHAHash(sentryFile);
            }

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = user,
                Password = pass,
                AuthCode = authCode,

                SentryFileHash = sentryHash,
            });
        }

        //do this after we log on
        void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result == EResult.AccountLogonDenied)
            {
                Console.WriteLine(">> This account is SteamGuard protected.");
                Console.Write(">> Please enter the auth code sent to the email at {0}: ", callback.EmailDomain);
                authCode = Console.ReadLine();

                return;
            }
            if (callback.Result != EResult.OK)
            {
                Console.WriteLine(">> Unable to log in to Steam: {0}\n", callback.Result);
                isRunning = false;
                return;
            }
            Console.WriteLine(">> {0} successfully logged in!\n", user);
            queueManager.CurrentQueue.displayQueue = true;
        }

        //when we are verified do this
        void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
        {
            Console.WriteLine(">> Updating sentry file...");

            byte[] sentryHash = CryptoHelper.SHAHash(callback.Data);
            File.WriteAllBytes("sentry.bin", callback.Data);
            steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
            {
                JobID = callback.JobID,
                FileName = callback.FileName,
                BytesWritten = callback.BytesToWrite,
                FileSize = callback.Data.Length,
                Offset = callback.Offset,
                Result = EResult.OK,
                LastError = 0,
                OneTimePassword = callback.OneTimePassword,
                SentryFileHash = sentryHash,
            });
            Console.WriteLine("Done.\n");
        }

        //if we disconnect do this
        void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            dcCount += 1;
            Console.WriteLine("\n>> (" + dcCount + ") {0} disconnected from Steam, reconnecting in 5...\n", user);
            Thread.Sleep(TimeSpan.FromSeconds(1));
            steamClient.Connect();
        }

        //after we load up our firneds list, see if we can add the newbie
        void OnFriendsList(SteamFriends.FriendsListCallback callback)
        {
            Thread.Sleep(2500);
            foreach (var friend in callback.FriendList)
            {
                if (friend.Relationship == EFriendRelationship.RequestRecipient)
                {
                    steamFriends.AddFriend(friend.SteamID);

                    Thread.Sleep(500);
                    int verify = verifyAccount(friend.SteamID);
                    if (verify == 5 || verify == 4 || verify == 3 || verify == 2 || verify == 1)
                    {
                        steamFriends.RemoveFriend(friend.SteamID);
                    }
                    else
                    {
                        steamFriends.SendChatMessage(friend.SteamID, EChatEntryType.ChatMsg, "Hello! I am the KIWI Bot! I automate and administrate the KIWI PUG network.");
                        steamFriends.SendChatMessage(friend.SteamID, EChatEntryType.ChatMsg, "To queue for a match, type !q join.");
                        steamFriends.SendChatMessage(friend.SteamID, EChatEntryType.ChatMsg, "To see all available commands, type !help.");
                    }
                }
            }
        }

        //set that the bot in online
        void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            steamFriends.SetPersonaState(EPersonaState.Online);
        }

        #endregion


        //check to see if the person messaging us is an admin
        public bool isBotAdmin(SteamID sid)
        {
            try
            {
                foreach (UInt64 ui in config.Admins)
                    if (ui == sid)
                        return true;
                steamFriends.SendChatMessage(sid, EChatEntryType.ChatMsg, "You are not an admin.");
                Console.WriteLine(steamFriends.GetFriendPersonaName(sid) + " attempted to use an administrator command while not an administrator.");
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        //returns hours
        //if not recently played or not owned, returns -1
        //if private profile, returns -2
        public int getCSGOHours(SteamID sid)
        {
            String hoursXML = "http://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v0001/?key=0659FE2A85CAA34AC96AB382172F4BFF&steamid=" + sid.ConvertToUInt64() + "&format=xml";

            XmlDocument csHours = new XmlDocument();
            csHours.Load(hoursXML);

            if (csHours.ChildNodes.Count == 0)
            {
                return -2;
            }
            else
            {
                XmlElement root = csHours.DocumentElement;
                XmlNodeList nodes = root.SelectNodes("/response/games/message");

                int minutes = 0;
                int id = 0;
                bool gotCSGO = false;

                foreach (XmlNode node in nodes)
                {
                    string appid = node["appid"].InnerText;
                    string playtime = node["playtime_forever"].InnerText;

                    if (appid == "730")
                    {
                        minutes = Int32.Parse(playtime);
                        id = Int32.Parse(appid);
                        gotCSGO = true;
                    }
                }

                int hours = minutes / 60;

                if (gotCSGO)
                {
                    return hours;
                }
                else
                {
                    return -1;
                }
            }
        }

        public int verifyAccount(SteamID sid)
        {
            try
            {
                int hours = getCSGOHours(sid);
                bool vac = getBans(sid, 0);
                bool owb = getBans(sid, 1);

                if (hours >= 400)
                {
                    if (!vac)
                    {
                        if (!owb)
                        {
                            steamFriends.SendChatMessage(sid, EChatEntryType.ChatMsg, "[VERIFY] You have passed verification!");
                            Console.WriteLine("[VERIFY] " + steamFriends.GetFriendPersonaName(sid) + " passed verification!");
                            return 0;
                        }
                        else
                        {
                            steamFriends.SendChatMessage(sid, EChatEntryType.ChatMsg, "[VERIFY] You have failed verification. You currently have an active game ban. As a result, you are not allowed to play on the KIWI PUG network. Have a nice day.");
                            Console.WriteLine("[VERIFY] " + steamFriends.GetFriendPersonaName(sid) + " failed verification! Has current game ban!");
                            return 1;
                        }
                    }
                    else
                    {
                        steamFriends.SendChatMessage(sid, EChatEntryType.ChatMsg, "[VERIFY] You have failed verification. You currently have an active VAC ban. As a result, you are not allowed to play on the KIWI PUG network. Have a nice day.");
                        Console.WriteLine("[VERIFY] " + steamFriends.GetFriendPersonaName(sid) + " failed verification! Has current VAC ban!");
                        return 2;
                    }
                }
                else if (hours == -1)
                {
                    steamFriends.SendChatMessage(sid, EChatEntryType.ChatMsg, "[VERIFY] You have failed verification. You don't own CS:GO. As a result, you are not allowed to play on the KIWI PUG network. Have a nice day.");
                    Console.WriteLine("[VERIFY] " + steamFriends.GetFriendPersonaName(sid) + " failed verification! Does not own CS:GO!");
                    return 3;
                }
                else if (hours == -2)
                {
                    steamFriends.SendChatMessage(sid, EChatEntryType.ChatMsg, "[VERIFY] You have failed verification. You currently have a private profile. To play on the KIWI network, your profile must be set to public. Have a nice day.");
                    Console.WriteLine("[VERIFY] " + steamFriends.GetFriendPersonaName(sid) + " failed verification! Has private profile!");
                    return 4;
                }
                else
                {
                    steamFriends.SendChatMessage(sid, EChatEntryType.ChatMsg, "[VERIFY] You have failed verification. You must have at least 400 hours logged in CS:GO. As a result, you are not allowed to play on the KIWI PUG network until you reach that criteria. Have a nice day!");
                    Console.WriteLine("[VERIFY] " + steamFriends.GetFriendPersonaName(sid) + " failed verification! Not enough hours!");
                    return 5;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine("[VERIFY] Ah fuck, I verified and I've fucking done this. I can't believe there's an error.");
                return -1;
            }
        }

        public int parseReady(string[] sids, SteamFriends steamFriends)
        {
            for (int i = 0; i < sids.Length; i++)
            {
                SteamID sid = new SteamID(sids[i]);
                steamFriends.SendChatMessage(sid, EChatEntryType.ChatMsg, "[PUG] Please ready up with !ready.");
            }
            return 0;
        }

        public void qNotify()
        {
            for (int i = 0; i < queueManager.CurrentQueue.qNotified.Count; i++)
            {
                SteamID id = new SteamID(queueManager.CurrentQueue.qNotified[i]);
                string grammar = "players";
                if (queueManager.CurrentQueue.numQ == 1)
                {
                    grammar = "player";
                }
                steamFriends.SendChatMessage(id, EChatEntryType.ChatMsg, "[PUG] Someone joined the queue! Currently " + queueManager.CurrentQueue.numQ + " " + grammar + " in the queue. To disable this  message, type \"!q notify\"");
            }
        }

        public string qAction(SteamID sid, int action)
        {
            queueManager.CurrentQueue.numQ = queueManager.CurrentQueue.qPlayers.Count;
            if (action == 0) //Join queue
            {
                if (!queueManager.CurrentQueue.qPlayers.Contains(sid.ToString()))
                {
                    queueManager.CurrentQueue.qPlayers.Add(sid.ToString());
                    queueManager.CurrentQueue.numQ = queueManager.CurrentQueue.qPlayers.Count;
                    Console.WriteLine("[PUG] Player: " + sid.ToString() + " has joined the PUG queue.\n");
                    steamFriends.SendChatMessage(sid, EChatEntryType.ChatMsg, "[PUG] You have joined the PUG queue.");
                    steamFriends.SendChatMessage(sid, EChatEntryType.ChatMsg, "[PUG] Please open your game before joining the server.");
                    string isAre = "are";
                    string playerSLASHs = "players";
                    if (queueManager.CurrentQueue.numQ == 1)
                    {
                        isAre = "is";
                        playerSLASHs = "player";
                    }
                    steamFriends.SendChatMessage(sid, EChatEntryType.ChatMsg, "[PUG] There " + isAre + " currently " + queueManager.CurrentQueue.numQ + " " + playerSLASHs + " in the queue.");


                    //Notify all players with notifications enabled.
                    qNotify();

                    return "j"; //Joined queue.
                }
                else
                {
                    steamFriends.SendChatMessage(sid, EChatEntryType.ChatMsg, "[PUG] Unable to join PUG queue. You are already queued.");
                    return "uj"; //Unable to join.
                }
            }
            else if (action == 1)
            {
                if (queueManager.CurrentQueue.qPlayers.Contains(sid.ToString()))
                {
                    queueManager.CurrentQueue.qPlayers.Remove(sid.ToString());
                    Console.WriteLine("[PUG] Player: " + sid.ToString() + "has left the PUG queue.\n");
                    steamFriends.SendChatMessage(sid, EChatEntryType.ChatMsg, "[PUG] You have left the PUG queue.");
                    return "l"; //Left queue.
                }
                else
                {
                    steamFriends.SendChatMessage(sid, EChatEntryType.ChatMsg, "[PUG] Unable to leave PUG queue. You never joined it to begin with.");
                    return "ul"; //You never joined the queue to begin with.
                }
            }
            else if (action == 2)
            {
                return queueManager.CurrentQueue.numQ.ToString();
            }
            else if (action == 3)
            {
                if (queueManager.CurrentQueue.qNotified.Contains(sid.ToString()))
                {
                    queueManager.CurrentQueue.qNotified.Remove(sid.ToString());
                    return "noff";
                }
                else
                {
                    queueManager.CurrentQueue.qNotified.Add(sid.ToString());
                    return "non";
                }
            }
            else
            {
                return "z"; //Program error. Invalid action.
            }
        }


        public bool addToWhitelist(string sid)
        {
            string gameID = convertToGameID(sid);
            bool duplicate = !Helper.fileContains(whitelistPath, gameID);
            if (File.Exists(whitelistPath))
            {
                using (StreamWriter sw = File.AppendText(whitelistPath))
                {
                    if (duplicate)
                    {
                        sw.WriteLine(gameID);
                        Console.WriteLine("SUCCESS :: " + gameID + " added to server whitelist.\n");
                        sw.Close();
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("DUPLICATE :: " + gameID + " already on the whitelist.\n");
                        sw.Close();
                        return false;
                    }
                }
            }
            else
            {
                Console.WriteLine("Whitelist file does not exist. Make sure the PUG server isn't broken...");
                return false;
            }
        }

        //args - string steamID.ToString() as STEAM_0:X:XXXXXXXXX
        //returns - string csgoID as STEAM_1:X:XXXXXXXXX
        public string convertToGameID(string sid)
        {
            return Regex.Replace(sid, "^STEAM_0", "STEAM_1");
        }

        //banType 0=return VAC 1=return game
        //returns true for banned
        public bool getBans(SteamID sid, int banType)
        {
            String banURL = "http://api.steampowered.com/ISteamUser/GetPlayerBans/v1/?key=0659FE2A85CAA34AC96AB382172F4BFF&steamids=" + sid.ConvertToUInt64() + "&format=xml";

            XmlDocument doc = new XmlDocument();
            doc.Load(banURL);
            XmlElement root = doc.DocumentElement;
            XmlNodeList nodes = root.SelectNodes("/response/players/player");

            string vacBan = "";
            string gameBan = "";

            foreach (XmlNode node in nodes)
            {
                vacBan = node["NumberOfVACBans"].InnerText;
                gameBan = node["NumberOfGameBans"].InnerText;
            }

            if (banType == 0)
            {
                if (vacBan == "0")
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                if (gameBan == "0")
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }


        public void OnChatMessage(SteamFriends.FriendMsgCallback callback)
        {
            if (callback.Message.Length > 1)
            {
                if (callback.Message.Remove(1) == "!" || callback.Message.Remove(1) == "/" || callback.Message.Remove(1) == ".")
                {
                    string command = callback.Message;
                    if (callback.Message.Contains(' '))
                    {
                        command = callback.Message.Remove(callback.Message.IndexOf(' '));
                    }

                    string[] args;
                    switch (command)
                    {
                        #region sendmessage
                        case "!send":
                        case "/send":
                        case ".send":
                            if (!isBotAdmin(callback.Sender))
                                return;
                            args = Helper.Seperate(2, ' ', callback.Message);
                            Console.WriteLine("!send " + args[1] + " " + args[2] + " command received. User: " + steamFriends.GetFriendPersonaName(callback.Sender));

                            if (args[0] == "-1")
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Command syntax: !send [friend] [callback.Message]");
                                return;
                            }

                            for (int i = 0; i < steamFriends.GetFriendCount(); i++)
                            {
                                SteamID friend = steamFriends.GetFriendByIndex(i);
                                if (steamFriends.GetFriendPersonaName(friend).ToLower().Contains(args[1].ToLower()))
                                {
                                    steamFriends.SendChatMessage(friend, EChatEntryType.ChatMsg, args[2]);
                                }
                            }
                            break;
                        #endregion
                        #region friend
                        case "!friend":
                        case "/friend":
                        case ".friend":
                            args = Helper.Seperate(1, ' ', callback.Message);


                            if (args[0] == "-1")
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Command syntax: !friend [Steam ID]");
                                return;
                            }

                            try
                            {
                                Console.WriteLine("!friend " + args[1] + " | " + steamFriends.GetFriendPersonaName(Convert.ToUInt64(args[1])) + " command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));
                                //check to see if the user is not an admin
                                if (!isBotAdmin(callback.Sender))
                                    return;

                                SteamID validSID = Convert.ToUInt64(args[1]); //set the SID = to the argument, we have to convert it to a UINT64, because it can't be a string
                                if (!validSID.IsValid)
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Invalid SteamID"); //no person exists with that SID
                                    break;
                                }
                                steamFriends.AddFriend(validSID.ConvertToUInt64()); //add the friend
                            }
                            catch (FormatException) //see the arg[1] couldn't be converted to a UINT64
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Invalid SteamID");
                            }
                            break;
                        #endregion
                        #region changename
                        case "!changename":
                        case "/changename":
                        case ".changename":
                            if (!isBotAdmin(callback.Sender))
                                return;
                            args = Helper.Seperate(1, ' ', callback.Message);
                            Console.WriteLine("!changename " + args[1] + " command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));
                            if (args[0] == "-1")
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Syntax: !changename [name]");
                                return;
                            }
                            steamFriends.SetPersonaName(args[1]);
                            break;
                        #endregion
                        #region help
                        case "!help":
                        case "/help":
                        case ".help":
                            Console.WriteLine("!help command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender) + "\n");
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Available Commands :: !hello, !id, !info, !owner, !(q)ueue [(i)nfo / (j)oin / (l)eave / (n)otify], and !version.");
                            break;
                        #endregion
                        #region id
                        case "!id":
                        case "/id":
                        case ".id":
                            Console.WriteLine("!id command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));

                            string steamID = callback.Sender.ToString();
                            string csgoID = convertToGameID(steamID);

                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Your SteamID is: " + steamID + ".");
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Your CS:GO ID is: " + csgoID + ".");

                            Console.WriteLine(steamFriends.GetFriendPersonaName(callback.Sender) + " :: Steam ID: " + steamID);
                            Console.WriteLine(steamFriends.GetFriendPersonaName(callback.Sender) + " :: CSGO ID: " + csgoID + "\n");
                            break;
                        #endregion
                        #region owner
                        case "!owner":
                        case "/owner":
                        case ".owner":
                            Console.WriteLine("!id command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "My owner is drop, let nobody tell you otherwise.");
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "His profile: http://steamcommunity.com/id/dropisbae/");
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "His SteamID: STEAM_0:0:39990");
                            break;
                        #endregion
                        #region rcon
                        case "!rcon":
                        case "/rcon":
                        case ".rcon":
                            Console.WriteLine("!rcon command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));
                            if (!isBotAdmin(callback.Sender))
                                return;
                            String apiURL = "http://kiirus.net/api.php/?key=2F6E713BD4BA889A21166251DEDE9&ip=0&rcon=q&cmd=q";

                            WebClient rcon = new WebClient();
                            string rconPW = rcon.DownloadString(apiURL);

                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[KIWI PUG] Join the server at steam://connect/208.43.245.195:27020/" + Helper.genQPass("208.43.245.195:27020"));

                            break;
                        #endregion
                        #region version
                        case "!version":
                        case "/version":
                        case ".version":
                            Console.WriteLine("!version command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender) + "\n");
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "KIWI Bot version: v" + version + " :|: Build " + build + " - " + buildDate);
                            break;
                        #endregion
                        #region info
                        case "!info":
                        case "/info":
                        case ".info":
                            Console.WriteLine("!info command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender) + "\n");
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "ABOUT ME :: I'm the KIWI Bot. I automate various systems for the KIWI network, most notably our PUG system. Additionally, I control access to the network through user verification and whitelisting.");
                            break;
                        #endregion
                        #region server
                        /*case "!server":
                        case "/server":
                        case ".server":
                            Console.WriteLine("!server command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender) + "\n");
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "ID: 1 :|: Name: PUG #1 :|: IP: 208.43.245.195:27015 :|: steam://connect/208.43.245.195:27015");
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "ID: 2 :|: Name: PUG #2 :|: IP: 208.43.245.195:27016 :|: steam://connect/208.43.245.195:27016");
                            break;*/
                        #endregion
                        #region qserver
                        case "!qserver":
                        case "/qserver":
                        case ".qserver":
                            Console.WriteLine("!qserver command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender) + "\n");
                            if (!isBotAdmin(callback.Sender))
                                return;
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "╔═══════════════════════════════╗");
                            for (int s = 0; s < queueManager.qServers.Count; s++)
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "║  SRV  :|:  " + queueManager.qServers[s] + "    ║");
                            }
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "╚═══════════════════════════════╝");
                            break;
                        #endregion
                        #region qplayers
                        case "!qplayers":
                        case "/qplayers":
                        case ".qplayers":
                            Console.WriteLine("!qplayers command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender) + "\n");
                            if (!isBotAdmin(callback.Sender))
                                return;

                            string qPlayerNames = "";
                            //get the players in the current queue, if a queue has been created
                            if (queueManager.CurrentQueue != null)
                            {
                                for (int i = 0; i < queueManager.CurrentQueue.qPlayers.Count; i++)
                                {
                                    SteamID qPlayerID = new SteamID(queueManager.CurrentQueue.qPlayers[i]);
                                    if (i == (queueManager.CurrentQueue.qPlayers.Count - 1))
                                    {
                                        qPlayerNames += steamFriends.GetFriendPersonaName(qPlayerID);
                                    }
                                    else
                                    {
                                        qPlayerNames += steamFriends.GetFriendPersonaName(qPlayerID) + " ::: ";
                                    }

                                }
                            }
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Players in queue ::: " + qPlayerNames);
                            break;
                        #endregion
                        #region reloadconfig
                        case "!reloadconfig":
                        case "/reloadconfig":
                        case ".reloadconfig":
                            if (!isBotAdmin(callback.Sender))
                                return;
                            Helper.reloadConfig(config);
                            break;
                        #endregion
                        #region banned
                        /*case "!banned":
                        case "/banned":
                        case ".banned":
                            Console.WriteLine("!banned command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));

                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Hold on while I check your VAC and game bans.");

                            String banURL = "http://api.steampowered.com/ISteamUser/GetPlayerBans/v1/?key=0659FE2A85CAA34AC96AB382172F4BFF&steamids=" + callback.Sender.ConvertToUInt64() + "&format=xml";

                            XmlDocument doc = new XmlDocument();
                            doc.Load(banURL);
                            XmlElement root = doc.DocumentElement;
                            XmlNodeList nodes = root.SelectNodes("/response/players/player");

                            string vacBan = "";
                            string gameBan = "";

                            foreach (XmlNode node in nodes)
                            {
                                vacBan = node["NumberOfVACBans"].InnerText;
                                gameBan = node["NumberOfGameBans"].InnerText;
                            }

                            Console.WriteLine(callback.Sender + " :: VAC Bans: " + vacBan + " :: Game Bans: " + gameBan + "\n");

                            if (vacBan.ToString() == "0" && gameBan.ToString() == "0")
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Your Steam account currently has 0 VAC bans and 0 game bans, so you are ALLOWED to play on the KIWI PUG network.");
                            }
                            else if (vacBan.ToString() != "0" && gameBan.ToString() == "0")
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Your Steam account currently has 1 or more VAC bans and 0 game bans, so you are NOT ALLOWED to play on the KIWI PUG network.");
                            }
                            else if (vacBan.ToString() == "0" && gameBan.ToString() != "0")
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Your Steam account currently has 0 VAC bans and 1 or more game bans, so you are NOT ALLOWED to play on the KIWI PUG network.");
                            }
                            else if (vacBan.ToString() != "0" && gameBan.ToString() != "0")
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Your Steam account currently has 1 or more VAC bans and 1 or more game bans, so you are NOT ALLOWED to play on the KIWI PUG network.");
                            }
                            else
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "An error has occured while getting your BAN status. Contact my creator with the code: 0x711");
                            }
                            break;*/
                        #endregion
                        #region hours
                        /*case "!hours":
                        case "/hours":
                        case ".hours":
                            Console.WriteLine("!hours command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));

                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Hold on while I check your CS:GO hours.");

                            String hoursURL = "http://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v0001/?key=0659FE2A85CAA34AC96AB382172F4BFF&steamid=" + callback.Sender.ConvertToUInt64() + "&format=xml";

                            XmlDocument doc2 = new XmlDocument();
                            doc2.Load(hoursURL);

                            if (doc2.ChildNodes.Count == 0)
                            {
                                Console.WriteLine("PRIVATE PROFILE! User: " + steamFriends.GetFriendPersonaName(callback.Sender));
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Your account is private. You're NOT ALLOWED to play on the KIWI PUG network.");
                            }
                            else
                            {
                                XmlElement root2 = doc2.DocumentElement;
                                XmlNodeList nodes2 = root2.SelectNodes("/response/games/message");

                                int minutes = 0;
                                int id = 0;
                                bool gotCSGO = false;

                                foreach (XmlNode node in nodes2)
                                {
                                    string appid = node["appid"].InnerText;
                                    string playtime = node["playtime_forever"].InnerText;

                                    if (appid == "730")
                                    {
                                        minutes = Int32.Parse(playtime);
                                        id = Int32.Parse(appid);
                                        gotCSGO = true;
                                    }
                                }

                                int hours = minutes / 60;
                                Console.WriteLine(callback.Sender + ":: CS:GO Hours: " + hours + "\n");

                                if (gotCSGO)
                                {
                                    if (hours >= 750)
                                    {
                                        steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "With " + hours + " hours, you're ALLOWED to play on the KIWI PUG network.");
                                    }
                                    else
                                    {
                                        steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "With " + hours + " hours, you're NOT ALLOWED to play on the KIWI PUG network.");
                                    }
                                }
                                else
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "You either don't own CS:GO or you haven't played it recently. Purchase the game or start it up before trying this command again.");
                                }
                            }
                            break;*/
                        #endregion
                        #region verify
                        /*case "!verify":
                        case "/verify":
                        case ".verify":
                            Console.WriteLine("!verify command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));

                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Please wait while your account is verified.");

                            int accHours = getCSGOHours(callback.Sender.ConvertToUInt64());
                            bool vacBanned = getBans(callback.Sender.ConvertToUInt64(), 0);
                            bool gameBanned = getBans(callback.Sender.ConvertToUInt64(), 1);

                            if (accHours >= 750 && !vacBanned && !gameBanned)
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Congratulations! Your account has passed all checks.");
                                if (addToWhitelist(callback.Sender.ToString()))
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Please allow up to 5 minutes for your steamID to be added to the whitelist. GLHF.");
                                }
                                else
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Either you're already on the whitelist or there was a server error. Try again in a few minutes.");
                                }
                            }
                            else if (vacBanned)
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Your account has at least 1 active VAC ban. You're NOT ALLOWED to play on the KIWI PUG network.");
                            }
                            else if (gameBanned)
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Your account has at least 1 active overwatch ban. You're NOT ALLOWED to play on the KIWI PUG network.");
                            }
                            else if (accHours < 400)
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "You don't have at least 750 hours in CS:GO. You're NOT ALLOWED to play on the KIWI PUG network.");
                            }
                            else if (accHours == -2)
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "You currently have a private profile. You're NOT ALLOWED to play on the KIWI PUG network.");
                            }
                            else if (accHours == -1)
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "You either don't own CS:GO or you haven't played it recently. Start up the game and try verifying again. You're NOT ALLOWED to play on the KIWI PUG network.");
                            }
                            else
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "An unknown error has occured. Contact an admin and please try verifying again later on.");
                            }
                            break;*/
                        #endregion
                        #region add
                        case "!add":
                        case "/add":
                        case ".add":
                            if (isBotAdmin(callback.Sender))
                            {
                                args = Helper.Seperate(1, ' ', callback.Message);

                                if (args[0] == "-1")
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Command syntax: !add [CSGO ID]");
                                    return;
                                }

                                Console.WriteLine("!add " + args[1] + " command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));
                                string addID = args[1];

                                bool success = addToWhitelist(addID);

                                if (success)
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Success! Added id: " + addID + " to the whitelist");
                                }
                                else
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "The id given is already on the list.");
                                }
                            }
                            break;
                        #endregion
                        #region broadcast
                        case "!broadcast":
                        case "/broadcast":
                        case ".broadcast":
                        case "!bc":
                        case "/bc":
                        case ".bc":
                            if (isBotAdmin(callback.Sender))
                            {
                                args = Helper.Seperate(1, ' ', callback.Message);

                                if (args[0] == "-1")
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Command syntax: !broadcast [message]");
                                    return;
                                }

                                Console.WriteLine("!broadcast " + args[1] + " command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));
                                string message = args[1];

                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Sending the broadcast message to everyone.");

                                int sendCount = steamFriends.GetFriendCount();

                                Console.WriteLine(sendCount + " friends loaded. Preparing to send...");

                                for (int i = 0; i < sendCount; i++)
                                {
                                    steamFriends.SendChatMessage(steamFriends.GetFriendByIndex(i), EChatEntryType.ChatMsg, "[BROADCAST] " + message);
                                    Console.WriteLine("Sending broadcast to: " + steamFriends.GetFriendByIndex(i).ToString());

                                    if (i == sendCount - 1)
                                    {
                                        Console.WriteLine("\nBroadcast has finished.\n");
                                    }
                                }
                            }
                            break;
                        #endregion
                        #region need
                        /*case "!need":
                        case "/need":
                        case ".need":
                            args = Seperate(2, ' ', callback.Message);

                            if (args[0] == "-1")
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Command syntax: !need [server IP:port] [server password]");
                                return;
                            }

                            Console.WriteLine("!need " + args[1] + args[2] + " command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));

                            string serverIP = args[1];
                            string serverPass = args[2];

                            String apiHost = "http://kiirus.net/api.php/?key=2F6E713BD4BA889A21166251DEDE9&ip=" + args[2] + "&cmd=host";
                            String apiPlayers = "http://kiirus.net/api.php/?key=2F6E713BD4BA889A21166251DEDE9&ip=" + args[2] + "&cmd=players";
                            WebClient client = new WebClient();

                            string hostname = client.DownloadString(apiHost);
                            int players = Int32.Parse(client.DownloadString(apiPlayers));

                            playersNeeded = (10 - players);

                            if (serverIP == "208.43.245.195:27020" || serverIP == "208.43.245.195:27021" || serverIP == "208.43.245.195:27022" || serverIP == "208.43.245.195:27023" || serverIP == "208.43.245.195:27024")
                            {
                                if (playersNeeded <= 3)
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] Sending a broadcast. Trying for " + playersNeeded + " players.");

                                    int sendCount = steamFriends.GetFriendCount();
                                    Console.WriteLine(sendCount + " friends loaded. Preparing to send...");

                                    for (int i = 0; i < sendCount; i++)
                                    {
                                        steamFriends.SendChatMessage(steamFriends.GetFriendByIndex(i), EChatEntryType.ChatMsg, "[PUG] A PUG needs " + playersNeeded + " players! Join at: steam://connect/" + serverIP);
                                        steamFriends.SendChatMessage(steamFriends.GetFriendByIndex(i), EChatEntryType.ChatMsg, "[PUG] When prompted, TYPE OUT this password: " + serverPass);
                                        Console.WriteLine("Sending broadcast to: " + steamFriends.GetFriendByIndex(i).ToString());

                                        if (i == sendCount - 1)
                                        {
                                            Console.WriteLine("\nBroadcast has finished.\n"); 
                                        }
                                    }
                                }
                                else
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "You must have at least 7 players connected before using the !need command.");
                                }
                            }
                            else
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Invalid server IP given.");
                            }
                            break;*/
                        #endregion
                        #region hello
                        case "!hello":
                        case "/hello":
                        case ".hello":
                            Console.WriteLine("User: " + steamFriends.GetFriendPersonaName(callback.Sender) + " said hello! \n");
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Hi there!");
                            break;
                        #endregion
                        #region yo
                        case "!yo":
                        case "/yo":
                        case ".yo":
                            Console.WriteLine("User: " + steamFriends.GetFriendPersonaName(callback.Sender) + " said yo! \n");
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "What up homie?");
                            break;
                        #endregion
                        #region queue
                        case "!queue":
                        case "/queue":
                        case ".queue":
                        case "!q":
                        case "/q":
                        case ".q":
                            args = Helper.Seperate(1, ' ', callback.Message);

                            if (args[0] == "-1")
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] Command syntax: !(q)ueue [(i)nfo / (j)oin / (l)eave / (n)otify]");
                                return;
                            }

                            Console.WriteLine("!queue " + args[1] + " command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));
                            string cmd = args[1];

                            //if (!isBotAdmin(callback.Sender)) //COMMENT THIS BEFORE USAGE
                            //return;

                            GameID gamePlayed = steamFriends.GetFriendGamePlayed(callback.Sender);

                            if (cmd == "join" || cmd == "j")
                            {
                                if (queueManager.CurrentQueue.qEnabled)
                                {
                                    //if (gamePlayed.ToString() == "730")
                                    //{
                                    string response = qAction(callback.Sender, 0);
                                    if (response == "j")
                                    {
                                        steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] Type \"!queue info\" for queue status.");
                                    }
                                    else if (response == "uj")
                                    {
                                        steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] Please try again in a few moments.");
                                    }
                                    //}
                                    //else
                                    //{
                                    // steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] You must be currently playing CS:GO to use this command.");
                                    //}
                                }
                                else
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] The queue has been temporarily disabled.");
                                }
                            }
                            else if (cmd == "leave" || cmd == "l")
                            {
                                if (queueManager.CurrentQueue.qEnabled)
                                {
                                    string response = qAction(callback.Sender, 1);
                                    if (response == "l")
                                    {
                                        steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] Thank you for using the KIWI PUG queue system.");
                                    }
                                    else if (response == "ul")
                                    {
                                        steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] Please try again in a few moments.");
                                    }
                                }
                                else
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] The queue has been temporarily disabled.");
                                }
                            }
                            else if (cmd == "info" || cmd == "i")
                            {

                                if (queueManager.CurrentQueue.qEnabled)
                                {
                                    string response = qAction(callback.Sender, 2);
                                    string grammar = "players";
                                    if (response == "1")
                                    {
                                        grammar = "player";
                                    }
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG]  Currently " + response + " " + grammar + " in the queue. Waiting for 10.");
                                }
                                else
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] The queue has been temporarily disabled.");
                                }
                            }
                            else if (cmd == "notify" || cmd == "n")
                            {
                                string response = qAction(callback.Sender, 3);
                                if (response == "non")
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] You have enabled queue join notifications.");
                                }
                                else if (response == "noff")
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] You have disabled queue join notifications.");
                                }
                                else
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] System error. Try again in a few minutes.");
                                }
                            }
                            else if (cmd == "purge" || cmd == "p")
                            {
                                if (!isBotAdmin(callback.Sender))
                                    return;

                                queueManager.CurrentQueue.qPlayers.Clear();
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG]  Purged the queue entirely. Removed " + queueManager.CurrentQueue.numQ + " from the queue");

                            }
                            else if (cmd == "disable" || cmd == "d")
                            {
                                if (!isBotAdmin(callback.Sender))
                                    return;

                                queueManager.CurrentQueue.qPlayers.Clear();
                                queueManager.CurrentQueue.qEnabled = false;
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG]  Disabled the queueing system. Removed " + queueManager.CurrentQueue.numQ + " from the queue");

                            }
                            else if (cmd == "enable" || cmd == "e")
                            {
                                if (!isBotAdmin(callback.Sender))
                                    return;

                                queueManager.CurrentQueue.qEnabled = true;
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG]  Enabled the queueing system.");
                            }
                            else
                            {
                                if (queueManager.CurrentQueue.qEnabled)
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] Command syntax: !(q)ueue [(i)nfo / (j)oin / (l)eave / (n)otify]");
                                }
                                else
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] The queue has been temporarily disabled.");
                                }

                            }

                            //might not be necessary anymore? I dont know the logic here
                            //if (!availableServer && currentActiveQueue.qEnabled)
                            //{
                            //    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] >>> All PUG servers are currently full! You will remain in queue until a server opens.");
                            //}

                            break;
                        #endregion
                        #region report
                        case "!report":
                        case "/report":
                        case ".report":
                            args = Helper.Seperate(1, ' ', callback.Message);

                            if (args[0] == "-1")
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] The reporting system is coming soon!");
                                return;
                            }

                            Console.WriteLine("!report command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender) + "\n");
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] The reporting system is coming soon!");
                            break;
                        #endregion
                        #region stats
                        case "!stats":
                        case "/stats":
                        case ".stats":
                            args = Helper.Seperate(1, ' ', callback.Message);

                            if (args[0] == "-1")
                            {
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] The stats system is coming soon!");
                                return;
                            }

                            Console.WriteLine("!stats command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender) + "\n");
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "[PUG] The stats system is coming soon!");
                            break;
                        #endregion
                        #region default
                        default:
                            steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Unknown command! For a list of available commands, type !help");
                            break;
                        #endregion
                    }
                    return;
                }
            }
            #region chatty
            if (!config.Chatty)
                return;

            string rLine;
            string trimmed = callback.Message;
            char[] trim = { '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '_', '=', '+', '[', ']', '{', '}', '\\', '|', ';', ':', '"', '\'', ',', '<', '.', '>', '/', '?' };

            StreamReader sReader = new StreamReader("chat.txt");
            for (int i = 0; i < 30; i++)
            {
                trimmed = trimmed.Replace(trim[i].ToString(), "");
            }
            while ((rLine = sReader.ReadLine()) != null)
            {

                string text = rLine.Remove(rLine.IndexOf('|') - 1);
                string response = rLine.Remove(0, rLine.IndexOf('|') + 2);
                if (callback.Message.Contains(text))
                {
                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, response);

                    sReader.Close();
                    return;
                }
            }
            #endregion
        }
    }
}
