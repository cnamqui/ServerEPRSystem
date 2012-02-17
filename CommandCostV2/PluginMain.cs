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

namespace CommandShop
{
    [APIVersion(1, 10)]
    public class CommandShop : TerrariaPlugin
    {
        private static SqlTableEditor SQLEditor;
        private static SqlTableCreator SQLWriter;
        internal static string CmdParserDataDirectory { get { return Path.Combine(TShock.SavePath, "Command Shop Data"); } }
        private static List<CommandParser> CommandParserList = new List<CommandParser>();
        public override string Name
        {
            get { return "CommandShop"; }
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
        public CommandShop(Main game)
            : base(game)
        {
            Order = -1;
        }

        public void OnInitialize()
        {
            if (!Directory.Exists(CmdParserDataDirectory))
            {
                Directory.CreateDirectory(CmdParserDataDirectory);
            }
            LoadCmdParsersFromText();
            SQLEditor = new SqlTableEditor(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            SQLWriter = new SqlTableCreator(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            bool checkcommandcost = false;
            foreach (Group group in TShock.Groups.groups)
            {
                if (group.Name != "superadmin")
                {
                    if (group.HasPermission("checkcommandcost"))
                        checkcommandcost = true;
                }
            }

            List<string> permlist = new List<string>();
            if (!checkcommandcost)
                permlist.Add("checkcommandcost");
            TShock.Groups.AddPermissions("trustedadmin", permlist);
            Commands.ChatCommands.Add(new Command("checkcommandcost", CheckCommandCost, "checkcommandcost"));
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
            string cmd = text;
            if (TShock.Players[ply].IsLoggedIn && text.StartsWith("/"))
            {
                Command chckcmd = TShockAPI.Commands.ChatCommands.Find(fcmd => fcmd.Names.Contains(cmd.Split(' ')[0].Remove(0, 1)));
                if (chckcmd != null)
                {
                    if (TShock.Players[ply].Group.HasPermission(chckcmd.Permission))
                    {
                        EPRPlayer player = ServerPointSystem.ServerPointSystem.GetEPRPlayerByIndex(ply);
                        bool block = false;
                        bool charge = false;
                        int cost = 0;
                        foreach (CommandParser cmdprsr in CommandParserList)
                        {
                            if (cmdprsr.BlockType == BlockType.BlockStartsWith && !TShock.Players[ply].Group.HasPermission(cmdprsr.BlockOverridePermission) && cmdprsr.BlockOverridePermission != "" && cmd.ToLower().StartsWith(cmdprsr.Command.ToLower()))
                                block = true;
                            else if (cmdprsr.BlockType == BlockType.BlockEqualsTo && !TShock.Players[ply].Group.HasPermission(cmdprsr.BlockOverridePermission) && cmdprsr.BlockOverridePermission != "" && cmd.ToLower().Equals(cmdprsr.Command.ToLower()))
                                block = true;

                            if (cmdprsr.ChargeType == ChargeType.StartsWith && cmd.ToLower().StartsWith(cmdprsr.Command.ToLower()))
                            {
                                charge = true;
                                if (cmdprsr.Cost > 0)
                                    cost = cmdprsr.Cost;
                                if (TShock.Players[ply].Group.HasPermission(cmdprsr.CostOverridePermission.ToLower()) && cmdprsr.CostOverridePermission != "")
                                    cost = 0;
                            }
                            else if (cmdprsr.ChargeType == ChargeType.EqualsTo && cmd.ToLower().Equals(cmdprsr.Command.ToLower()))
                            {
                                charge = true;
                                if (cmdprsr.Cost > 0)
                                    cost = cmdprsr.Cost;
                                if (TShock.Players[ply].Group.HasPermission(cmdprsr.CostOverridePermission.ToLower()) && cmdprsr.CostOverridePermission != "")
                                    cost = 0;
                            }
                        }
                        if (block)
                        {
                            e.Handled = true;
                            return;
                        }
                        else if (!block && charge && cost > 0)
                        {
                            if (player.DisplayAccount >= cost)
                            {
                                EPREvents.PointUse(player, cost, PointUsage.Command);
                                player.TSPlayer.SendMessage(string.Format("Command cost you {0} {1}s. You have {2} {1}(s) left", cost, ServerPointSystem.ServerPointSystem.currname, (player.DisplayAccount)), Color.Red);
                                return;
                            }
                            else
                            {
                                player.TSPlayer.SendMessage(string.Format("You do not have enough {0}s to use that command", ServerPointSystem.ServerPointSystem.currname), Color.Red);
                                e.Handled = true;
                                return;
                            }
                        }
                    }
                }
            }
            return;
        }



        private static void CheckCommandCost(CommandArgs args)
        {
        }
        private static List<CommandParser> GetCommands(string start)
        {
            return CommandParserList.FindAll(item => item.Command.StartsWith(start.ToLower()));
        }
        private static void LoadCmdParsersFromText()
        {

            string[] CmdParserDataPaths = Directory.GetFiles(@CmdParserDataDirectory);

            foreach (string CMDataPath in CmdParserDataPaths)
            {
                //Console.WriteLine("Loading Monster from {0}...", CMDataPath);
                #region Variables
                string Command = "";
                int Cost = 0;
                BlockType bt = BlockType.NoBlock;
                ChargeType ct = ChargeType.EqualsTo;
                string costoverride = "";
                string blockoverride = "";
                #endregion
                List<string> CmdPrsrData = new List<string>();
                try
                {
                    using (StreamReader sr = new StreamReader(CMDataPath))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (!line.StartsWith("##"))
                                CmdPrsrData.Add(line);
                        }
                    }
                }
                catch (Exception e)
                {
                    string errormessage = string.Format("The file \"{0}\" could not be read:", CMDataPath);
                    Console.WriteLine(errormessage);
                    Console.WriteLine(e.Message);
                }
                foreach (string cmdprsrdata in CmdPrsrData)
                {
                    string field = cmdprsrdata.Split(':')[0];
                    string val ="";
                    for(int i = 1; i< cmdprsrdata.Split(':').Length;i++)
                    {
                        val += cmdprsrdata.Split(':')[i];
                    }
                    if(val!="")
                    {
                        #region switch block // just cuz i dun like it
                        switch (field.ToLower())
                        {
                            case "cost":
                            case "price":
                                {
                                    Int32.TryParse(val, out Cost);
                                    break;
                                }
                            case "commandname":
                            case "command":
                                {
                                    if (val.StartsWith("/"))
                                        Command = val;
                                    else
                                        Command = "/" + val;
                                    break;
                                }
                            case "bt":
                            case "block":
                            case "blocktype":
                                {
                                    switch (val.ToLower())
                                    {
                                        case "startswith":
                                        case "startwith":
                                        case "starts":
                                        case "start":
                                            {
                                                bt = BlockType.BlockStartsWith;
                                                break;
                                            }
                                        case "equalsto":
                                        case "equalto":
                                        case "equal":
                                        case "equals":
                                            default:
                                            {
                                                bt = BlockType.BlockEqualsTo;
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case "chargetype":
                            case "costtype":
                            case "type":
                                {
                                    switch (val.ToLower())
                                    {
                                        case "startswith":
                                        case "startwith":
                                        case "starts":
                                        case "start":
                                            {
                                                ct = ChargeType.StartsWith;
                                                break;
                                            }
                                        case "equalsto":
                                        case "equalto":
                                        case "equal":
                                        case "equals":
                                            {
                                                ct = ChargeType.EqualsTo;
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case"costoverride":
                                costoverride = val;
                                break;
                            case "blockoverride":
                                blockoverride = val;
                                break;
                            default:
                                break;
                        }
                        #endregion
                    }
                }
                CommandParserList.Add(new CommandParser(Command, Cost, ct, costoverride, bt, blockoverride));

            }





        }
    }
}