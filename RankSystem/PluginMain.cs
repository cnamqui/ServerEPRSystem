using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;
using ServerPointSystem;
using MySql.Data.MySqlClient;
using System.IO;
using System.Text;

namespace RankSystem
{
    [APIVersion(1, 10)]
    public class RankSystem : TerrariaPlugin
    {
        private static RankConfigFile RankConfig { get; set; }
        internal static string RankConfigPath { get { return Path.Combine(TShock.SavePath, "rankconfig.json"); } }
        private static SqlTableEditor SQLEditor;
        private static SqlTableCreator SQLWriter;
        public override string Name
        {
            get { return "ServerRankingSystem"; }
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
        public RankSystem(Main game)
            : base(game)
        {
            RankConfig = new RankConfigFile();
        }

        public void OnInitialize()
        {
            SetupConfig();
            for (int i = 0; i < RankConfig.Ranks.Length; i++ )
            {
                for(int j= 0 ; j <RankConfig.Ranks[i].Length;j++)
                    if (!TShock.Groups.GroupExists(RankConfig.Ranks[i][j]))
                        TShock.Groups.AddGroup(RankConfig.Ranks[i][j], RankConfig.RankPermissions[i][j]);
            }
            Commands.ChatCommands.Add(new Command("pouch", Rank, "rank"));
            Commands.ChatCommands.Add(new Command("changerank", ChRank, "chrank"));
            SQLEditor = new SqlTableEditor(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            SQLWriter = new SqlTableCreator(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            bool dontchangemyrank = false;
            bool changerank = false;
            foreach (Group group in TShock.Groups.groups)
            {
                if (group.Name != "superadmin")
                {
                    if (group.HasPermission("dontchangemyrank"))
                        dontchangemyrank = true;
                    if (group.HasPermission("chrank"))
                        changerank = true;
                }
            }

            List<string> permlist = new List<string>();
            if (!dontchangemyrank)
                permlist.Add("dontchangemyrank");
            if (!changerank)
                permlist.Add("chrank");            
            TShock.Groups.AddPermissions("trustedadmin", permlist);
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

        }

        private static void ChRank(CommandArgs args)
        {
            if (args.Parameters.Count > 1)
            {
                TSPlayer player;
                if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count == 1)
                {
                    player = TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0])[0];
                    if (TShock.Groups.GroupExists(args.Parameters[1]))
                    {
                        Group Group = TShockAPI.TShock.Utils.GetGroup(args.Parameters[1]);
                        if (IsRank(Group.Name) && !Group.HasPermission("dontchangemyrank"))
                        {
                            List<SqlValue> list = new List<SqlValue>();
                            List<SqlValue> where = new List<SqlValue>();
                            where.Add(new SqlValue("Username", "'" + player.UserAccountName + "'"));
                            list.Add(new SqlValue("Usergroup", "'" + Group.Name + "'"));
                            SQLEditor.UpdateValues("Users", list, where);
                            args.Player.SendMessage("You changed " + player.Name + "'s rank from " + player.Group.Name + " to " + Group.Name, Color.Green);
                            player.SendMessage(args.Player.Name + "has changed your rank to " + Group.Name + "!", Color.Green);
                            player.Group = Group;
                        }
                        else
                            args.Player.SendMessage("You cannot change that players rank!", Color.Red);
                    }
                }
                else if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count > 1)
                    args.Player.SendMessage("More than 1 player matched!", Color.Red);
                else if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count ==0)
                    args.Player.SendMessage("A player by that name does not exist!", Color.Red);
            }
        }
        private static void Rank(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                if (args.Player.IsLoggedIn)
                {
                    string cmd = "help";
                    cmd = args.Parameters[0].ToLower();
                    switch (cmd)
                    {
                        case "change":
                            {
                                if (!args.Player.Group.HasPermission("dontrankme"))
                                {
                                    if (args.Parameters.Count > 1)
                                    {
                                        int RankLineID = GetRankLineIDByLineName(args.Parameters[1]);
                                        if (RankLineID >= 0)
                                        {
                                            if (!RankConfig.RankLineRestrictons[RankLineID])
                                            {
                                                Group Group = TShockAPI.TShock.Utils.GetGroup(RankConfig.Ranks[RankLineID][0]);
                                                List<SqlValue> list = new List<SqlValue>();
                                                List<SqlValue> where = new List<SqlValue>();
                                                where.Add(new SqlValue("Username", "'" + args.Player.UserAccountName + "'"));
                                                list.Add(new SqlValue("Usergroup", "'" + Group.Name + "'"));
                                                SQLEditor.UpdateValues("Users", list, where);
                                                args.Player.SendMessage("You changed rank from " + args.Player.Group.Name + " to " + Group.Name, Color.Green);
                                                args.Player.Group = Group;
                                            }
                                            else
                                                args.Player.SendMessage("That Rank Line is Restricted!", Color.Red);
                                        }
                                        else
                                            args.Player.SendMessage("There is no such rank line!", Color.Red);
                                    }
                                    else
                                    {
                                        args.Player.SendMessage("/rank change [Rank Line]", Color.Yellow);
                                        args.Player.SendMessage("Warning this will transfer you to the lowest rank in that Rank Line", Color.Yellow);
                                    }
                                }
                                else
                                    args.Player.SendMessage("You cannot change your Rank Line", Color.Red);

                                break;
                            }
                        case "check":
                            {
                                int RankLineID = GetRankLineIDByRank(args.Player.Group.Name);
                                int RankID = -1;
                                if(RankLineID >=0)
                                    RankID = GetRankID(args.Player.Group.Name, RankLineID);
                                if (RankID >= 0)
                                {
                                    args.Player.SendMessage(RankConfig.RankCheckMessage[RankLineID][RankID], Color.AntiqueWhite);
                                }
                                else
                                {
                                    args.Player.SendMessage("Your group: " + args.Player.Group.Name + " isn't part of the ranking system", Color.Red);
                                }
                                break;
                            }
                        case "up":
                            {
                                EPRPlayer player = ServerPointSystem.ServerPointSystem.GetEPRPlayerByIndex(args.Player.Index);
                                List<SqlValue> where2 = new List<SqlValue>();
                                where2.Add(new SqlValue("Username", "'" + args.Player.UserAccountName.ToString() + "'"));
                                int currbal = player.DisplayAccount;
                                int RankLineID = GetRankLineIDByRank(args.Player.Group.Name);
                                int RankID = -1;
                                if (RankLineID >= 0)
                                    RankID = GetRankID(args.Player.Group.Name, RankLineID);
                                if (RankID >= 0 && RankID < (RankConfig.Ranks[RankLineID].Length - 1))
                                {
                                    int RankUpCost = RankConfig.RankUpCost[RankLineID][RankID];
                                    if (currbal >= RankUpCost)
                                    {
                                        EPREvents.PointUse(player, RankUpCost, PointUsage.Rank);
                                        List<SqlValue> list2 = new List<SqlValue>();
                                        list2.Add(new SqlValue("Usergroup", "'" + RankConfig.Ranks[RankLineID][RankID + 1] + "'"));
                                        SQLEditor.UpdateValues("Users", list2, where2);
                                        args.Player.SendMessage(RankConfig.RankUpMessage[RankLineID][RankID], Color.AntiqueWhite);
                                        args.Player.Group = TShockAPI.TShock.Utils.GetGroup(RankConfig.Ranks[RankLineID][RankID + 1]);
                                    }
                                    else
                                    {
                                        args.Player.SendMessage(RankConfig.RankCheckMessage[RankLineID][RankID], Color.AntiqueWhite);
                                    }
                                }
                                else if (RankID >= 0 && RankID == (RankConfig.Ranks[RankLineID].Length - 1))
                                {
                                    args.Player.SendMessage(RankConfig.RankCheckMessage[RankLineID][RankID], Color.AntiqueWhite);
                                }
                                else
                                {
                                    args.Player.SendMessage("Your group: " + args.Player.Group.Name + " isn't part of the ranking system", Color.Red);
                                }
                                break;
                            }
                        default:
                            args.Player.SendMessage("Invalid Syntax! try:/rank up or /rank check or /rank change", Color.Yellow);
                            break;
                    }
                }
                else
                {
                    args.Player.SendMessage("You must be logged in to do that!", Color.Red);
                }

            }
            else
            {
                args.Player.SendMessage("Invalid Syntax! try:/rank up or /rank check or /rank change", Color.Yellow);
            }
        }
        private static void SetupConfig()
        {
            try
            {
                if (File.Exists(RankConfigPath))
                {
                    RankConfig = RankConfigFile.Read(RankConfigPath);
                    // Add all the missing config properties in the json file
                }
                RankConfig.Write(RankConfigPath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error in config file");
                Console.ForegroundColor = ConsoleColor.Gray;
                Log.Error("Config Exception");
                Log.Error(ex.ToString());
            }
        }
        private static int GetRankID(string rank, int LineID)
        {
            int ID = -1;
            for (int j = 0; j < RankConfig.Ranks[LineID].Length; j++)
                    if (RankConfig.Ranks[LineID][j] == rank)
                        ID = j;
            return ID;
        }
        private static int GetRankLineIDByRank(string rank)
        {
            int ID = -1;
            for (int i = 0; i < RankConfig.Ranks.Length; i++)
            {
                for (int j = 0; j < RankConfig.Ranks[i].Length; j++)
                    if (RankConfig.Ranks[i][j] == rank)
                        ID = i;
            }
            return ID;
        }
        private static int GetRankLineIDByLineName(string rankline)
        {
            int ID = -1;
            for (int i = 0; i < RankConfig.RankLines.Length ; i++)
            {
                if (rankline == RankConfig.RankLines[i])
                    ID = i;
            }
            return ID;
        }
        private static bool IsRank(string rank)
        {
            bool isrank = false;
            for (int i = 0; i < RankConfig.Ranks.Length; i++)
            {
                for (int j = 0; j < RankConfig.Ranks[i].Length; j++)
                    if (RankConfig.Ranks[i][j] == rank)
                        isrank = true;
            }
            return isrank;
        }
    }
}