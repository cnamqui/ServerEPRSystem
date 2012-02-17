using System;
using System.Collections.Generic;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;
using MySql.Data.MySqlClient;
//using System.Drawing;
using System.IO;

namespace ServerPointSystem
{
    public class TimeRewardPlayer
    {
        public int Index;
        public DateTime LastNotify;
        public DateTime LastReward;
        public bool notify;
        public bool canclaim;

        public TimeRewardPlayer(int who)
        {
            Index = who;
            LastNotify = DateTime.Now;
            LastReward = DateTime.Now;
            notify = true;
            canclaim = false;
        }
    }
}
