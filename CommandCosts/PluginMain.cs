using System;
using System.Collections.Generic;
using System.Reflection;
//using System.Drawing;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;
using ServerPointSystem;
using MySql.Data.MySqlClient;
using System.IO;
using System.Text;

namespace CommandCosts
{
    [APIVersion(1, 10)]
    public class CommandCosts : TerrariaPlugin
    {
        private static SqlTableEditor SQLEditor;
        private static SqlTableCreator SQLWriter;
        private static int warpcost = 100;
        private static int tpcost = 100;
        private static int healcost = 100;
        private static int buffcost = 100;
        public override string Name
        {
            get { return "CommandCosts"; }
        }
        public override string Author
        {
            get { return "Created by Vharonftw"; }
        }
        public override string Description
        {
            get { return ""; }
        }
        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public override void Initialize()
        {
            GameHooks.Update += OnUpdate;
            GameHooks.Initialize += OnInitialize;
            NetHooks.GreetPlayer += OnGreetPlayer;
            ServerHooks.Leave += OnLeave;
            ServerHooks.Chat += OnChat;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Update -= OnUpdate;
                GameHooks.Initialize -= OnInitialize;
                NetHooks.GreetPlayer -= OnGreetPlayer;
                ServerHooks.Leave -= OnLeave;
                ServerHooks.Chat -= OnChat;
            }
            base.Dispose(disposing);
        }
        public CommandCosts(Main game)
            : base(game)
        {
            Order = -1;
        }

        public void OnInitialize()
        {
            SQLEditor = new SqlTableEditor(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            SQLWriter = new SqlTableCreator(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            bool setcost = false;
            bool freewarp = false;
            bool freetp = false;
            bool freeheal = false;
            bool freebuff = false;
            bool checkcommandcost = false;

            foreach (Group group in TShock.Groups.groups)
            {
                if (group.Name != "superadmin")
                {
                    if (group.HasPermission("setcost"))
                        setcost = true;
                    if (group.HasPermission("freewarp"))
                        freewarp = true;
                    if (group.HasPermission("freetp"))
                        freetp = true;
                    if (group.HasPermission("freeheal"))
                        freeheal = true;
                    if (group.HasPermission("freebuff"))
                        setcost = true;
                    if (group.HasPermission("checkcommandcost"))
                        checkcommandcost = true;
                }
            }

            List<string> permlist = new List<string>();
            if (!setcost)
                permlist.Add("setcost");
            if (!freewarp)
                permlist.Add("freewarp");
            if (!freetp)
                permlist.Add("freetp");
            if (!freeheal)
                permlist.Add("freeheal");
            if (!freebuff)
                permlist.Add("freebuff");
            if (!checkcommandcost)
                permlist.Add("checkcommandcost");
            TShock.Groups.AddPermissions("trustedadmin", permlist);

            Commands.ChatCommands.Add(new Command("setcost", SetCost, "setcost"));
            Commands.ChatCommands.Add(new Command("setcost", SetWarpCost, "setwarpcost"));
            Commands.ChatCommands.Add(new Command("checkcommandcost", CheckCommandCost, "checkcommandcost"));
            Commands.ChatCommands.Add(new Command("checkcommandcost", CheckWarpCost, "checkwarpcost"));
            var table = new SqlTable("CommandCosts",
                new SqlColumn("Command", MySqlDbType.String, 255) { Unique = true },
                new SqlColumn("Cost", MySqlDbType.Int32)
            );
            SQLWriter.EnsureExists(table);
            if (SQLEditor.ReadColumn("CommandCosts", "Command", new List<SqlValue>()).Count == 0)
            {
                List<SqlValue> tp = new List<SqlValue>();
                tp.Add(new SqlValue("Command", "'" + "tp" + "'"));
                tp.Add(new SqlValue("Cost", 100));
                SQLEditor.InsertValues("CommandCosts", tp);
                List<SqlValue> heal = new List<SqlValue>();
                heal.Add(new SqlValue("Command", "'" + "heal" + "'"));
                heal.Add(new SqlValue("Cost", 100));
                SQLEditor.InsertValues("CommandCosts", heal);
                List<SqlValue> buff = new List<SqlValue>();
                buff.Add(new SqlValue("Command", "'" + "buff" + "'"));
                buff.Add(new SqlValue("Cost", 100));
                SQLEditor.InsertValues("CommandCosts", buff);
                List<SqlValue> warp = new List<SqlValue>();
                warp.Add(new SqlValue("Command", "'" + "warp" + "'"));
                warp.Add(new SqlValue("Cost", 100));
                SQLEditor.InsertValues("CommandCosts", warp);
            }
            List<SqlValue> where1 = new List<SqlValue>();
            where1.Add(new SqlValue("Command", "'" + "warp" + "'"));
            warpcost = Int32.Parse(SQLEditor.ReadColumn("CommandCosts", "Cost", where1)[0].ToString());
            List<SqlValue> where2 = new List<SqlValue>();
            where2.Add(new SqlValue("Command", "'" + "tp" + "'"));
            tpcost = Int32.Parse(SQLEditor.ReadColumn("CommandCosts", "Cost", where1)[0].ToString());
            List<SqlValue> where3 = new List<SqlValue>();
            where3.Add(new SqlValue("Command", "'" + "heal" + "'"));
            healcost = Int32.Parse(SQLEditor.ReadColumn("CommandCosts", "Cost", where1)[0].ToString());
            List<SqlValue> where4 = new List<SqlValue>();
            where4.Add(new SqlValue("Command", "'" + "buff" + "'"));
            buffcost = Int32.Parse(SQLEditor.ReadColumn("CommandCosts", "Cost", where1)[0].ToString());
        }

        public void OnUpdate()
        {
        }

        public void OnGreetPlayer(int who, HandledEventArgs e)
        {
        }

        public void OnLeave(int ply)
        {
        }

        public void OnChat(messageBuffer msg, int ply, string text, HandledEventArgs e)
        {
            string cmd = text.Split(' ')[0];
            if (TShock.Players[ply].IsLoggedIn)
            {
                EPRPlayer player = ServerPointSystem.ServerPointSystem.GetEPRPlayerByIndex(ply);
                if (cmd == "/warp")
                {
                    if (text.Split(' ').Length > 1)
                    {
                        if (TShock.Warps.FindWarp(text.Split(' ')[1]).WarpPos != Vector2.Zero)
                        {

                            if (!TShock.Players[ply].Group.HasPermission("freewarp"))
                            {
                                
                                int warpcosttemp = warpcost;
                                int currbal = player.DisplayAccount; 
                                string warpname = text.Split(' ')[1];
                                List<SqlValue> where2 = new List<SqlValue>();
                                where2.Add(new SqlValue("Command", "'" + "warp " + warpname + "'"));
                                if (SQLEditor.ReadColumn("CommandCosts", "Cost", where2).Count == 1)
                                    warpcosttemp = Int32.Parse(SQLEditor.ReadColumn("CommandCosts", "Cost", where2)[0].ToString());
                                if (currbal < warpcosttemp)
                                {
                                    TShock.Players[ply].SendMessage("You do not have enough " + ServerPointSystem.ServerPointSystem.currname + "s!", Color.Red);
                                    TShock.Players[ply].SendMessage("You need " + warpcosttemp + " " + ServerPointSystem.ServerPointSystem.currname + "s to use that command", Color.Red);
                                    e.Handled = true;
                                    return;
                                }
                                else
                                {
                                    EPREvents.PointUse(player, warpcosttemp, PointUsage.Command);
                                    player.TSPlayer.SendMessage(string.Format("Warped to {0} for {1} {2}(s)", warpname, warpcosttemp, ServerPointSystem.ServerPointSystem.currname), Color.Yellow);
                                    return;
                                }

                            }
                            //else
                            //    return;
                        }
                        //else
                        //    return;
                    }
                    //else
                    return;
                }
                else if (cmd == "/heal")
                {
                    if (!TShock.Players[ply].Group.HasPermission("freeheal"))
                    {

                        int currbal = player.DisplayAccount;
                        if (currbal < healcost)
                        {
                            TShock.Players[ply].SendMessage("You do not have enough " + ServerPointSystem.ServerPointSystem.currname + "s!", Color.Red);
                            TShock.Players[ply].SendMessage("You need " + healcost + " " + ServerPointSystem.ServerPointSystem.currname + "s to use that command", Color.Red);
                            e.Handled = true;
                            return;
                        }
                        else
                        {
                            EPREvents.PointUse(player, healcost, PointUsage.Command);
                            return;
                        }
                    }
                }
                else if (cmd == "/tp")
                {
                    if (!TShock.Players[ply].Group.HasPermission("freetp"))
                    {

                        int currbal = player.DisplayAccount;
                        if (currbal < tpcost)
                        {
                            TShock.Players[ply].SendMessage("You do not have enough " + ServerPointSystem.ServerPointSystem.currname + "s!", Color.Red);
                            TShock.Players[ply].SendMessage("You need " + tpcost + " " + ServerPointSystem.ServerPointSystem.currname + "s to use that command", Color.Red);
                            e.Handled = true;
                            return;
                        }
                        else
                        {
                            EPREvents.PointUse(player, tpcost, PointUsage.Command);
                            return;
                        }
                    }
                }
                else if (cmd == "/buff")
                {
                    if (!TShock.Players[ply].Group.HasPermission("freebuff"))
                    {
                        int currbal = player.DisplayAccount;
                        if (currbal < buffcost)
                        {
                            TShock.Players[ply].SendMessage("You do not have enough " + ServerPointSystem.ServerPointSystem.currname + "s!", Color.Red);
                            TShock.Players[ply].SendMessage("You need " + buffcost + " " + ServerPointSystem.ServerPointSystem.currname + "s to use that command", Color.Red);
                            e.Handled = true;
                            return;
                        }
                        else
                        {
                            EPREvents.PointUse(player, buffcost, PointUsage.Command);
                        }
                    }
                }
            }
            return;
        }

        private static void SetCost(CommandArgs args)
        {
            if (args.Parameters.Count > 1)
            {
                string cmd = args.Parameters[0].ToLower();
                switch (cmd)
                {
                    case "buff":
                        {
                            Int32.TryParse(args.Parameters[1], out buffcost);
                            List<SqlValue> where = new List<SqlValue>();
                            List<SqlValue> list = new List<SqlValue>();
                            list.Add(new SqlValue("Cost", buffcost));
                            where.Add(new SqlValue("Command", "'" + "buff" + "'"));
                            SQLEditor.UpdateValues("CommandCosts", list, where);
                            TShockAPI.TShock.Utils.Broadcast("Using \"/buff\" now costs " + warpcost.ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "s", Color.Yellow);
                            break;
                        }
                    case "warp":
                        {
                            Int32.TryParse(args.Parameters[1], out warpcost);
                            List<SqlValue> where = new List<SqlValue>();
                            List<SqlValue> list = new List<SqlValue>();
                            list.Add(new SqlValue("Cost", warpcost));
                            where.Add(new SqlValue("Command", "'" + "warp" + "'"));
                            SQLEditor.UpdateValues("CommandCosts", list, where);
                            TShockAPI.TShock.Utils.Broadcast("Warping to un-priced warp points now costs " + warpcost.ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "s", Color.Yellow);
                            break;
                        }
                    case "heal":
                        {
                            Int32.TryParse(args.Parameters[1], out healcost);
                            List<SqlValue> where = new List<SqlValue>();
                            List<SqlValue> list = new List<SqlValue>();
                            list.Add(new SqlValue("Cost", healcost));
                            where.Add(new SqlValue("Command", "'" + "heal" + "'"));
                            SQLEditor.UpdateValues("CommandCosts", list, where);
                            TShockAPI.TShock.Utils.Broadcast("Using \"/heal\" now costs " + warpcost.ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "s", Color.Yellow);
                            break;
                        }
                    case "tp":
                        {
                            Int32.TryParse(args.Parameters[1], out tpcost);
                            List<SqlValue> where = new List<SqlValue>();
                            List<SqlValue> list = new List<SqlValue>();
                            list.Add(new SqlValue("Cost", tpcost));
                            where.Add(new SqlValue("Command", "'" + "tp" + "'"));
                            SQLEditor.UpdateValues("CommandCosts", list, where);
                            TShockAPI.TShock.Utils.Broadcast("Using \"/tp\" now costs " + warpcost.ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "s", Color.Yellow);
                            break;
                        }
                }
            }
            else
                args.Player.SendMessage("/setcost [buff|heal|warp|tp] [amount]", Color.Yellow);
        }
        private static void SetWarpCost(CommandArgs args)
        {
            if (args.Parameters.Count > 1)
            {
                if (TShock.Warps.FindWarp(args.Parameters[0]).WarpPos != Vector2.Zero)
                {
                    string warpname;
                    warpname = args.Parameters[0];
                    int cost = 100;
                    Int32.TryParse(args.Parameters[1], out cost);
                    List<SqlValue> list = new List<SqlValue>();
                    list.Add(new SqlValue("Command", "'" + "warp " + warpname + "'"));
                    if (SQLEditor.ReadColumn("CommandCosts", "Command", list).Count < 1)
                    {
                        list.Add(new SqlValue("Cost", cost));
                        SQLEditor.InsertValues("CommandCosts", list);
                    }
                    else
                    {
                        List<SqlValue> list2 = new List<SqlValue>();
                        list2.Add(new SqlValue("Cost", cost));
                        SQLEditor.UpdateValues("CommandCosts", list2, list);
                    }
                    args.Player.SendMessage("It now costs " + cost.ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "s to warp to " + warpname, Color.Green);
                }
                else
                    args.Player.SendMessage("You must enter a valid warp!", Color.Yellow);
            }
            else
                args.Player.SendMessage("/setwarpcost [warp name] [amount]", Color.Yellow);
        }
        private static void CheckWarpCost(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                if (TShock.Warps.FindWarp(args.Parameters[0]).WarpPos != Vector2.Zero)
                {
                    string warpname;
                    warpname = args.Parameters[0];
                    int warpcosttemp = warpcost;
                    List<SqlValue> where = new List<SqlValue>();
                    where.Add(new SqlValue("Command", "'" + "warp " + warpname + "'"));
                    if (SQLEditor.ReadColumn("CommandCosts", "Command", where).Count > 0)
                        Int32.TryParse(SQLEditor.ReadColumn("CommandCosts", "Cost", where)[0].ToString(), out warpcost);
                    args.Player.SendMessage("It costs " + warpcosttemp.ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "s to warp there", Color.Yellow);
                }
                else
                    args.Player.SendMessage("You must enter a valid warp!", Color.Yellow);
            }
            else
                args.Player.SendMessage("/checkwarpcost [warp name]", Color.Yellow);
        }
        private static void CheckCommandCost(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                string cmd = args.Parameters[0].ToLower();

                switch (cmd)
                {
                    case "buff":
                        args.Player.SendMessage("It costs " + buffcost.ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "s to use that command", Color.Yellow);
                        break;
                    case "heal":
                        args.Player.SendMessage("It costs " + healcost.ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "s to use that command", Color.Yellow);
                        break;
                    case "tp":
                        args.Player.SendMessage("It costs " + tpcost.ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "s to use that command", Color.Yellow);
                        break;
                    case "warp":
                        args.Player.SendMessage("It costs " + warpcost.ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "s to use that command", Color.Yellow);
                        break;
                    default:
                        args.Player.SendMessage("/checkcommandcost [buff|heal|warp|tp]", Color.Yellow);
                        break;
                }

            }
            else
                args.Player.SendMessage("/checkcommandcost [buff|heal|warp|tp]", Color.Yellow);
        }
    }
}