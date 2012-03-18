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
    [APIVersion(1, 11)]
    public class ServerPointSystem : TerrariaPlugin
    {
        private static string PayLogs = Path.Combine(TShock.SavePath, "PayLogs");
        private static string PayLogSavePath = Path.Combine(PayLogs, "Paylogs.txt");
        private static string EPRLogSavePath = Path.Combine(TShock.SavePath, "EPRLogs.txt");
        public static SqlTableEditor SQLEditor;
        public static SqlTableCreator SQLWriter;
        public static EPRConfigFile EPRConfig { get; set; }
        internal static string EPRConfigPath { get { return Path.Combine(TShock.SavePath, "eprconfig.json"); } }
        public static string currname;
        public static float DeathToll = 90;
        public static int DeathTollStatic = 100;
        public static float PointMultiplier = 1;
        public static double LadyLucksMultiplier = 1;
        public static bool ReapersBlessingEnabled = false;
        public static List<EPRPlayer> EPRPlayers = new List<EPRPlayer>();
        public static List<TimeRewardPlayer> TimeRewardPlayers = new List<TimeRewardPlayer>();
        public static int TimeReward = 100;
        public static bool EnableTR = true;
        public static int RewardTime = 60;
        public static int ClaimTime = 30;
        public static DateTime[] LastStrike = new DateTime[1000];
        public static ENPC[] ENPCs = new ENPC[Main.maxNPCs];
        public override string Name
        {
            get { return "ServerPointSystem"; }
        }
        public override string Author
        {
            get { return "Created by Vharonftw"; }
        }
        public override string Description
        {
            get { return "Terraria Server-based Point System"; }
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
            NetHooks.GetData += NetHooks_GetData;
            EPREvents.OnMonsterPointAward += OnMonsterPointAward;
            EPREvents.OnPointOperate += OnPointOperate;
            EPREvents.OnPointPay += OnPointPay;
            EPREvents.OnPointUse += OnPointUse;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            //WorldHooks.SaveWorld += OnSaveWorld;
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
                NetHooks.GetData -= NetHooks_GetData;
                EPREvents.OnMonsterPointAward -= OnMonsterPointAward;
                EPREvents.OnPointOperate -= OnPointOperate;
                EPREvents.OnPointPay -= OnPointPay;
                EPREvents.OnPointUse -= OnPointUse;
                AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
                //WorldHooks.SaveWorld -= OnSaveWorld;
            }
            base.Dispose(disposing);
        }

        public void NetHooks_GetData(GetDataEventArgs e)
        {
            if (e.MsgID == PacketTypes.NpcStrike)
            {
                using (var data = new MemoryStream(e.Msg.readBuffer, e.Index, e.Length))
                {
                    var reader = new BinaryReader(data);
                    var npcid = reader.ReadInt16();
                    var dmg = reader.ReadInt16();
                    var knockback = reader.ReadSingle();
                    var direction = reader.ReadByte();
                    var crit = reader.ReadBoolean();
                    if (Main.npc[npcid].target < 255)
                    {
                        if (TShock.Players[e.Msg.whoAmI].IsLoggedIn)
                        {
                            int critmultiply=1;
                            if(crit)
                                critmultiply = 2;
                            int actualdmg = (dmg - Main.npc[npcid].defense / 2) * critmultiply;
                            if (actualdmg < 0)
                                actualdmg = 1;
                            int TimeSpan = (int)(DateTime.Now - LastStrike[npcid]).TotalMilliseconds;
                            LastStrike[npcid] = DateTime.Now;
                            //if (TimeSpan>300 && actualdmg >= Main.npc[npcid].life && Main.npc[npcid].damage > 0 && Main.npc[npcid].lifeMax >= 10 && Main.npc[npcid].type != 68 && Main.npc[npcid].life > 0 && Main.npc[npcid].active)
                            if ( Main.npc[npcid].value!=0 && actualdmg >= Main.npc[npcid].life && Main.npc[npcid].damage > 0 && Main.npc[npcid].lifeMax >= 10 && Main.npc[npcid].type != 68 && Main.npc[npcid].life > 0 && Main.npc[npcid].active)
                            {
                                Random r = new Random();
                                int gained = (int)((0.29915 * Math.Pow((double)Main.npc[npcid].value, 0.5983)) < 1 ? 1 : (0.29915 * Math.Pow((double)Main.npc[npcid].value, 0.5983)));
                                int variance = r.Next() % ((gained / 10) + 1);
                                int gainedfinal = gained + variance;
                                EPREvents.MonsterPointAward(npcid, Main.npc[npcid].type, gainedfinal, GetEPRPlayerByIndex(e.Msg.whoAmI));
                                return;
                            }
                            else if (Main.npc[npcid].value != 0 && actualdmg < Main.npc[npcid].life && Main.npc[npcid].damage > 0 && Main.npc[npcid].lifeMax >= 10 && Main.npc[npcid].type != 68 && Main.npc[npcid].life > 0 && Main.npc[npcid].active)
                            {
                                if (EPRConfig.EnablePointShare)
                                {
                                    double dmgpct = (double)actualdmg / (double)Main.npc[npcid].lifeMax;
                                    List<Attacker> PrevAtk = ENPCs[npcid].Attackers.FindAll(item => item.attacker.Index == e.Msg.whoAmI);
                                    if (PrevAtk.Count == 0)
                                    {
                                        ENPCs[npcid].Attackers.Add(new Attacker(GetEPRPlayerByIndex(e.Msg.whoAmI), dmgpct));
                                    }
                                    else if (PrevAtk.Count > 0)
                                    {
                                        PrevAtk[0].DamageDealtPct += dmgpct;
                                    }
                                }
                            }
                            return;
                        }
                    }
                    return;
                }

            }
            if (e.MsgID == PacketTypes.PlayerKillMe)
            {
                if ((!TShock.Players[e.Msg.whoAmI].Group.HasPermission("reapersblessing") && !GetEPRPlayerByIndex(e.Msg.whoAmI).ReaperBless) || !ReapersBlessingEnabled)
                {
                    using (var data = new MemoryStream(e.Msg.readBuffer, e.Index, e.Length))
                    {
                        var reader = new BinaryReader(data);
                        var playerid = reader.ReadInt16();
                        var direction = reader.ReadByte();
                        var damage = reader.ReadInt16();
                        var pvp = reader.ReadBoolean();
                        if (TShock.Players[e.Msg.whoAmI].IsLoggedIn)
                        {
                            EPRPlayer player =  GetEPRPlayerByIndex(e.Msg.whoAmI);
                            int prevbal = player.Account;
                            float balanceflt = (prevbal * DeathToll) / 100;
                            int balance = (int)balanceflt - DeathTollStatic;
                            if (balance < 0)
                                balance = 0;
                            TShock.Players[e.Msg.whoAmI].SendMessage("You have lost " + (prevbal - balance).ToString() + " " + currname + "s!", Color.Red);
                            EPREvents.PointOperate(player, balance - prevbal, PointOperateReason.Death);
                        }
                    }
                }

            }
        }

        public static void OnMonsterPointAward(MonsterAwardArgs e)
        {
            if (e.Player.AccountEnable && !e.Handled)
            {
                double gainedfinalflt = e.AwardAmount * PointMultiplier;
                if (e.Player.TSPlayer.Group.HasPermission("ladyluck") || e.Player.LadyLuck)
                    gainedfinalflt = gainedfinalflt * LadyLucksMultiplier;
                e.AwardAmount = (int)gainedfinalflt;
                if (!EPRConfig.EnablePointShare)
                {
                    EPREvents.PointOperate(e.Player, e.AwardAmount, PointOperateReason.MonsterKill);
                    if (e.Player.Notify)
                        e.Player.TSPlayer.SendMessage("you gained " + e.AwardAmount + " " + currname + "(s)!", Color.Green);
                }
                else
                {
                    lock (ENPCs[e.NPCID].Attackers)
                    {
                        foreach (Attacker player in ENPCs[e.NPCID].Attackers)
                        {
                            EPREvents.PointOperate(e.Player, (int)(e.AwardAmount * player.DamageDealtPct), PointOperateReason.MonsterKill);
                            if (player.attacker.Notify)
                                player.attacker.TSPlayer.SendMessage("you gained " + ((int)(e.AwardAmount * player.DamageDealtPct)).ToString() + " " + currname + "(s)!", Color.Green);
                        }
                    }
                    ENPCs[e.NPCID].Attackers.Clear();
                }
                e.Handled = true;
            }
        }

        public void OnPointOperate(PointOperateArgs e)
        {
            if(!e.Handled)
            {
                string[] EPRLog = new string[1];
                EPRLog[0] = string.Format("{0}: {1} operation by {2} Reason: {3} Amount: {4}, {2} now has {5} {1}", DateTime.Now.ToString(), currname, e.Player.Username, e.Reason,e.Amount,e.Player.Account);
                File.AppendAllLines(EPRLogSavePath, EPRLog);
                e.Player.Account += e.Amount;
                //e.Player.TSPlayer.SendMessage(string.Format("{0}: {1} operation by {2} Reason: {3} Amount: {4}", DateTime.Now.ToString(), currname, e.Player.Username, e.Reason, e.Amount), Color.Yellow);
                e.Handled = true;
            }
        }

        public void OnPointUse(PointUseArgs e)
        {
            if (!e.Handled)
            {
                string[] EPRLog = new string[1];
                EPRLog[0] = string.Format("{0}: {1} used by {2} Reason: {3} Amount: {4}", DateTime.Now.ToString(), currname, e.Player.Username, e.Reason, e.Amount);
                File.AppendAllLines(EPRLogSavePath, EPRLog);
                EPREvents.PointOperate(e.Player, -e.Amount,PointOperateReason.PlayerUse);
                e.Handled = true;
            }
        }

        public void OnPointPay(PointPayArgs e)
        {
            if (!e.Handled)
            {
                string[] EPRLog = new string[1];
                EPRLog[0] = string.Format("{0}: {1} paid {2} {3}(s) to {4}", DateTime.Now.ToString(), e.Sender.Username, e.Amount, currname, e.Receiver.Username);
                File.AppendAllLines(EPRLogSavePath, EPRLog);
                EPREvents.PointUse(e.Sender, e.Amount, PointUsage.SentPayment);
                EPREvents.PointOperate(e.Sender, e.Amount,PointOperateReason.ReceivedPayment);
                e.Handled = true;
            }
        }

        #region Commands
        public static void ReaperBless(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count == 1)
                {
                    TSPlayer Player = TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0])[0];
                    if (GetEPRPlayerByIndex(Player.Index).ReaperBless)
                    {
                        GetEPRPlayerByIndex(Player.Index).ReaperBless = false;
                        args.Player.SendMessage("Reaper's blessing is disabled for " + Player.Name, Color.Green);
                        Player.SendMessage("When the bells of Death Tolls the reaper gets his Toll", Color.Red);
                    }
                    else
                    {
                        GetEPRPlayerByIndex(Player.Index).ReaperBless = true;
                        args.Player.SendMessage("Reaper's blessing is enabled for " + Player.Name, Color.Green);
                        Player.SendMessage("The Reaper gives you his blessings", Color.Green);
                    }
                }
                else if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count < 1)
                {
                    args.Player.SendMessage("There is no such player!", Color.Red);
                }
                else if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count > 1)
                {
                    args.Player.SendMessage("More than 1 player matched!", Color.Red);
                }

            }
            else
            {
                args.Player.SendMessage("/reaperbless [player name]", Color.Yellow);
                args.Player.SendMessage("/rb [player name]", Color.Yellow);
            }
        }

        public static void LadyLuck(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count == 1)
                {
                    TSPlayer Player = TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0])[0];
                    if (GetEPRPlayerByIndex(Player.Index).LadyLuck)
                    {
                        GetEPRPlayerByIndex(Player.Index).LadyLuck = false;
                        args.Player.SendMessage("Lady Luck is bored with " + Player.Name, Color.Green);
                        Player.SendMessage("Lady Luck is bored with you", Color.Green);
                    }
                    else
                    {
                        GetEPRPlayerByIndex(Player.Index).LadyLuck = true;
                        args.Player.SendMessage("Lady Luck Smiles on " + Player.Name, Color.Green);
                        Player.SendMessage("Lady Luck Smiles upon you", Color.Green);
                    }
                }
                else if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count < 1)
                {
                    args.Player.SendMessage("There is no such player!", Color.Red);
                }
                else if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count > 1)
                {
                    args.Player.SendMessage("More than 1 player matched!", Color.Red);
                }

            }
            else
            {
                args.Player.SendMessage("/ladylucksmileson [player name]", Color.Yellow);
                args.Player.SendMessage("/llso [player name]", Color.Yellow);
            }

        }

        public static void CPoints(CommandArgs args)
        {
            string text = "";
            string cmd = "help";
            if (args.Parameters.Count > 0)
            {
                cmd = args.Parameters[0].ToLower();
                switch (cmd)
                {
                    case "reaper":
                            {
                                if (ReapersBlessingEnabled)
                                {
                                    ReapersBlessingEnabled = false;
                                    args.Player.SendMessage("Reaper's Blessing is disabled", Color.Red);
                                    foreach (EPRPlayer player in EPRPlayers)
                                    {
                                        if (player.TSPlayer.Group.HasPermission("reapersblessing") || player.ReaperBless)
                                            player.TSPlayer.SendMessage("When the bells of Death tolls the Reaper shall collect his tolls", Color.Red);
                                    }
                                }
                                else
                                {
                                    ReapersBlessingEnabled = true; 
                                    foreach (EPRPlayer player in EPRPlayers)
                                    {
                                        if (player.TSPlayer.Group.HasPermission("reapersblessing") || player.ReaperBless)
                                            player.TSPlayer.SendMessage("The Reaper shall let you pass the gates of death for free", Color.Red);
                                    }
                                }
                                break;
                            }
                    case "ladyluck":
                        {
                            if (args.Parameters.Count > 1)
                            {
                                double.TryParse(args.Parameters[1], out LadyLucksMultiplier);
                                args.Player.SendMessage("Lady Luck changed her mind and now multiplies points by " + LadyLucksMultiplier.ToString(), Color.Red);
                                foreach (EPRPlayer player in EPRPlayers)
                                {
                                    if (player.TSPlayer.Group.HasPermission("ladyluck") || player.LadyLuck)
                                        player.TSPlayer.SendMessage("Lady Luck smiles on you and multiplies the " + currname + "s you gain by " + LadyLucksMultiplier.ToString(), Color.Red);
                                }
                            }
                            break;
                        }
                    case "name":
                        {
                            if (args.Parameters.Count > 1)
                            {
                                text = args.Parameters[1].ToString();
                                currname = text;
                                //TShockAPI.TShock.Utils.Broadcast("Points are now called " + text + "s", Color.Yellow);
                                //List<SqlValue> values = new List<SqlValue>();
                                //values.Add(new SqlValue("currname", "'" + currname + "'"));
                                //SQLEditor.UpdateValues("ServerPointSystemName", values, new List<SqlValue>());
                                Commands.ChatCommands.Add(new Command("pouch", Points, currname));
                            }
                            else
                            {
                                args.Player.SendMessage("Syntax Error! /cpoints name [new name]", Color.IndianRed);
                            }
                            break;
                        }
                    case "deathtoll":
                        {
                            if (args.Parameters.Count > 1)
                            {
                                float.TryParse(args.Parameters[1], out DeathToll);
                                if (DeathToll > 100 || DeathToll < 0)
                                    DeathToll = 100;
                                TShockAPI.TShock.Utils.Broadcast("You will now lose " + (100 - DeathToll).ToString() + "% of your " + currname + "s upon death!", Color.IndianRed);
                            }
                            else
                            {
                                args.Player.SendMessage("Syntax Error! /cpoints deathtoll [number in percent -- no need for the percent sign]", Color.IndianRed);
                            }
                        }
                        break;
                    case "deathtollstatic":
                        {
                            if (args.Parameters.Count > 1)
                            {
                                int.TryParse(args.Parameters[1], out DeathTollStatic);
                                if (DeathTollStatic < 0)
                                    DeathTollStatic = 0;
                                TShockAPI.TShock.Utils.Broadcast("You will now lose an additional " + DeathTollStatic.ToString() + " " + currname + "s upon death!", Color.IndianRed);
                            }
                            else
                            {
                                args.Player.SendMessage("Syntax Error! /cpoints deathtollstatic [number]", Color.IndianRed);
                            }
                        }
                        break;
                    case "setmultiplier":
                        {
                            if (args.Parameters.Count > 1)
                            {
                                float.TryParse(args.Parameters[1], out PointMultiplier);
                                if (PointMultiplier < 0)
                                    PointMultiplier = 0;
                                if (PointMultiplier > 1)
                                    TShockAPI.TShock.Utils.Broadcast("You will now gain " + PointMultiplier.ToString() + "x more " + currname + "s per monster killed!", Color.IndianRed);
                                if (PointMultiplier < 1)
                                    TShockAPI.TShock.Utils.Broadcast("You will now gain " + ((1 - PointMultiplier) * 100).ToString() + "% less " + currname + "s per monster killed!", Color.IndianRed);
                                if (PointMultiplier == 1)
                                    TShockAPI.TShock.Utils.Broadcast("The " + currname + "-drop multiplier has been reset to 1", Color.IndianRed);
                            }
                            else
                            {
                                args.Player.SendMessage("Syntax Error! /cpoints setmultiplier [number -- can be a decimal value]", Color.IndianRed);
                            }
                        }
                        break;
                    default:
                        args.Player.SendMessage("/cpoints name [new point name]", Color.Yellow);
                        args.Player.SendMessage("/cpoints deathtoll [number in percent -- no need for the percent sign]", Color.Yellow);
                        args.Player.SendMessage("/cpoints deathtollstatic [number]", Color.Yellow);
                        args.Player.SendMessage("/cpoints setmultiplier [number -- can be a decimal value]", Color.Yellow);
                        args.Player.SendMessage("/cpoints reaper -- enables reaper's blessing", Color.Yellow);
                        args.Player.SendMessage("/cpoints ladyluck [number -- can be a decimal value] -- sets the multiplier for lady luck", Color.Yellow);
                        break;
                }
            }
        }    

        public static void Points(CommandArgs args)
        {
            string cmd = "help";
            if (args.Parameters.Count > 0)
            {
                cmd = args.Parameters[0].ToLower();
                switch (cmd)
                {
                    case "claim":
                        {
                            int ID = GetTRPlayerID(args.Player.Index);
                            if (TimeRewardPlayers[ID].canclaim)
                            {
                                EPREvents.PointOperate(GetEPRPlayerByIndex(args.Player.Index), TimeReward, PointOperateReason.TimeReward);
                                TimeRewardPlayers[ID].LastReward = DateTime.Now;
                                TimeRewardPlayers[ID].canclaim = false;
                                TimeRewardPlayers[ID].notify = true;
                                args.Player.SendMessage("You have received " + TimeReward.ToString() + " " + currname + "s", Color.Green);
                            }
                            else
                                args.Player.SendMessage("You are not elligible to claim this reward", Color.Red);
                            break;
                        }
                    case "notify":
                        {
                            EPRPlayer player = GetEPRPlayerByIndex(args.Player.Index);
                            if (args.Player.IsLoggedIn)
                            {
                                if (player.Notify)
                                {
                                    player.Notify = false;
                                    args.Player.SendMessage("You will not receive any more " + currname + "-gain notifications when you kill a monster", Color.Green);
                                }
                                else
                                {
                                    player.Notify = true;
                                    args.Player.SendMessage("You will now receive " + currname + "-gain notifications when you kill monsters", Color.Green);
                                }
                            }
                            else
                            {
                                args.Player.SendMessage("You must be logged in to do that!", Color.Red);
                            }
                        }
                        break;
                    case "pay":
                        #region PAY
                        {
                            if (args.Player.IsLoggedIn)
                            {
                                if (args.Parameters.Count > 1)
                                {
                                    int count = TShockAPI.TShock.Utils.FindPlayer(args.Parameters[1]).Count;
                                    if (count == 1)
                                    {
                                        TSPlayer Receiver = TShockAPI.TShock.Utils.FindPlayer(args.Parameters[1])[0];
                                        if (Receiver.IsLoggedIn)
                                        {
                                            EPRPlayer ESender = GetEPRPlayerByIndex(args.Player.Index);
                                            EPRPlayer EReceiver = GetEPRPlayerByIndex(Receiver.Index);
                                            int amount = 0;
                                            if (args.Parameters.Count > 2)
                                                Int32.TryParse(args.Parameters[2], out amount);
                                            if (ESender.Account >= amount && amount>0)
                                            {
                                                if (ESender != EReceiver)
                                                {
                                                    EPREvents.PointUse(ESender, amount, PointUsage.SentPayment);
                                                    EPREvents.PointOperate(EReceiver, amount, PointOperateReason.ReceivedPayment);
                                                    args.Player.SendMessage(string.Format("You paid {0} {1}(s) to {2}'s account", amount, currname,Receiver.Name), Color.Green);
                                                    Receiver.SendMessage(string.Format("{0} paid{1} {2}(s) to your account", args.Player.Name, amount, currname), Color.Green);
                                                }
                                                else
                                                    ESender.TSPlayer.SendMessage("You cannot pay yourself silly person", Color.Red);
                                            }
                                            else
                                                args.Player.SendMessage("Please Enter a valid amount", Color.Red);
                                        }
                                        else
                                        {
                                            args.Player.SendMessage(string.Format("{0} must be logged in to receive payment", Receiver.Name), Color.Red);
                                        }
                                    }
                                    else
                                    {
                                        args.Player.SendMessage(string.Format("Invalid! {0} players matched",count), Color.Red);
                                    }
                                }
                            }
                            else
                            {
                                args.Player.SendMessage("You must be logged in to do that!", Color.Red);
                            }
                            break;
                        }
                        #endregion
                    case "pouch":
                        #region Pouch
                        {
                            if (args.Player.IsLoggedIn)
                            {
                                EPRPlayer player = GetEPRPlayerByIndex(args.Player.Index);
                                List<SqlValue> where = new List<SqlValue>();
                                where.Add(new SqlValue("name", "'" + args.Player.UserAccountName + "'"));
                                List<SqlValue> values = new List<SqlValue>();
                                values.Add(new SqlValue("amount", player.Account));
                                SQLEditor.UpdateValues("ServerPointAccounts", values, where);
                                string currbal = player.Account.ToString();
                                if (Convert.ToInt32(currbal) == 1)
                                {
                                    args.Player.SendMessage("Your pouch contains " + currbal + " " + currname, Color.Yellow);
                                }
                                else
                                {
                                    args.Player.SendMessage("Your pouch contains " + currbal + " " + currname + "s", Color.Yellow);
                                }
                            }
                            else
                            {
                                args.Player.SendMessage("You must be logged in to do that!", Color.Red);
                            }
                            break;
                        }
                        #endregion
                    default:
                        {
                            args.Player.SendMessage("/shards pouch", Color.Yellow);
                            args.Player.SendMessage("/shards pay [player account] [number of shards]", Color.Yellow);
                            break;
                        }

                }


            }
            else
            {
                args.Player.SendMessage("/shards pouch", Color.Yellow);
                args.Player.SendMessage("/shards pay [player account] [number of shards]", Color.Yellow);
            }
        }

        public static void Award(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                int award = 1;
                if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count > 1)
                    args.Player.SendMessage("More than 1 player matched!", Color.Red);
                else if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count < 1)
                {
                    args.Player.SendMessage("No player matched", Color.Red);
                }
                else if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count == 1)
                {
                    EPRPlayer player = GetEPRPlayerByIndex(TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0])[0].Index);
                    if (player.TSPlayer.IsLoggedIn)
                    {
                        if (args.Parameters.Count > 1)
                            Int32.TryParse(args.Parameters[1], out award);
                        if (award < 0)
                            award = 1;
                        EPREvents.PointOperate(player, award, PointOperateReason.Award);
                        TShockAPI.TShock.Utils.Broadcast(args.Player.Name + " has added " + award.ToString() + " " + currname + "s to " + TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0])[0].Name + "'s account", Color.Yellow);
                    }
                    else
                        args.Player.SendMessage("Player is not logged in!", Color.Red);
                }

            }
            else
            {
                args.Player.SendMessage("/award [player name] [amount -- default is 1 if not specified]", Color.Yellow);
            }
        }

        public static void AwardAll(CommandArgs args)
        {
            int award = 1;
            if (args.Parameters.Count > 1 && args.Parameters[1]== "help")
            {
                args.Player.SendMessage("/awardall [amount]", Color.Yellow);
            }
            else
            {
                if (args.Parameters.Count > 0)
                    Int32.TryParse(args.Parameters[0], out award);
                foreach (EPRPlayer player in EPRPlayers)
                {
                    if (player.TSPlayer.IsLoggedIn)
                    {
                        if (award < 0)
                            award = 1;
                        EPREvents.PointOperate(player, award, PointOperateReason.Award);
                        player.TSPlayer.SendMessage(args.Player.Name + " has added " + award.ToString() + " " + currname + "s to everyone's account", Color.Yellow);
                    }
                }
            }
        }

        public static void Deduct(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                int deduct = 1;
                if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count > 1)
                    args.Player.SendMessage("More than 1 player matched!", Color.Red);
                else if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count < 1)
                {
                    args.Player.SendMessage("No player matched", Color.Red);
                }
                else if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count == 1)
                {
                    EPRPlayer player = GetEPRPlayerByIndex(TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0])[0].Index);
                    if (player.TSPlayer.IsLoggedIn)
                    {                        
                        if (args.Parameters.Count > 1)
                            Int32.TryParse(args.Parameters[1], out deduct);
                        if (deduct > player.Account)
                            deduct = player.Account;
                        EPREvents.PointOperate(player, -deduct, PointOperateReason.Deduct);
                    }
                    else
                        args.Player.SendMessage("Player is not logged in!", Color.Red);
                }

            }
            else
            {
                args.Player.SendMessage("/deduct [player name] [amount -- default is 1 if not specified]", Color.Yellow);
            }
        }

        public static void DeductAll(CommandArgs args)
        {
            int deduct = 1;
            foreach (EPRPlayer player in EPRPlayers)
            {
                if (player.TSPlayer.IsLoggedIn)
                {
                    int deducttemp = deduct;
                    if (deducttemp > player.Account)
                        deducttemp = player.Account;
                    EPREvents.PointOperate(player, -deduct, PointOperateReason.Deduct);
                }
            }
        }

        public static void CheckDeathToll(CommandArgs args)
        {
            if (args.Player.IsLoggedIn)
            {
                if (DeathToll < 100 && DeathTollStatic > 0)
                    args.Player.SendMessage("You'll lose " + (100 - DeathToll).ToString() + "% of your " + currname + "s plus " + DeathTollStatic.ToString() + " " + currname + "s upon death", Color.IndianRed);
                else if (DeathToll == 100 && DeathTollStatic > 0)
                    args.Player.SendMessage("You'll lose " + DeathTollStatic.ToString() + " " + currname + "s upon death", Color.IndianRed);
                else if (DeathToll < 100 && DeathTollStatic == 0)
                    args.Player.SendMessage("You'll lose " + (100 - DeathToll).ToString() + "% of your " + currname + "s upon death", Color.IndianRed);
                else
                    args.Player.SendMessage("DeathToll is currently disabled. You will not lose " + currname + "s upon death", Color.Green);
                if ((args.Player.Group.HasPermission("reaperbless") || GetEPRPlayerByIndex(args.Player.Index).ReaperBless) && ReapersBlessingEnabled)
                {
                    args.Player.SendMessage("You have been blessed by the Reaper", Color.Green);
                    args.Player.SendMessage("Your " + currname + "s are safe!", Color.Green);
                }

            }
        }

        public static void CheckMultiplier(CommandArgs args)
        {
            if (args.Player.IsLoggedIn)
            {                
                if (PointMultiplier > 1)
                    args.Player.SendMessage("You will currently gain " + PointMultiplier.ToString() + "x more " + currname + "s per monster killed!", Color.Green);
                if (PointMultiplier < 1)
                    args.Player.SendMessage("You will currently gain " + ((1 - PointMultiplier) * 100).ToString() + "% less " + currname + "s per monster killed!", Color.Green);
                if (PointMultiplier == 1)
                    args.Player.SendMessage("The " + currname + "-drop multiplier has been set to 1", Color.Green);
                if (args.Player.Group.HasPermission("ladyluck") || GetEPRPlayerByIndex(args.Player.Index).LadyLuck)
                {
                    args.Player.SendMessage("Lady luck has smiled on you! Your "+ currname+ "-gain will be multiplied by " + LadyLucksMultiplier.ToString(), Color.Green);
                }
            }
        }
        #endregion

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.IsTerminating)
            {
                lock (EPRPlayers)
                {
                    for (int i = 0; i < EPRPlayers.Count; i++)
                    {
                        List<SqlValue> where = new List<SqlValue>();
                        where.Add(new SqlValue("name", "'" + EPRPlayers[i].Username + "'"));
                        List<SqlValue> values = new List<SqlValue>();
                        values.Add(new SqlValue("amount", EPRPlayers[i].Account));
                        SQLEditor.UpdateValues("ServerPointAccounts", values, where);
                    }
                }
            }
        }
        
        public ServerPointSystem(Main game)
            : base(game)
        {
            EPRConfig = new EPRConfigFile();
        }

        
        public void OnInitialize()
        {
            for(int i = 0; i<LastStrike.Length;i++)
            {
                LastStrike[i] = DateTime.Now;
            }
            if (!Directory.Exists(TShock.SavePath))
            {
                Directory.CreateDirectory(TShock.SavePath);
            }

            if (!File.Exists(EPRLogSavePath))
            {
                File.Create(EPRLogSavePath).Close();
            }
            SetupConfig();

            DeathToll = EPRConfig.DeathToll;
            DeathTollStatic = EPRConfig.DeathTollStatic;
            PointMultiplier = EPRConfig.PointMultiplier;
            currname = EPRConfig.currname;
            ReapersBlessingEnabled = EPRConfig.ReapersBlessingEnabled;
            LadyLucksMultiplier = EPRConfig.LadyLucksMultiplier;
            TimeReward = EPRConfig.TimeReward;
            EnableTR = EPRConfig.EnableTimeRewards;
            RewardTime = EPRConfig.RewardTime;
            ClaimTime = EPRConfig.ClaimTime;
            if (ClaimTime >= RewardTime * 60)
                ClaimTime = (RewardTime * 60) - 1;

            for(int i = 0; i <Main.maxNPCs;i++)
            {
                ENPCs[i] = new ENPC(i);
            }

            Commands.ChatCommands.Add(new Command("changepoints", CPoints, "cpoints"));
            Commands.ChatCommands.Add(new Command("pouch", Points, "shards"));
            Commands.ChatCommands.Add(new Command("pouch", Points, "points"));
            Commands.ChatCommands.Add(new Command("pouch", CheckDeathToll, "checkdeathtoll","checkdt"));
            Commands.ChatCommands.Add(new Command("pouch", CheckMultiplier, "checkmultiplier", "checkmult"));
            Commands.ChatCommands.Add(new Command("manage", Award, "award"));
            Commands.ChatCommands.Add(new Command("manage", Deduct, "deduct"));
            Commands.ChatCommands.Add(new Command("manage", AwardAll, "awardall"));
            Commands.ChatCommands.Add(new Command("manage", DeductAll, "deductall"));
            Commands.ChatCommands.Add(new Command("manage", LadyLuck, "ladylucksmileson","llso"));
            Commands.ChatCommands.Add(new Command("manage", ReaperBless, "reaperbless", "rb"));
            Commands.ChatCommands.Add(new Command("manage", GetPlayerBalance,"balance","bal"));
            bool changepoints = false;
            bool pouchperm = false;
            bool manageperm = false;

            foreach (Group group in TShock.Groups.groups)
            {
                if (group.Name != "superadmin")
                {
                    if (group.HasPermission("changepoints"))
                        changepoints = true;
                    if (group.HasPermission("pouch"))
                        pouchperm = true;
                    if (group.HasPermission("manage"))
                        manageperm = true;
                }
            }

            List<string> permlist = new List<string>();
            List<string> permlist2 = new List<string>();
            List<string> permlist3 = new List<string>();
            if (!changepoints)
                permlist2.Add("changepoints");
            if (!pouchperm)
                permlist.Add("pouch");
            if (!manageperm)
                permlist3.Add("manage");

            TShock.Groups.AddPermissions("default", permlist);
            TShock.Groups.AddPermissions("trustedadmin", permlist2);
            TShock.Groups.AddPermissions("trustedadmin", permlist3);
            SQLEditor = new SqlTableEditor(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            SQLWriter = new SqlTableCreator(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

            var table = new SqlTable("ServerPointAccounts",
                new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                new SqlColumn("name", MySqlDbType.String, 255) { Unique = true },
                new SqlColumn("amount", MySqlDbType.Int32)
            );
            SQLWriter.EnsureExists(table);
            Commands.ChatCommands.Add(new Command("pouch", Points, currname));
            if (!Directory.Exists(PayLogs))
            {
                Directory.CreateDirectory(PayLogs);
            }

            if (!File.Exists(PayLogSavePath))
            {
                File.Create(PayLogSavePath).Close();
            }
        }
        //Sorry if I syntax fail, I don't have C# completely down yet.
        //This is so admins don't have to get into the DB to look at shard amounts, basically peeking at players' pouch 
        public void GetPlayerBalance(CommandArgs c)
        {
            if(c.Parameters.Count>0)
            {
                for(int i=0;i<EPRPlayers.Length;i++)
                {
                    if(EPRPlayers(i).TSPlayer.Name == (c.Parameters[0]))
                    {
                        c.Player.SendMessage(EPRPlayers(i).TSPlayer.Name + " has " + EPRPlayers(i).Account.ToString(), Color.Yellow);
                    }
                }
            }
            else
            {
                c.Player.SendMessage("Invalid Syntax! Correct syntax is /balance <name> or /bal <name>");
            }
        }
        public void OnUpdate()
        {
            //List<int> RemoveTRPlayer = new List<int>();
            lock (TimeRewardPlayers)
            {
                for (int i = 0; i < TimeRewardPlayers.Count; i++ )
                {
                    bool PlayerIsLoggedIn = false;
                    try
                    {
                        PlayerIsLoggedIn = TShock.Players[TimeRewardPlayers[i].Index].IsLoggedIn;
                    }
                    catch (Exception) { }
                    if (EnableTR && PlayerIsLoggedIn)
                    {
                        int Minutes = (int)(DateTime.Now - TimeRewardPlayers[i].LastReward).TotalMinutes;
                        if (Minutes >= RewardTime && TimeRewardPlayers[i].notify)
                        {
                            TShock.Players[TimeRewardPlayers[i].Index].SendMessage("You have earned a reward for playing for over " + RewardTime.ToString() + " minutes", Color.Green);
                            TShock.Players[TimeRewardPlayers[i].Index].SendMessage("You may claim it by typing /shards claim This will expire in " + ClaimTime.ToString() + " seconds", Color.Green);
                            TimeRewardPlayers[i].LastNotify = DateTime.Now;
                            TimeRewardPlayers[i].notify = false;
                            TimeRewardPlayers[i].canclaim = true;
                        }
                        if ((DateTime.Now - TimeRewardPlayers[i].LastNotify).TotalSeconds > ClaimTime && ClaimTime != 0 && !TimeRewardPlayers[i].notify)
                        {
                            TimeRewardPlayers[i].LastReward = DateTime.Now;
                            TimeRewardPlayers[i].notify = true;
                            TimeRewardPlayers[i].canclaim = false;
                            TShock.Players[TimeRewardPlayers[i].Index].SendMessage("Reward has expired.", Color.Red);
                        }
                    }
                    //else if (!TShock.Players[TimeRewardPlayers[i].Index].Active)
                    //{
                    //    RemoveTRPlayer.Add(i);
                    //}
                }
            }
            //foreach (int TRIndex in RemoveTRPlayer)
            //{
            //    TimeRewardPlayers.RemoveAt(TRIndex);
            //}
            foreach (ENPC npc in ENPCs)
            {
                if (!npc.MNPC.active)
                {
                    npc.Attackers.Clear();
                }
            }
        }

        public void OnGreetPlayer(int who, HandledEventArgs e)
        {
            lock (EPRPlayers)
                EPRPlayers.Add(new EPRPlayer(who));
            lock (TimeRewardPlayers)
                TimeRewardPlayers.Add(new TimeRewardPlayer(who));


        }

        public void OnLeave(int ply)
        {
            lock (EPRPlayers)
            {
                for (int i = 0; i < EPRPlayers.Count; i++)
                {
                    if (EPRPlayers[i].Index == ply)
                    {
                        List<SqlValue> where = new List<SqlValue>();
                        where.Add(new SqlValue("name", "'" + EPRPlayers[i].Username + "'"));
                        List<SqlValue> values = new List<SqlValue>();
                        values.Add(new SqlValue("amount", EPRPlayers[i].Account));
                        SQLEditor.UpdateValues("ServerPointAccounts", values, where);
                        EPRPlayers.RemoveAt(i);
                        break; //Found the player, break.
                    }
                }
            }
        }

        public void OnChat(messageBuffer msg, int ply, string text, HandledEventArgs e)
        {
            string firstword = text.Split(' ')[0];
            if (firstword == "/login")
            {
                if (TShock.Players[ply].IsLoggedIn)
                {
                    bool PlayerHasAnSPAccount = false;
                    int count = SQLEditor.ReadColumn("ServerPointAccounts", "ID", new List<SqlValue>()).Count;
                    object[] ServerPointAccounts = new object[count];
                    SQLEditor.ReadColumn("ServerPointAccounts", "name", new List<SqlValue>()).CopyTo(ServerPointAccounts);
                    for (int i = 0; i < count; i++)
                    {
                        if (TShock.Players[ply].UserAccountName == ServerPointAccounts[i].ToString())
                        {
                            EPRPlayer player = GetEPRPlayerByIndex(ply);
                            if (player.Username != "" && player.AccountEnable)
                            {
                                List<SqlValue> where = new List<SqlValue>();
                                where.Add(new SqlValue("name", "'" + player.Username + "'"));
                                List<SqlValue> values = new List<SqlValue>();
                                values.Add(new SqlValue("amount", player.Account));
                                SQLEditor.UpdateValues("ServerPointAccounts", values, where);
                            }
                            player.Username = TShock.Players[ply].UserAccountName;
                            player.AccountEnable = true;
                            player.Account = 0;
                            List<SqlValue> where2 = new List<SqlValue>();
                            where2.Add(new SqlValue("name", "'" + TShock.Players[ply].UserAccountName.ToString() + "'"));
                            if (SQLEditor.ReadColumn("ServerPointAccounts", "amount", where2).Count > 0)
                            {
                                player.Account = int.Parse(SQLEditor.ReadColumn("ServerPointAccounts", "amount", where2)[0].ToString());
                            }
                            PlayerHasAnSPAccount = true;
                            break;
                        }
                    }
                    if (!PlayerHasAnSPAccount)
                    {
                        int defaultbal = 0;
                        List<SqlValue> list = new List<SqlValue>();
                        list.Add(new SqlValue("name", "'" + TShock.Players[ply].UserAccountName.ToString() + "'"));
                        list.Add(new SqlValue("amount", defaultbal));
                        SQLEditor.InsertValues("ServerPointAccounts", list);
                        EPRPlayer player = GetEPRPlayerByIndex(ply);
                        if (player.Username != "" && player.AccountEnable)
                        {
                            List<SqlValue> where = new List<SqlValue>();
                            where.Add(new SqlValue("name", "'" + player.Username + "'"));
                            List<SqlValue> values = new List<SqlValue>();
                            values.Add(new SqlValue("amount", player.Account));
                            SQLEditor.UpdateValues("ServerPointAccounts", values, where);
                        }
                        player.Username = TShock.Players[ply].UserAccountName;
                        player.AccountEnable = true;
                        player.Account = 0;
                        TShock.Players[ply].SendMessage("A pouch was created for this account. You can now start accumulating " + currname + "s!", Color.Green);
                    }
                    #region hide this messy code
                    //Tempaccounts.AddTempAccount(TempAccount(TShock.Players[ply].UserAccountName, ply));
                    //List<string> AccountsToRemove = new List<string>();
                    //foreach (TempAccount tempaccountctr in Tempaccounts.tempaccounts)
                    //{
                    //    if (AccountOwnerLoggedOnAdifferentAccount(tempaccountctr.AccountName))
                    //    {
                    //        AccountsToRemove.Add(tempaccountctr.AccountName);
                    //        //Tempaccounts.RemoveAndUpdate(tempaccountctr.AccountName);
                    //    }
                    //}
                    //if(AccountsToRemove.Count > 0)
                    //{
                    //    foreach (string accountname in AccountsToRemove)
                    //    {
                    //        Tempaccounts.RemoveAndUpdate(accountname);
                    //    }
                    //}
                    //AccountsToRemove.Clear();
                    #endregion
                    for (int i = 0; i < TimeRewardPlayers.Count; i++)
                    {
                        if (ply == TimeRewardPlayers[i].Index)
                            TimeRewardPlayers[i].LastReward = DateTime.Now;
                    }
                }
            }
        }
        
        public static EPRPlayer GetEPRPlayerByIndex(int index)
        {
            EPRPlayer player = new EPRPlayer(index);
            lock (EPRPlayers)
            {
                foreach (EPRPlayer playerctr in EPRPlayers)
                {
                    if (playerctr.Index == index)
                        player = playerctr;
                }
            }
            return player;
        }
       
        public static int GetTRPlayerID(int who)
        {
            int ID=-1;
            lock (TimeRewardPlayers)
            {
                for(int i = 0; i<TimeRewardPlayers.Count; i++)
                {
                    if (TimeRewardPlayers[i].Index == who)
                        ID = i;
                }
            }
            return ID;
        }
       
        public static bool DontMultiply(int type)
        {
            bool flag = false;
            switch (type)
            {
                case 13:
                case 14:
                case 15:
                case 26:
                case 27:
                case 28:
                case 29:
                    flag = true;
                    break;
                default:
                    flag = false;
                    break;
            }
            return flag;
        }
       
        public static void SetupConfig()
        {
            try
            {
                if (File.Exists(EPRConfigPath))
                {
                    EPRConfig = EPRConfigFile.Read(EPRConfigPath);
                    // Add all the missing config properties in the json file
                }
                EPRConfig.Write(EPRConfigPath);
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
