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
using System.Reflection;
namespace ServerPointSystem
{
    public class Attacker
    {
        public EPRPlayer attacker { get; set; }
        public double DamageDealtPct { get; set; }
        public Attacker(EPRPlayer player, double dmg)
        {
            attacker = player;
            DamageDealtPct = dmg;
        }
    }
    public class ENPC
    {
        public int Index { get; set; }
        public NPC MNPC { get { return Main.npc[Index]; } }
        public List<Attacker> Attackers { get; set; }

        public ENPC(int index)
        {
            Index = index;
            Attackers = new List<Attacker>();
        }
    }
}
