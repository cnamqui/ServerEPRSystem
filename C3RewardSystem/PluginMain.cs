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
using C3Mod;

namespace C3RewardSystem
{
    [APIVersion(1, 11)]
    public class C3RewardSystem : TerrariaPlugin
    {
        private static CEConfigFile CEConfig { get; set; }
        private static string CEConfigPath { get { return Path.Combine(TShock.SavePath, "ceconfig.json"); } }
        private static int PointRange = 10;
        private static int PvPKillReward = 100;
        private static int TDMReward = 100;
        private static int CTFReward = 100;
        private static int OFReward = 100;
        private static int MoE = 100;
        private static SqlTableEditor SQLEditor;
        private static SqlTableCreator SQLWriter;
        private static List<CEPlayer> CEPlayers = new List<CEPlayer>();
        public override string Name
        {
            get { return "C3RewardSystem"; }
        }
        public override string Author
        {
            get { return "Created by Vharonftw"; }
        }
        public override string Description
        {
            get { return "PvP Reward System Using C3Mod and ServerEPRSystem"; }
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
            C3Mod.C3Events.OnPvPDeath += OnPvPdeath;
            C3Mod.C3Events.OnGameEnd += OnGameEnd;
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
        public C3RewardSystem(Main game)
            : base(game)
        {
            Order = -2;
            CEConfig = new CEConfigFile();
        }

        public void OnInitialize()
        {
            SQLEditor = new SqlTableEditor(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            SQLWriter = new SqlTableCreator(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            SetupConfig();
            PointRange = CEConfig.PointRange;
            PvPKillReward = CEConfig.PvPKillReward;
            TDMReward = CEConfig.TDMReward;
            CTFReward = CEConfig.CTFReward;
            OFReward = CEConfig.OFReward;
            MoE = CEConfig.MoE;
            Commands.ChatCommands.Add(new Command("duel", Bet, "bet"));
        }

        public void OnUpdate()
        {
        }

        public void OnGreetPlayer(int who, HandledEventArgs e)
        {
            CEPlayer player = new CEPlayer();
            player.ID = who;
            player.Bet = 0;
            player.challenged = -1;
            player.DuelReward = 0;
            CEPlayers.Add(player);
        }

        public void OnLeave(int ply)
        {
            for (int i = 0; i < CEPlayers.Count; i++)
            {
                if (CEPlayers[i].ID == ply)
                    CEPlayers.RemoveAt(i);
            }
        }

        public void OnChat(messageBuffer msg, int ply, string text, HandledEventArgs e)
        {
            string cmd = text.Split(' ')[0];
            if (cmd == "/duel" && TShockAPI.TShock.Utils.FindPlayer(text.Split(' ')[1]).Count == 1)
            {
                TSPlayer challenged = TShockAPI.TShock.Utils.FindPlayer(text.Split(' ')[1])[0];
                TSPlayer challenger = TShock.Players[ply];
                if (challenger.IsLoggedIn && challenged.IsLoggedIn)
                {
                    int challengerbal = ServerPointSystem.ServerPointSystem.GetEPRPlayerByIndex(challenger.Index).DisplayAccount;
                    if (challengerbal >= GetCEPlayer(ply).Bet)
                    {
                        GetCEPlayer(ply).challenged = challenged.Index;
                        challenger.SendMessage("You challenged " + challenged.Name + " to a duel for " + GetCEPlayer(ply).Bet.ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "s", Color.Teal);
                        challenger.SendMessage(challenger.Name + " has  challenged you" + challenged.Name + " to a duel for " + GetCEPlayer(ply).Bet.ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "s", Color.Teal);
                        foreach (CEPlayer ceplayerctr in WhoChallengedThisGuy(challenged.Index))
                        {
                            if (ceplayerctr.ID != ply)
                                GetCEPlayer(ceplayerctr.ID).challenged = -1;
                        }
                    }
                    else
                    {
                        e.Handled = true;
                        challenger.SendMessage("You don't have enough " + ServerPointSystem.ServerPointSystem.currname + "s to do that! Lower your bet", Color.Red);
                    }
                }
                return;
            }
            else if (cmd == "/accept")
            {
                if (WhoChallengedThisGuy(ply).Count > 0)
                {
                    TSPlayer challenger = TShock.Players[WhoChallengedThisGuy(ply)[0].ID];
                    TSPlayer challenged = TShock.Players[ply];
                    EPRPlayer Echallenger =ServerPointSystem.ServerPointSystem.GetEPRPlayerByIndex(challenger.Index);
                    EPRPlayer Echallenged =ServerPointSystem.ServerPointSystem.GetEPRPlayerByIndex(challenged.Index);
                    if (challenger.IsLoggedIn && challenged.IsLoggedIn)
                    {
                        int challengerbal = Echallenger.DisplayAccount;
                        int challengedbal = Echallenged.DisplayAccount;
                        int wager = GetCEPlayer(challenger.Index).Bet;
                        if (challengerbal >= wager && challengedbal >= wager)
                        {
                            GetCEPlayer(challenged.Index).DuelReward = wager;
                            GetCEPlayer(challenger.Index).DuelReward = wager;
                            EPREvents.PointUse(Echallenger, wager, PointUsage.Bet);
                            EPREvents.PointUse(Echallenged, wager, PointUsage.Bet);

                        }
                        else
                        {
                            challenger.SendMessage("Error processing dueling bets. Either you or your opponent does not have enough " + ServerPointSystem.ServerPointSystem.currname + "s", Color.Red);
                            challenged.SendMessage("Error processing dueling bets. Either you or your opponent does not have enough " + ServerPointSystem.ServerPointSystem.currname + "s", Color.Red);
                            e.Handled = true;
                        }
                    }
                }
                return;
            }
            return;
        }

        private static void Bet(CommandArgs args)
        {
            if (args.Player.IsLoggedIn)
            {
                if(args.Parameters.Count>0)
                {
                    int bet = 0;
                    Int32.TryParse(args.Parameters[0], out bet);
                    GetCEPlayer(args.Player.Index).Bet = bet;
                    args.Player.SendMessage("You wagered " + bet.ToString() + ServerPointSystem.ServerPointSystem.currname + "s", Color.Red);
                }
            }
            else
                args.Player.SendMessage("You must be logged in to do that!", Color.Red);
        }
        private static void OnPvPdeath(C3Mod.DeathArgs e)
        {
            if (e.PvPKill)
            {
                if( e.Killer.TSPlayer.IsLoggedIn && e.Killed.TSPlayer.IsLoggedIn)
                {
                    EPRPlayer EKiller = ServerPointSystem.ServerPointSystem.GetEPRPlayerByIndex(e.Killer.TSPlayer.Index);
                    EPRPlayer EKilled = ServerPointSystem.ServerPointSystem.GetEPRPlayerByIndex(e.Killed.TSPlayer.Index);
                    //The two ints here are the two account balances.
                    int killerbal = EKiller.DisplayAccount;
                    int killedbal = EKilled.DisplayAccount;
                    //If the two are within the proper range, it will add and deduct.
                    if(InPointRange(killerbal, killedbal))
                    {
                        //gain is one percent of the balance of the player killed multiplied by one hundred minus the deathtoll.
                        float gain = (killedbal * (100 - ServerPointSystem.ServerPointSystem.DeathToll)/100) + PvPKillReward;
                        //This notifies the player that they just pwned someone, and got some shards from them.
                        e.Killer.TSPlayer.SendMessage("You gained " + ((int)gain).ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "(s)!", Color.Green);
                        //This adds points to the killer's shard count.
                        EPREvents.PointOperate(EKiller, (int)gain, PointOperateReason.PVP);
                        //The following is my proposed changes.
                        //Notify the loser of his losses.
                        e.Killed.TSPlayer.SendMessage("You lost " + ((int)gain)).ToString() + " " + ServerPointSystem.ServerPointSystem.currname +"(s)!", Color.Green);
                        //Deduct the amount the killer gained by adding a negative amount. This appears to be how /deduct works, so I am mimicking it.
                        EPREvents.PointOperate(EKilled, -((int)gain),PointOperateReason.PVP);
                        //The comments were mainly for my own use, by the way.
                        
                    }
                }
            }
        }
        public static void OnGameEnd(C3Mod.GameEndArgs e)
        {
            int winnings = 0;
            switch (e.GameType.ToLower())
            {
                case "tdm":
                    {
                        int multiplier = e.WinningTeamScore - e.LosingTeamScore;
                        winnings = TDMReward * multiplier;
                        break;
                    }
                case "ctf":
                    {
                        winnings = CTFReward;
                        break;
                    }
                case "1v1":
                    {
                        winnings = GetCEPlayer(e.WinningTeamPlayers[0].TSPlayer.Index).DuelReward * 2;
                        break;
                    }
                case "oneflag":
                    {
                        winnings = OFReward;
                        break;
                    }
            }
            foreach (C3Player player in e.WinningTeamPlayers)
            {
                if (player.TSPlayer.IsLoggedIn)
                {
                    EPREvents.PointOperate(ServerPointSystem.ServerPointSystem.GetEPRPlayerByIndex(player.Index),winnings,PointOperateReason.PVPEvent);
                    player.TSPlayer.SendMessage("You gained " + winnings.ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "s!", Color.Green);
                }
            }
        }
        private static bool InPointRange(int killerbal, int killedbal)
        {
            if (killerbal <= ((killedbal + MoE) * PointRange) && killedbal <= ((killerbal + MoE) * PointRange))
                return true;
            else
                return false;
        }
        private static CEPlayer GetCEPlayer(int ply)
        {
            CEPlayer ceplayer = new CEPlayer();
            foreach (CEPlayer ceplayerctr in CEPlayers)
                if (ceplayerctr.ID == ply)
                    ceplayer = ceplayerctr;
            return ceplayer;
        }
        private static List<CEPlayer> WhoChallengedThisGuy(int ply)
        {
            List<CEPlayer> ListOfPplWCTG = new List<CEPlayer>();
            foreach (CEPlayer ceplayerctr in CEPlayers)
            {
                if (ceplayerctr.challenged == ply)
                    ListOfPplWCTG.Add(ceplayerctr);
            }
            return ListOfPplWCTG;
        }
        private static void SetupConfig()
        {
            try
            {
                if (File.Exists(CEConfigPath))
                {
                    CEConfig =CEConfigFile.Read(CEConfigPath);
                    // Add all the missing config properties in the json file
                }
                CEConfig.Write(CEConfigPath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error in (EPR) config file");
                Console.ForegroundColor = ConsoleColor.Gray;
                Log.Error("(EPR) Config Exception");
                Log.Error(ex.ToString());
            }
        }
    }

}
