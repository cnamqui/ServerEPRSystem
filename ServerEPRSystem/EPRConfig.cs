using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;
using ServerPointSystem;
using MySql.Data.MySqlClient;
using System.Text;
using Newtonsoft.Json;

namespace ServerPointSystem
{
    public class EPRConfigFile
    {
        //Add public variables here (NOT STATIC)
        public int DeathToll = 90;
        public int DeathTollStatic = 100;
        public float PointMultiplier = 1;
        public string currname = "Shard";
        public double LadyLucksMultiplier = 1.5;
        public bool ReapersBlessingEnabled = false;
        public int TimeReward = 100;
        public bool EnableTimeRewards = true;
        public int RewardTime = 60;
        public int ClaimTime = 30;
        public bool EnablePointShare = true;
        public static EPRConfigFile Read(string path)
        {
            if (!File.Exists(path))
                return new EPRConfigFile();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(fs);
            }
        }

        public static EPRConfigFile Read(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                var cf = JsonConvert.DeserializeObject<EPRConfigFile>(sr.ReadToEnd());
                if (ConfigRead != null)
                    ConfigRead(cf);
                return cf;
            }
        }

        public void Write(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                Write(fs);
            }
        }

        public void Write(Stream stream)
        {
            var str = JsonConvert.SerializeObject(this, Formatting.Indented);
            using (var sw = new StreamWriter(stream))
            {
                sw.Write(str);
            }
        }

        public static Action<EPRConfigFile> ConfigRead;
    }
}
