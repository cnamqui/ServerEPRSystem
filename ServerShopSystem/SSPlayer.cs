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
    class SSPlayer
    {
        public int Index { get; set; }
        public TSPlayer Player { get { return TShock.Players[Index]; } }
        public bool PBREnabled { get; set; }
        public double PrivateBuyRate { get; set; }

        public SSPlayer(int index)
        {
            Index = index;
            PBREnabled = false;
            PrivateBuyRate = 1;
        }

    }
}
