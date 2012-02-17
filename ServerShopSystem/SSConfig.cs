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


namespace ServerShopSystem
{
    public class SSConfigFile
    {
        public string[] BuyRatePermissions =
        {
            "superadmin",
            "discount1",
            "discount2",
            "discount3"
        };
        public double[] BuyRates =
        {
            0,
            0.75,
            0.50,
            0.25
        };





        public static SSConfigFile Read(string path)
        {
            if (!File.Exists(path))
                return new SSConfigFile();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(fs);
            }
        }

        public static SSConfigFile Read(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                var cf = JsonConvert.DeserializeObject<SSConfigFile>(sr.ReadToEnd());
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

        public static Action<SSConfigFile> ConfigRead;
    }
}

