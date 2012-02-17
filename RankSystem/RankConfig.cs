using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RankSystem
{
    public class RankConfigFile
    {

        public string[] RankLines =
#region
        {
            "Registered",
            "Donator"
        };
#endregion
        public bool[] RankLineRestrictons =
#region
        {
            false,
            true
        };
#endregion
        public int[][] RankUpCost =
#region
        { 
            new int[]
            {
                2000, 
                5000, 
                10000, 
                25000, 
                50000
            },
            new int[]
            {
                1600, 
                4000, 
                8000, 
                20000, 
                40000
            },
        };
#endregion
        public string[][] Ranks = 
#region
        {
            new string[]
            {
                "Amethyst", 
                "Topaz", 
                "Emerald", 
                "Ruby", 
                "Sapphire", 
                "Diamond"
            },
            new string[]
            {
                "DarknessSigil", 
                "EarthSigil", 
                "LifeSigil", 
                "FireSigil", 
                "WaterSigil", 
                "LightSigil"
            },
        };
#endregion
        public string[][] RankUpMessage =
#region
        {
            new string[]
            {
                "Congratulations! You are now ranked Topaz", 
                "Congratulations! You are now ranked Emerald", 
                "Congratulations! You are now ranked Ruby", 
                "Congratulations! You are now ranked Sapphire", 
                "Congratulations! You are now ranked Diamond"
            },
            new string[]
            {
                "Congratulations! You are now have the EarthSigil rank", 
                "Congratulations! You are now have the LifeSigil rank", 
                "Congratulations! You are now have the FireSigil rank", 
                "Congratulations! You are now have the WaterSigil rank", 
                "Congratulations! You are now have the LightSigil rank"
            }
        };
#endregion
        public string[][] RankCheckMessage =
#region
        {
            new string[]
            {
                "Your current rank is Amethyst, you may move up to rank Topaz upon acquiring 2000 Shards", 
                "Your current rank is Topaz, you may move up to rank Emerald upon acquiring 5000 Shards", 
                "Your current rank is Emerald, you may move up to rank Ruby upon acquiring 10000 Shards", 
                "Your current rank is Ruby, you may move up to rank Sapphire upon acquiring 25000 Shards", 
                "Your current rank is Sapphire, you may move up to rank Diamond upon acquiring 50000 Shards",
                "Your current rank is Diamond, you are currently unable to rank up"
            },
            new string[]
            {
                "Your currently have the DarkSigil rank, you may move up to the EarthSigil rank upon acquiring 1600 Shards", 
                "Your currently have the EarthSigil rank, you may move up to the LifeSigil rank upon acquiring 4000 Shards",
                "Your currently have the LifeSigil rank, you may move up to the FireSigil rank upon acquiring 8000 Shards",
                "Your currently have the FireSigil rank, you may move up to the WaterSigil rank upon acquiring 20000 Shards",
                "Your currently have the WaterSigil rank, you may move up to the LightSigil rank upon acquiring 40000 Shards",
                "Your currently have the LightSigil rank, you may not rank up anymore",
            }

        };
#endregion
        public string[][] RankPermissions =
#region
        {
            new string[]
            {
                "canbuild,warp,canwater,pouch", 
                "canbuild,warp,canwater,pouch",
                "canbuild,warp,canwater,pouch",
                "canbuild,warp,canwater,pouch",
                "canbuild,warp,canwater,pouch",
                "canbuild,warp,canwater,pouch",
            },
            new string[]
            {
                "canbuild,warp,canwater,pouch", 
                "canbuild,warp,canwater,pouch",
                "canbuild,warp,canwater,pouch",
                "canbuild,warp,canwater,pouch",
                "canbuild,warp,canwater,pouch",
                "canbuild,warp,canwater,pouch",
            },
        };
#endregion
        public static RankConfigFile Read(string path)
        {
            if (!File.Exists(path))
                return new RankConfigFile();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(fs);
            }
        }

        public static RankConfigFile Read(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                var cf = JsonConvert.DeserializeObject<RankConfigFile>(sr.ReadToEnd());
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

        public static Action<RankConfigFile> ConfigRead;
    }
}