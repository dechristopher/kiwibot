using SteamKit2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace KiwiBotProgram
{
    class Helpers
    {

        Random random = new Random();
        readonly object syncLock = new object();

        public int RandomNumber(int min, int max)
        {

            lock (syncLock)
            {
                return random.Next(min, max);
            }
        }
             


        public string genQPass(string servIP)
        {
            string connectedURL = "http://kiirus.net/api.php/?key=2F6E713BD4BA889A21166251DEDE9&ip=" + servIP + "&rcon=q&cmd=q";

            WebClient client = new WebClient();
            string downloadString = client.DownloadString(connectedURL);

            return downloadString;
        }

        public bool fileContains(string path, string text)
        {
            int counter = 0;
            string line;

            System.IO.StreamReader file = new System.IO.StreamReader(path);

            while ((line = file.ReadLine()) != null)
            {
                if (line.Contains(text))
                {
                    file.Close();
                    return true;
                }
                counter++;
            }
            file.Close();
            return false;
        }

        

        public RootObject reloadConfig(RootObject config)
        {
            if (!File.Exists("config.cfg"))
            {
                StringBuilder sb = new StringBuilder();

                sb.Append("{\r\n");
                sb.Append("  \"Admins\":[76561198060315636],\r\n");
                sb.Append("  \"Chatty\": true,\r\n");
                sb.Append("  \"Changelog\": \"\"\r\n");
                sb.Append("}\r\n");
            }
            try
            {
                JavaScriptSerializer jss = new JavaScriptSerializer();
                config = jss.Deserialize<RootObject>(File.ReadAllText("config.cfg"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.ReadKey();
                reloadConfig(config);
            }
            return config;
        }

        public string[] Seperate(int number, char seperator, string thestring)
        {
            string[] returned = new string[4];
            int i = 0;
            int error = 0;
            int length = thestring.Length;

            foreach (char s in thestring)
            {
                if (i != number)
                {
                    if (error > length || number > 5)
                    {
                        returned[0] = "-1";
                        return returned;
                    }
                    else if (s == seperator)
                    {
                        returned[i] = thestring.Remove(thestring.IndexOf(s));
                        thestring = thestring.Remove(0, thestring.IndexOf(s) + 1);
                        i++;
                    }
                    error++;
                    if (error == length && i != number)
                    {
                        returned[0] = "-1";
                        return returned;
                    }
                }
                else
                {
                    returned[i] = thestring;
                }
            }
            return returned;
        }
    }
}
