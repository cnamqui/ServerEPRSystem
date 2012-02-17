using System;
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
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace C3RewardSystem
{
    public class CEConfigFile
    {
        public int PointRange = 10;
        public int PvPKillReward = 100;
        public int TDMReward = 100;
        public int CTFReward = 100;
        public int OFReward = 100;
        public int MoE = 100;

        public static CEConfigFile Read(string path)
        {
            if (!File.Exists(path))
                return new CEConfigFile();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(fs);
            }
        }

        public static CEConfigFile Read(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                var cf = JsonConvert.DeserializeObject<CEConfigFile>(sr.ReadToEnd());
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

        public static Action<CEConfigFile> ConfigRead;
    }
}
