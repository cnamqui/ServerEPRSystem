using System;
//using System.Collections.Generic;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;
using MySql.Data.MySqlClient;
using System.IO;

namespace ServerPointSystem
{
    public class EPRPlayer
    {
        internal int Index { get; set; }
        public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }
        public bool ReaperBless { get; set; }
        public bool LadyLuck { get; set; }
        internal string Username { get; set; }
        public bool Notify { get; set; }
        internal int Account { get; set; }
        public int DisplayAccount { get { return Account; } }
        internal bool AccountEnable { get; set; }
        public EPRPlayer(int index)
        {
            Index = index;
            ReaperBless= false;
            LadyLuck = false;
            Username = "";
            Account = 0;
            AccountEnable = false;
            Notify = true;
        }
    }
}
