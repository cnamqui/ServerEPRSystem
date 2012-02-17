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

namespace C3RewardSystem
{
    public class CEPlayer
    {
        internal int ID;
        internal int Bet;
        internal int DuelReward;
        internal int challenged;

        //public CEPlayer(int ply)
        //{
        //    ID = ply;
        //    Bet = 0;
        //}
    }
}
