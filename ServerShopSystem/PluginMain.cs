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


namespace ServerShopSystem
{
    [APIVersion(1, 11)]
    public class ServerShopSystem : TerrariaPlugin
    {
        private static string ShopLogs = Path.Combine(TShock.SavePath, "ShopLogs");
        private static string ShopLogSavePath = Path.Combine(ShopLogs, "ShopLogs.txt");
        private static SqlTableEditor SQLEditor;
        private static SqlTableCreator SQLWriter;
        private static List<ServerShopCatalogueItem> SSCatalogue = new List<ServerShopCatalogueItem>();
        private static List<string> ShopList = new List<string>();
        private static List<ServerShopCatalogueItem> SSCatalogueInStock = new List<ServerShopCatalogueItem>();
        private static SSConfigFile SSConfig { get; set; }
        internal static string SSConfigPath { get { return Path.Combine(TShock.SavePath, "ssconfig.json"); } }
        private static List<string> BuyRatePermissions = new List<string>();
        private static List<double> BuyRates = new List<double>();
        private static List<SSPlayer> SSPlayers = new List<SSPlayer>();
        public override string Name
        {
            get { return "ServerShopSystem"; }
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
        public ServerShopSystem(Main game)
            : base(game)
        {
            SSConfig = new SSConfigFile();
        }

        public void OnInitialize()
        {
            SetupConfig();

            for (int i = 0; i < SSConfig.BuyRatePermissions.Length; i++)
            {
                BuyRatePermissions.Add(SSConfig.BuyRatePermissions[i]);
                BuyRates.Add(SSConfig.BuyRates[i]);
            }

            if (!Directory.Exists(ShopLogs))
            {
                Directory.CreateDirectory(ShopLogs);
            }

            if (!File.Exists(ShopLogSavePath))
            {
                File.Create(ShopLogSavePath).Close();
            }

            bool serverbuy = false;
            bool seehiddenprices = false;
            bool buy = false;
            bool buyrate = false;
            foreach (Group group in TShock.Groups.groups)
            {
                if (group.Name != "superadmin")
                {
                    if (group.HasPermission("serverbuy"))
                        serverbuy = true;
                    if (group.HasPermission("seehiddenprices"))
                        seehiddenprices = true;
                    if (group.HasPermission("buy"))
                        buy = true;
                    if (group.HasPermission("buyrate"))
                        buyrate = true;
                }
            }

            List<string> permlist = new List<string>();
            if (!serverbuy)
                permlist.Add("serverbuy");
            if (!seehiddenprices)
                permlist.Add("seehiddenprices");
            if (!buy)
                permlist.Add("buy");
            if (!buyrate)
                permlist.Add("buyrate");

            bool setshop = false;
            foreach (Group group in TShock.Groups.groups)
            {
                if (group.Name != "superadmin")
                {
                    if (group.HasPermission("setshop"))
                        setshop = true;
                }
            }
            if (!setshop)
                permlist.Add("setshop");

            TShock.Groups.AddPermissions("trustedadmin", permlist);
            Commands.ChatCommands.Add(new Command("buy", Buy, "buy"));
            Commands.ChatCommands.Add(new Command("serverbuy", Servershop, "servershop"));
            Commands.ChatCommands.Add(new Command("serverbuy", Servershop, "ss"));
            Commands.ChatCommands.Add(new Command("setshop", Setshop, "setshop"));
            Commands.ChatCommands.Add(new Command("buyrate", BuyRate, "buyrate"));
            SQLEditor = new SqlTableEditor(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            SQLWriter = new SqlTableCreator(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            var table = new SqlTable("ServerShopCatalogue",
                new SqlColumn("ID", MySqlDbType.Int32) { Primary = true },
                new SqlColumn("Name", MySqlDbType.String, 255) { Unique = true },
                new SqlColumn("Price", MySqlDbType.Int32),
                new SqlColumn("MaxStack", MySqlDbType.Int32),
                new SqlColumn("InStock", MySqlDbType.Int32),
                new SqlColumn("ShopName", MySqlDbType.String, 255),
                new SqlColumn("Hidden", MySqlDbType.Int32),
                new SqlColumn("Permission", MySqlDbType.String, 255)
            );
            SQLWriter.EnsureExists(table);
            string alpha = "abcdefghijklmnopqrstuvwkyz";
            if (SQLEditor.ReadColumn("ServerShopCatalogue", "ID", new List<SqlValue>()).Count < 1)
            {
                Console.WriteLine("Creating Item Price List...");
                foreach (char letter in alpha)
                //for (Int32 i = 1; i < 364; i++ )
                {
                    string firstletter = "" + letter + "";
                    for (int k = 0; k < TShockAPI.TShock.Utils.GetItemByName(firstletter).Count; k++)
                    //for (Int32 k = 0; k < TShockAPI.TShock.Utils.GetItemByIdOrName(i.ToString()).Count; k++)
                    {
                        Item item = TShockAPI.TShock.Utils.GetItemByName(firstletter)[k];
                        int price = item.value / 5;
                        int i = item.type;
                        string space = " ";
                        string itemID = i.ToString();
                        string itemname = item.name;
                        string apostrophe = "'";
                        char[] apostrophetochar = apostrophe.ToCharArray();
                        itemname = itemname.Replace(apostrophetochar[0], ' ');
                        int itemmaxstack = item.maxStack;
                        List<SqlValue> list = new List<SqlValue>();
                        list.Add(new SqlValue("ID", i));
                        list.Add(new SqlValue("Name", "'" + itemname + "'"));
                        list.Add(new SqlValue("Price", price));
                        list.Add(new SqlValue("MaxStack", itemmaxstack));
                        list.Add(new SqlValue("InStock", 1));
                        list.Add(new SqlValue("ShopName", "'" + space + "'"));
                        list.Add(new SqlValue("Hidden", 0));
                        try
                        {
                            SQLEditor.InsertValues("ServerShopCatalogue", list);
                        }
                        catch (Exception) { }


                    }
                }
            }
            if (SQLEditor.ReadColumn("ServerShopCatalogue", "ID", new List<SqlValue>()).Count > 0)
            {
                Console.WriteLine("Loading Item Price List...");
                for (int i = 0; i < SQLEditor.ReadColumn("ServerShopCatalogue", "ID", new List<SqlValue>()).Count; i++)
                {
                    int id = Int32.Parse(SQLEditor.ReadColumn("ServerShopCatalogue", "ID", new List<SqlValue>())[i].ToString());
                    List<SqlValue> where = new List<SqlValue>();
                    where.Add(new SqlValue("ID", id));
                    string name = SQLEditor.ReadColumn("ServerShopCatalogue", "Name", where)[0].ToString();
                    int price = Int32.Parse(SQLEditor.ReadColumn("ServerShopCatalogue", "Price", where)[0].ToString());
                    bool instock = Int32.Parse(SQLEditor.ReadColumn("ServerShopCatalogue", "InStock", where)[0].ToString()) == 0 ? false : true;
                    string shopname = SQLEditor.ReadColumn("ServerShopCatalogue", "ShopName", where)[0].ToString();
                    bool hidden = Int32.Parse(SQLEditor.ReadColumn("ServerShopCatalogue", "Hidden", where)[0].ToString()) == 0 ? false : true;
                    string permission;
                    try
                    {
                        permission = SQLEditor.ReadColumn("ServerShopCatalogue", "Permission", where)[0].ToString();
                    }
                    catch (NullReferenceException)
                    {
                        permission = null;
                    }
                    SSCatalogue.Add(new ServerShopCatalogueItem(id, name, price, instock, shopname, permission, hidden));
                    if (!ShopList.Contains(shopname))
                        ShopList.Add(shopname);
                }
                for (int i = 0; i < ShopList.Count; i++ )
                {
                    if (ShopList[i] == " ")
                        ShopList.RemoveAt(i);
                }
            }
            SSCatalogueInStock = GetItemsInStock();
        }




        public void OnUpdate()
        {
        }

        public void OnGreetPlayer(int who, HandledEventArgs e)
        {
            SSPlayers.Add(new SSPlayer(who));
        }

        public void OnLeave(int ply)
        {
            lock (SSPlayers)
            {
                for (int i = 0; i < SSPlayers.Count; i++)
                {
                    if (SSPlayers[i].Index == ply)
                    {
                        SSPlayers.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        public void OnChat(messageBuffer msg, int ply, string text, HandledEventArgs e)
        {
        }

        private static void Servershop(CommandArgs args)
        {
            string cmd = "help";
            if (args.Parameters.Count > 0)
            {
                if (args.Player.IsLoggedIn)
                {
                    EPRPlayer player = ServerPointSystem.ServerPointSystem.GetEPRPlayerByIndex(args.Player.Index);
                    cmd = args.Parameters[0].ToLower();
                    switch (cmd)
                    {
                        case "shoplist":
                            #region
                            {
                                args.Player.SendMessage("Retrieving data...", Color.Pink);
                                int page = 1;
                                if (args.Parameters.Count > 1)
                                    Int32.TryParse(args.Parameters[1], out page);
                                #region Hide messy commented out block of code
                                //List<string> shops = new List<string>();
                                //List<SqlValue> where = new List<SqlValue>();
                                //where.Add(new SqlValue("InStock", 1));
                                //shops.Add("dummyvalue");
                                //shops[0] = SQLEditor.ReadColumn("ServerShopCatalogue", "ShopName", where)[0].ToString();
                                //for (int i = 1; i < SQLEditor.ReadColumn("ServerShopCatalogue", "ShopName", where).Count; i++)
                                //{
                                //    if (!shops.Contains(SQLEditor.ReadColumn("ServerShopCatalogue", "ShopName", where)[i].ToString()) && SQLEditor.ReadColumn("ServerShopCatalogue", "ShopName", where)[i].ToString() != " ")
                                //        shops.Add(SQLEditor.ReadColumn("ServerShopCatalogue", "ShopName", where)[i].ToString());
                                //}
                                //if (shops[0] == " ")
                                //    shops.RemoveAt(0);
                                #endregion
                                if (ShopList.Count > 0)
                                {
                                    StringBuilder sb = new StringBuilder();
                                    foreach (string shop in ShopList)
                                    {
                                        int j = 0;
                                        while (j < 18 && (((page - 1) * 18) + j) < ShopList.Count)
                                        {
                                            int k = 0;
                                            while (k < 3 && (((page - 1) * 18) + j) < ShopList.Count)
                                            {
                                                sb.Append(ShopList[((page - 1) * 18) + j]);
                                                j++;
                                                k++;
                                            }
                                            //sbmsg.AppendLine(sbline.ToString());
                                            args.Player.SendMessage(sb.ToString(), Color.Yellow);
                                            sb.Clear();
                                        }
                                        int pagecount = (ShopList.Count / 18) + 1;
                                        //args.Player.SendMessage(sbmsg.ToString(), Color.Yellow);
                                        if (page < pagecount)
                                            args.Player.SendMessage("Found what you were looking for? If not, type \"/servershop shoplist " + (page + 1).ToString() + " \"" + "[" + page.ToString() + "/" + pagecount.ToString() + "]", Color.YellowGreen);

                                    }
                                }
                                else
                                    args.Player.SendMessage("There are currently no Shop Regions Set up", Color.Red);

                            }
                            break;
                            #endregion
                        case "masterpricelist":
                            #region
                            {
                                int page = 1;
                                if (args.Parameters.Count > 1)
                                    Int32.TryParse(args.Parameters[1], out page);
                                int i = 0;
                                var sbline = new StringBuilder();
                                List<ServerShopCatalogueItem> MasterList = new List<ServerShopCatalogueItem>();
                                if (args.Player.Group.HasPermission("seehiddenprices"))
                                    MasterList = SSCatalogueInStock;
                                else
                                {
                                    foreach (ServerShopCatalogueItem item in SSCatalogueInStock)
                                    {
                                        bool CheckPermission = item.Permission != null ? true : args.Player.Group.HasPermission(item.Permission);
                                        if (!item.Hidden && CheckPermission)
                                        {
                                            MasterList.Add(item);
                                        }
                                    }
                                }
                                if (MasterList.Count == 0)
                                    args.Player.SendMessage("Prices are hidden", Color.Red);
                                while (i < 18 && (((page - 1) * 18) + i) < MasterList.Count)
                                {
                                    int k = 0;
                                    while (k < 3 && (((page - 1) * 18) + i) < MasterList.Count)
                                    {
                                        int buyprice = (int)(MasterList[(((page - 1) * 18) + i)].Price * GetBuyRate(args.Player.Index));
                                        sbline.Append(MasterList[(((page - 1) * 18) + i)].Name + " " + buyprice.ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "(s), ");
                                        i++;
                                        k++;
                                    }
                                    //sbmsg.AppendLine(sbline.ToString());
                                    args.Player.SendMessage(sbline.ToString(), Color.Yellow);
                                    sbline.Clear();
                                }
                                int pagecount = (SSCatalogueInStock.Count / 18) + 1;
                                //args.Player.SendMessage(sbmsg.ToString(), Color.Yellow);
                                if (page < pagecount)
                                    args.Player.SendMessage("Found what you were looking for? If not, type \"/servershop masterpricelist " + (page + 1).ToString() + " \"" + "[" + page.ToString() + "/" + pagecount.ToString() + "]", Color.YellowGreen);
                                break;
                            }
                            #endregion
                        case "shoppricelist":
                            #region
                            {
                                if (args.Parameters.Count > 1)
                                {
                                    //bool RegionIsAShop = false;
                                    //for (int j = 0; j < SQLEditor.ReadColumn("ServerShopCatalogue", "ShopName", new List<SqlValue>()).Count; j++)
                                    //{
                                    //    if (args.Parameters[1] == SQLEditor.ReadColumn("ServerShopCatalogue", "ShopName", new List<SqlValue>())[j].ToString())
                                    //        RegionIsAShop = true;
                                    //}
                                    if (!RegionIsAShop(args.Parameters[1]))
                                    {
                                        args.Player.SendMessage("That place doesnt sell any items!", Color.Red);
                                    }
                                    else
                                    {
                                        int page = 1;
                                        if (args.Parameters.Count > 2)
                                            Int32.TryParse(args.Parameters[2], out page);
                                        //List<SqlValue> where = new List<SqlValue>();
                                        //where.Add(new SqlValue("InStock", 1));
                                        //where.Add(new SqlValue("ShopName", "'" + args.Parameters[1] + "'"));
                                        List<ServerShopCatalogueItem> ShopItems = new List<ServerShopCatalogueItem>();
                                        foreach (ServerShopCatalogueItem item in SSCatalogueInStock)
                                        {
                                            bool CheckPermission = item.Permission != null ? true : args.Player.Group.HasPermission(item.Permission);
                                            bool SeeHidden = (args.Player.Group.HasPermission("seehiddenprices") || (CheckPermission && !item.Hidden));
                                            if (item.InStock && item.ShopName == args.Parameters[1] && SeeHidden)
                                                ShopItems.Add(item);
                                        }
                                        int i = 0;
                                        var sbline = new StringBuilder();
                                        if (ShopItems.Count == 0)
                                            args.Player.SendMessage("Prices are hidden", Color.Red);
                                        while (i < 18 && (((page - 1) * 18) + i) < ShopItems.Count)
                                        {
                                            int k = 0;
                                            while (k < 3 && (((page - 1) * 18) + i) < ShopItems.Count)
                                            {
                                                int buyprice = (int)(ShopItems[(((page - 1) * 18) + i)].Price * GetBuyRate(args.Player.Index));
                                                sbline.Append(ShopItems[(((page - 1) * 18) + i)].Name + " " + buyprice + " " + ServerPointSystem.ServerPointSystem.currname + "(s), ");
                                                i++;
                                                k++;
                                            }
                                            args.Player.SendMessage(sbline.ToString(), Color.Yellow);
                                            sbline.Clear();
                                        }
                                        int pagecount = (ShopItems.Count / 18) + 1;
                                        if (page < pagecount)
                                            args.Player.SendMessage("Found what you were looking for? If not, type \"/servershop shoppricelist " + args.Parameters[1] + " " + (page + 1).ToString() + " \"" + "[" + page.ToString() + "/" + pagecount.ToString() + "]", Color.YellowGreen);
                                    }
                                }
                                else
                                {
                                    args.Player.SendMessage("/servershop shoppricelist [shop name]", Color.Yellow);
                                }
                                break;
                            }
                            #endregion
                        case "price":
                            #region
                            {
                                if (args.Parameters.Count > 1)
                                {
                                    Item bought;
                                    int amount = 0;
                                    if (TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1].ToString()).Count == 1)
                                    {
                                        bought = TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1].ToString())[0];
                                        if (args.Parameters.Count == 3)
                                        {
                                            amount = Convert.ToInt32(args.Parameters[2].ToString());
                                            if (amount > bought.maxStack)
                                            {
                                                amount = bought.maxStack;
                                            }
                                        }
                                        else
                                        {
                                            amount = 1;
                                        }
                                        //List<SqlValue> where2 = new List<SqlValue>();
                                        //where2.Add(new SqlValue("ID", "'" + bought.type + "'"));
                                        ServerShopCatalogueItem ssibought = GetSSItemByID(bought.type);
                                        int price = (int)(ssibought.Price * GetBuyRate(args.Player.Index));
                                        bool CheckPermission = ssibought.Permission != null ? true : args.Player.Group.HasPermission(ssibought.Permission);
                                        bool SeeHidden = (args.Player.Group.HasPermission("seehiddenprices") || (CheckPermission && !ssibought.Hidden));
                                        if (ssibought.InStock && SeeHidden)
                                        {
                                            if (TShock.Regions.InAreaRegionName(args.Player.TileX, args.Player.TileY).Contains(ssibought.ShopName) && ssibought.ShopName != " ")
                                            {
                                                args.Player.SendMessage("" + amount + " " + bought.name + " costs " + price * amount + " " + ServerPointSystem.ServerPointSystem.currname + "s and it can be bought here", Color.Yellow);
                                            }
                                            else if (!TShock.Regions.InAreaRegionName(args.Player.TileX, args.Player.TileY).Contains(ssibought.ShopName) && ssibought.ShopName != " ")
                                            {
                                                args.Player.SendMessage("" + amount + " " + bought.name + " costs " + price * amount + " " + ServerPointSystem.ServerPointSystem.currname + "s and is sold at " + ssibought.ShopName + " ", Color.Yellow);
                                            }
                                            else if (ssibought.ShopName == " ")
                                            {
                                                args.Player.SendMessage("" + amount + " " + bought.name + " costs " + price * amount + " " + ServerPointSystem.ServerPointSystem.currname + "s and it can be bought from anywhere", Color.Yellow);
                                            }
                                        }
                                        else if (ssibought.InStock && !SeeHidden)
                                        {
                                            args.Player.SendMessage("Price is Hidden", Color.Red);
                                        }
                                        else
                                        {
                                            args.Player.SendMessage("Item not for sale", Color.Red);
                                        }
                                        
                                    }
                                    else if (TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1].ToString()).Count > 1)
                                    {
                                        args.Player.SendMessage("More than 1 item matched! Be more specific please", Color.Red);
                                        args.Player.SendMessage("Example: please type 'Dirt Block' instead of 'Dirt', because there is also an item called 'Dirt Wall'", Color.Red);
                                    }
                                    else
                                    {
                                        args.Player.SendMessage("There is no such item", Color.Red);
                                    }
                                }
                                else
                                {
                                    args.Player.SendMessage("/servershop price [item name] [amount]", Color.Yellow);
                                    args.Player.SendMessage("or /ss price [item name] [amount]", Color.Yellow);
                                }
                                break;
                            }
                            #endregion
                        case "buy":
                            #region
                            {
                                if (args.Parameters.Count > 1)
                                {
                                    if (args.Player.InventorySlotAvailable)
                                    {
                                        Item bought;
                                        int amount = 0;
                                        if (TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1].ToString()).Count == 1)
                                        {
                                            bought = TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1].ToString())[0];
                                            if (args.Parameters.Count == 3)
                                            {
                                                amount = Convert.ToInt32(args.Parameters[2].ToString());
                                                if (amount > bought.maxStack)
                                                {
                                                    amount = bought.maxStack;
                                                }
                                            }
                                            else
                                            {
                                                amount = bought.maxStack;
                                            }
                                            ServerShopCatalogueItem ssibought = GetSSItemByID(bought.type);
                                            bool CheckPermission = ssibought.Permission != null ? true : args.Player.Group.HasPermission(ssibought.Permission);
                                            int currbal = player.DisplayAccount;
                                            int price = (int)(ssibought.Price * GetBuyRate(args.Player.Index));
                                            if (ssibought.InStock && CheckPermission)
                                            {
                                                if (TShock.Regions.InAreaRegionName(args.Player.TileX, args.Player.TileY).Contains(ssibought.ShopName) && ssibought.ShopName != " ")
                                                {
                                                    if (currbal >= (price * amount))
                                                    {
                                                        args.Player.GiveItem(bought.type, bought.name, bought.width, bought.height, amount);
                                                        ServerPointSystem.EPREvents.PointUse(player, price * amount, PointUsage.Shop);
                                                        args.Player.SendMessage("Transaction complete! You have " + (player.DisplayAccount - price * amount).ToString() + " " + ServerPointSystem.ServerPointSystem.currname + "s left", Color.Green);
                                                        string[] PayLog = new string[1];
                                                        PayLog[0] = DateTime.UtcNow.ToString() + " " + args.Player.Name + " bought " + amount + " " + bought.name + "(s) for " + price + ServerPointSystem.ServerPointSystem.currname + "s each";
                                                        File.AppendAllLines(ShopLogSavePath, PayLog);
                                                    }
                                                    else
                                                    {
                                                        args.Player.SendMessage("You don't have enough " + ServerPointSystem.ServerPointSystem.currname + "s!", Color.Red);
                                                    }
                                                }
                                                else if (!TShock.Regions.InAreaRegionName(args.Player.TileX, args.Player.TileY).Contains(ssibought.ShopName) && ssibought.ShopName != " ")
                                                {
                                                    args.Player.SendMessage("" + amount + " " + bought.name + " costs " + price * amount + " " + ServerPointSystem.ServerPointSystem.currname + "s and is sold at " + ssibought.ShopName + " ", Color.Yellow);
                                                }
                                                else if (ssibought.ShopName == " ")
                                                {
                                                    if (currbal >= (price * amount))
                                                    {
                                                        args.Player.GiveItem(bought.type, bought.name, bought.width, bought.height, amount);
                                                        EPREvents.PointUse(player, price * amount, PointUsage.Shop);
                                                        args.Player.SendMessage("Transaction complete! You have " + player.DisplayAccount + " " + ServerPointSystem.ServerPointSystem.currname + "s left", Color.Green);
                                                        string[] PayLog = new string[1];
                                                        PayLog[0] = DateTime.UtcNow.ToString() + args.Player.Name + " bought " + amount + " " + bought.name + "(s) for " + price + ServerPointSystem.ServerPointSystem.currname + "s each";
                                                        File.AppendAllLines(ShopLogSavePath, PayLog);
                                                    }
                                                    else
                                                    {
                                                        args.Player.SendMessage("You don't have enough " + ServerPointSystem.ServerPointSystem.currname + "s!", Color.Red);
                                                    }
                                                }

                                            }
                                            else if (ssibought.InStock && !CheckPermission)
                                            {
                                                args.Player.SendMessage("You're not allowed to buy that item", Color.Red);
                                            }
                                            else
                                            {
                                                args.Player.SendMessage("Item not for sale", Color.Red);
                                            }
                                        }
                                        else if (TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1].ToString()).Count > 1)
                                        {
                                            args.Player.SendMessage("More than 1 item matched! Be more specific please", Color.Red);
                                            args.Player.SendMessage("Example: please type 'Dirt Block' instead of 'Dirt', because there is also an item called 'Dirt Wall'", Color.Red);
                                        }
                                        else
                                        {
                                            args.Player.SendMessage("There is no such item", Color.Red);
                                        }
                                    }
                                    else
                                        args.Player.SendMessage("You don't have enough space!", Color.Red);
                                }
                                else
                                {
                                    args.Player.SendMessage("/servershop buy [item name] [amount]", Color.Yellow);
                                    args.Player.SendMessage("or /ss buy [item name] [amount]", Color.Yellow);
                                }
                                break;
                            }
                            #endregion
                        default:
                            {
                                args.Player.SendMessage("/servershop buy [item name] [amount]", Color.Yellow);
                                //args.Player.SendMessage("or /ss buy [item name] [amount]", Color.Yellow);
                                args.Player.SendMessage("/servershop price [item name] [amount]", Color.Yellow);
                                //args.Player.SendMessage("or /ss price [item name] [amount]", Color.Yellow);
                                args.Player.SendMessage("/servershop masterpricelist", Color.Yellow);
                                args.Player.SendMessage("/servershop shoplist", Color.Yellow);
                                args.Player.SendMessage("/servershop shoppricelist [shop name]", Color.Yellow);
                                args.Player.SendMessage("or you can replace /servershop with /ss", Color.Yellow);
                                break;
                            }
                    }
                }
                else
                {
                    args.Player.SendMessage("You must be logged in to do that!", Color.Red);
                }
            }
        }
        private static void Buy(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                if (args.Player.InventorySlotAvailable)
                {
                    Item bought;
                    int amount = 0;
                    if (TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[0].ToString()).Count == 1)
                    {
                        bought = TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[0].ToString())[0];
                        if (args.Parameters.Count == 2)
                        {
                            amount = Convert.ToInt32(args.Parameters[1].ToString());
                            if (amount > bought.maxStack)
                            {
                                amount = bought.maxStack;
                            }
                        }
                        else
                        {
                            amount = bought.maxStack;
                        }
                        ServerShopCatalogueItem ssibought = GetSSItemByID(bought.type);
                        bool CheckPermission = ssibought.Permission != null ? true : args.Player.Group.HasPermission(ssibought.Permission);
                        List<SqlValue> where = new List<SqlValue>();
                        //List<SqlValue> where2 = new List<SqlValue>();
                        where.Add(new SqlValue("name", "'" + args.Player.UserAccountName + "'"));
                        //where2.Add(new SqlValue("ID", "'" + bought.type + "'"));
                        int currbal = Int32.Parse(SQLEditor.ReadColumn("ServerPointAccounts", "amount", where)[0].ToString());
                        int price = (int)(ssibought.Price * GetBuyRate(args.Player.Index));
                        int finalbal = 0;
                        if (ssibought.InStock && CheckPermission)
                        {
                            if (TShock.Regions.InAreaRegionName(args.Player.TileX, args.Player.TileY).Contains(ssibought.ShopName) && ssibought.ShopName != " ")
                            {
                                if (currbal >= (price * amount))
                                {
                                    args.Player.GiveItem(bought.type, bought.name, bought.width, bought.height, amount);
                                    finalbal = currbal - (price * amount);
                                    List<SqlValue> values = new List<SqlValue>();
                                    values.Add(new SqlValue("amount", "'" + finalbal + "'"));
                                    SQLEditor.UpdateValues("ServerPointAccounts", values, where);
                                    args.Player.SendMessage("Transaction complete! You have " + finalbal + " " + ServerPointSystem.ServerPointSystem.currname + "s left", Color.Green);
                                    string[] PayLog = new string[1];
                                    PayLog[0] = DateTime.UtcNow.ToString() + args.Player.Name + " bought " + amount + " " + bought.name + "(s) for " + price + ServerPointSystem.ServerPointSystem.currname + "s each";
                                    File.AppendAllLines(ShopLogSavePath, PayLog);
                                }
                                else
                                {
                                    args.Player.SendMessage("You don't have enough " + ServerPointSystem.ServerPointSystem.currname + "s!", Color.Red);
                                }
                            }
                            else if (!TShock.Regions.InAreaRegionName(args.Player.TileX, args.Player.TileY).Contains(ssibought.ShopName) && ssibought.ShopName != " ")
                            {
                                args.Player.SendMessage("" + amount + " " + bought.name + " costs " + price * amount + " " + ServerPointSystem.ServerPointSystem.currname + "s and is sold at " + ssibought.ShopName + " ", Color.Yellow);
                            }
                            else if (ssibought.ShopName == " ")
                            {
                                if (currbal >= (price * amount))
                                {
                                    args.Player.GiveItem(bought.type, bought.name, bought.width, bought.height, amount);
                                    finalbal = currbal - (price * amount);
                                    List<SqlValue> values = new List<SqlValue>();
                                    values.Add(new SqlValue("amount", "'" + finalbal + "'"));
                                    SQLEditor.UpdateValues("ServerPointAccounts", values, where);
                                    args.Player.SendMessage("Transaction complete! You have " + finalbal + " " + ServerPointSystem.ServerPointSystem.currname + "s left", Color.Green);
                                    string[] PayLog = new string[1];
                                    PayLog[0] = DateTime.UtcNow.ToString() + args.Player.Name + " bought " + amount + " " + bought.name + "(s) for " + price + ServerPointSystem.ServerPointSystem.currname + "s each";
                                    File.AppendAllLines(ShopLogSavePath, PayLog);
                                }
                                else
                                {
                                    args.Player.SendMessage("You don't have enough " + ServerPointSystem.ServerPointSystem.currname + "s!", Color.Red);
                                }
                            }

                        }
                        else if (ssibought.InStock && !CheckPermission)
                        {
                            args.Player.SendMessage("You're not allowed to buy that item", Color.Red);
                        }
                        else
                        {
                            args.Player.SendMessage("Item not for sale", Color.Red);
                        }
                    }
                    else if (TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[0].ToString()).Count > 1)
                    {
                        args.Player.SendMessage("More than 1 item matched! Be more specific please", Color.Red);
                        args.Player.SendMessage("Example: please type 'Dirt Block' instead of 'Dirt', because there is also an item called 'Dirt Wall'", Color.Red);
                    }
                    else
                    {
                        args.Player.SendMessage("There is no such item", Color.Red);
                    }
                }
                else
                    args.Player.SendMessage("You don't have enough space!", Color.Red);
            }
            else
            {
                args.Player.SendMessage("/buy [item name] [amount]", Color.Yellow);
            }
        }
        private static void BuyRate(CommandArgs args)
        {
            if (args.Parameters.Count > 1)
            {
                if (TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count == 1)
                {
                    TSPlayer player = TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0])[0];
                    double buyrate = GetBuyRate(player.Index);
                    if (double.TryParse(args.Parameters[1], out buyrate))
                    {
                        GetSSPlayerByID(player.Index).PrivateBuyRate = buyrate;
                        GetSSPlayerByID(player.Index).PBREnabled = true;
                        args.Player.SendMessage("You changed " + player.Name + "'s Buy Rate to " + buyrate, Color.Green);
                        player.SendMessage(args.Player.Name + " has changed the price rate at which you can buy items to " + buyrate, Color.Green);
                    }
                    else if (args.Parameters[1] == "off")
                    {
                        GetSSPlayerByID(player.Index).PBREnabled = false;
                        args.Player.SendMessage("You disabled " + player.Name + "'s Private Buy Rate to ", Color.Green);
                        player.SendMessage(args.Player.Name + " has reset the price rate at which you can buy items ", Color.Green);
                    }
                    else
                    {
                        args.Player.SendMessage("/buyrate [player] [buy rate -- multiplies the prices by this amount]", Color.Yellow);
                        args.Player.SendMessage("/buyrate [player] off", Color.Yellow);
                    }
                }
                else
                    args.Player.SendMessage(TShockAPI.TShock.Utils.FindPlayer(args.Parameters[0]).Count + " players matched!", Color.Red);
            }
            else
            {
                
                args.Player.SendMessage("/buyrate [player] [buy rate -- multiplies the prices by this amount]", Color.Yellow);
                args.Player.SendMessage("/buyrate [player] off", Color.Yellow);
            }
        }
        private static void Setshop(CommandArgs args)
        {
            string cmd = "help";
            if (args.Parameters.Count > 0)
            {
                cmd = args.Parameters[0].ToLower();
                switch (cmd)
                {
                    case "hideitem":
                        #region
                        {
                            if (args.Parameters.Count > 1)
                            {
                                if (TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1]).Count == 1)
                                {
                                    Item item = TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0];
                                    if (GetSSItemByID(item.type).Hidden)
                                    {
                                        GetSSItemByID(item.type).Hidden = false;
                                        List<SqlValue> where = new List<SqlValue>();
                                        List<SqlValue> list = new List<SqlValue>();
                                        where.Add(new SqlValue("ID", item.type));
                                        list.Add(new SqlValue("Hidden", 0));
                                        SQLEditor.UpdateValues("ServerShopCatalogue", list, where);
                                        args.Player.SendMessage(item.name + " is not a hidden shop item anymore", Color.Green);
                                    }
                                    else
                                    {
                                        GetSSItemByID(item.type).Hidden = true;
                                        List<SqlValue> where = new List<SqlValue>();
                                        List<SqlValue> list = new List<SqlValue>();
                                        where.Add(new SqlValue("ID", item.type));
                                        list.Add(new SqlValue("Hidden", 1));
                                        SQLEditor.UpdateValues("ServerShopCatalogue", list, where);
                                        args.Player.SendMessage(item.name + " is now a hidden shop item", Color.Green);
                                    }
                                }
                                else
                                    args.Player.SendMessage(TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1]).Count + " items matched!", Color.Red);
                            }
                            else
                                args.Player.SendMessage("/setshop hideitem [item name]", Color.Yellow);
                        }
                        break;
                        #endregion
                    case "hideshop":
                        #region
                        {
                            if (args.Parameters.Count > 1)
                            {
                                string shopname = args.Parameters[1];
                                if (RegionIsAShop(shopname))
                                {
                                    int hidden = 0;
                                    int shown = 0;
                                    args.Player.SendMessage("Editing values...", Color.MediumPurple);
                                    foreach (ServerShopCatalogueItem item in SSCatalogue)
                                    {
                                        if (item.ShopName == shopname)
                                        {
                                            if (item.Hidden)
                                            {
                                                item.Hidden = false;
                                                List<SqlValue> where = new List<SqlValue>();
                                                List<SqlValue> list = new List<SqlValue>();
                                                where.Add(new SqlValue("ID", item.ID));
                                                list.Add(new SqlValue("Hidden", 0));
                                                SQLEditor.UpdateValues("ServerShopCatalogue", list, where);
                                                shown++;
                                            }
                                            else
                                            {
                                                item.Hidden = true;
                                                List<SqlValue> where = new List<SqlValue>();
                                                List<SqlValue> list = new List<SqlValue>();
                                                where.Add(new SqlValue("ID", item.ID));
                                                list.Add(new SqlValue("Hidden", 1));
                                                SQLEditor.UpdateValues("ServerShopCatalogue", list, where);
                                                hidden++;
                                            }
                                        }
                                    }
                                    args.Player.SendMessage("Hidden items in " + shopname + ": " + hidden, Color.Green);
                                    args.Player.SendMessage("Shown items in " + shopname + ": " + shown, Color.Green);
                                }
                                else
                                    args.Player.SendMessage("That place is not a shop", Color.Red);
                            }
                            else
                                args.Player.SendMessage("/setshop hideshop [shop name]", Color.Yellow);
                        }
                        break;
                        #endregion
                    case "itempermission":
                        #region
                        {
                            if (args.Parameters.Count > 2)
                            {
                                if (TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1]).Count == 1)
                                {
                                    string permission = args.Parameters[2] == "remove" ? null : args.Parameters[2];
                                    Item item = TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0];
                                    GetSSItemByID(item.type).Permission = permission;
                                    List<SqlValue> where = new List<SqlValue>();
                                    List<SqlValue> list = new List<SqlValue>();
                                    where.Add(new SqlValue("ID", item.type));
                                    list.Add(new SqlValue("Permission", "'" + permission + "'"));
                                    SQLEditor.UpdateValues("ServerShopCatalogue", list, where);
                                    args.Player.SendMessage("Permissions changed", Color.MediumPurple);
                                }
                                else
                                    args.Player.SendMessage(TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1]).Count + " items matched!", Color.Red);
                            }
                            else
                            {
                                args.Player.SendMessage("/setshop itempermission [item name] [permission]", Color.Yellow);
                                args.Player.SendMessage("/setshop itempermission [item name] remove", Color.Yellow);
                            }
                        }
                        break;
                        #endregion
                    case "shoppermission":
                        #region
                        {
                            if (args.Parameters.Count > 2)
                            {
                                string shopname = args.Parameters[1];
                                if (RegionIsAShop(shopname))
                                {
                                    args.Player.SendMessage("Editing permissions...", Color.MediumPurple);
                                    foreach (ServerShopCatalogueItem item in SSCatalogue)
                                    {
                                        if (item.ShopName == shopname)
                                        {
                                            string permission = args.Parameters[2] == "remove" ? null : args.Parameters[2];
                                            item.Permission = permission;
                                            List<SqlValue> where = new List<SqlValue>();
                                            List<SqlValue> list = new List<SqlValue>();
                                            where.Add(new SqlValue("ID", item.ID));
                                            list.Add(new SqlValue("Permission", "'" + permission + "'"));
                                            SQLEditor.UpdateValues("ServerShopCatalogue", list, where);
                                        }
                                    }
                                    args.Player.SendMessage("Permissions changed", Color.MediumPurple);
                                }
                                else
                                    args.Player.SendMessage("That place is not a shop", Color.Red);
                            }
                            else
                            {
                                args.Player.SendMessage("/setshop shopermission [item name] [permission]", Color.Yellow);
                                args.Player.SendMessage("/setshop shoppermission [item name] remove", Color.Yellow);
                            }
                        }
                        break;
                        #endregion
                    case "close":
                        #region
                        if (args.Parameters.Count > 1)
                        {
                            //bool RegionIsAShop = false;
                            //for (int i = 0; i < SQLEditor.ReadColumn("ServerShopCatalogue", "ShopName", new List<SqlValue>()).Count; i++)
                            //    if (args.Parameters[1] == SQLEditor.ReadColumn("ServerShopCatalogue", "ShopName", new List<SqlValue>())[i].ToString())
                            //        RegionIsAShop = true;

                            if (RegionIsAShop(args.Parameters[1]))
                            {
                                List<SqlValue> where = new List<SqlValue>();
                                List<SqlValue> list = new List<SqlValue>();
                                where.Add(new SqlValue("ShopName", "'" + args.Parameters[1] + "'"));
                                list.Add(new SqlValue("ShopName", " "));
                                SQLEditor.UpdateValues("ServerShopCatalogue", list, where);
                                foreach (ServerShopCatalogueItem item in SSCatalogue)
                                {
                                    if (item.ShopName == args.Parameters[1])
                                        item.ShopName = " ";
                                }
                                for (int i = 0; i < ShopList.Count; i++)
                                {
                                    if (ShopList[i] == args.Parameters[1])
                                        ShopList.RemoveAt(i);
                                }
                                args.Player.SendMessage("'" + args.Parameters[1] + " is now Closed", Color.Red);
                            }
                            else
                                args.Player.SendMessage("" + args.Parameters[1] + " is not a Shop", Color.Red);
                        }
                        else
                            args.Player.SendMessage("/setshop close [ShopName]", Color.Yellow);
                        break;
                        #endregion
                    case "move":
                        #region
                        if (args.Parameters.Count > 2)
                        {
                            //bool RegionIsAShop = false;
                            //for (int i = 0; i < SQLEditor.ReadColumn("ServerShopCatalogue", "ShopName", new List<SqlValue>()).Count; i++)
                            //    if (args.Parameters[1] == SQLEditor.ReadColumn("ServerShopCatalogue", "ShopName", new List<SqlValue>())[i].ToString())
                            //        RegionIsAShop = true;
                            //bool RegionExists = false;
                            //for (int i = 0; i < SQLEditor.ReadColumn("Regions", "RegionName", new List<SqlValue>()).Count; i++)
                            //    if (args.Parameters[2] == SQLEditor.ReadColumn("Regions", "RegionName", new List<SqlValue>())[i].ToString())
                            //        RegionExists = true;
                            if (!RegionIsAShop(args.Parameters[1]) && args.Parameters[1] != " ")
                                args.Player.SendMessage("" + args.Parameters[1] + " is not a Shop", Color.Red);
                            if (!RegionExists(args.Parameters[2]))
                                args.Player.SendMessage("" + args.Parameters[2] + " doesn't exist. Use the /region commands to define it first", Color.Red);
                            if (RegionExists(args.Parameters[2]) && (RegionIsAShop(args.Parameters[1]) || args.Parameters[1] == " "))
                            {
                                List<SqlValue> where = new List<SqlValue>();
                                List<SqlValue> list = new List<SqlValue>();
                                where.Add(new SqlValue("ShopName", "'" + args.Parameters[1] + "'"));
                                list.Add(new SqlValue("ShopName", "'" + args.Parameters[2] + "'"));
                                SQLEditor.UpdateValues("ServerShopCatalogue", list, where);
                                args.Player.SendMessage("Moving items...", Color.Pink);
                                foreach (ServerShopCatalogueItem item in SSCatalogue)
                                {
                                    if (item.ShopName == args.Parameters[1])
                                        item.ShopName = args.Parameters[2];
                                }
                                if(!ShopList.Contains(args.Parameters[2]))
                                    ShopList.Add(args.Parameters[2]);
                                for (int i = 0; i < ShopList.Count; i++)
                                {
                                    if (ShopList[i] == args.Parameters[1])
                                        ShopList.RemoveAt(i);
                                }
                                args.Player.SendMessage("The Items from " + args.Parameters[1] + " will now be sold at " + args.Parameters[2] + "", Color.Green);
                            }
                        }
                        else
                            args.Player.SendMessage("/setshop move [OLD Shop] [NEW Shop]", Color.Yellow);
                        break;
                        #endregion
                    case "item":
                        #region
                        if (args.Parameters.Count > 2)
                        {
                            if (args.Parameters[2].ToLower() == "sellonstreet")
                            {
                                if (TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1].ToString()).Count == 1)
                                {
                                    List<SqlValue> where = new List<SqlValue>();
                                    List<SqlValue> list = new List<SqlValue>();
                                    where.Add(new SqlValue("ID", TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type));
                                    list.Add(new SqlValue("ShopName", " "));
                                    SQLEditor.UpdateValues("ServerShopCatalogue", list, where);
                                    bool flag1 = false;
                                    string oldshop = GetSSItemByID(TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type).ShopName;
                                    
                                    GetSSItemByID(TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type).ShopName = " ";
                                    foreach (ServerShopCatalogueItem item in SSCatalogueInStock)
                                    {
                                        if (item.ShopName == oldshop)
                                            flag1 = true;
                                    }
                                    if(!flag1)
                                    {
                                        for (int i = 0; i < ShopList.Count; i++)
                                        {
                                            if (ShopList[i] == oldshop)
                                                ShopList.RemoveAt(i);
                                        }
                                    }
                                    args.Player.SendMessage("'" + TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].name + "can now be bought anywhere ", Color.Green);
                                }
                                else
                                    args.Player.SendMessage("More than 1 item matched!", Color.Red);
                            }
                            else
                            {
                                //bool RegionExists = false;
                                //for (int i = 0; i < SQLEditor.ReadColumn("Regions", "RegionName", new List<SqlValue>()).Count; i++)
                                //    if (args.Parameters[2] == SQLEditor.ReadColumn("Regions", "RegionName", new List<SqlValue>())[i].ToString())
                                //        RegionExists = true;
                                if (RegionExists(args.Parameters[2]))
                                {
                                    if (TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1].ToString()).Count == 1)
                                    {
                                        List<SqlValue> where = new List<SqlValue>();
                                        List<SqlValue> list = new List<SqlValue>();
                                        where.Add(new SqlValue("ID", TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type));
                                        list.Add(new SqlValue("ShopName", "'" + args.Parameters[2] + "'"));
                                        SQLEditor.UpdateValues("ServerShopCatalogue", list, where);
                                        bool flag1 =false;
                                        string oldshop = GetSSItemByID(TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type).ShopName;
                                        foreach (ServerShopCatalogueItem item in SSCatalogueInStock)
                                        {
                                            if (item.ShopName == oldshop)
                                                flag1 = true;
                                        }
                                        if(!flag1)
                                        {
                                            for (int i = 0; i < ShopList.Count; i++)
                                            {
                                                if (ShopList[i] == oldshop)
                                                    ShopList.RemoveAt(i);
                                            }
                                        }
                                        GetSSItemByID(TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type).ShopName = args.Parameters[2];
                                        if (!ShopList.Contains(args.Parameters[2]))
                                            ShopList.Add(args.Parameters[2]);
                                        args.Player.SendMessage("" + TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].name + " is now sold at " + args.Parameters[2] + "", Color.Green);

                                    }
                                    else
                                        args.Player.SendMessage("More than 1 item matched!", Color.Red);
                                }
                            }
                        }
                        else
                        {
                            args.Player.SendMessage("/setshop item [item name] sellonstreet -will remove this item from its current shop", Color.Yellow);
                            args.Player.SendMessage("/setshop item [item name] [region name] -will add the item to a new shop", Color.Yellow);
                        }
                        break;
                        #endregion
                    case "itemprice":
                        #region
                        if (args.Parameters.Count > 2)
                        {
                            if (TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1]).Count == 1)
                            {
                                int priceset;
                                if (Int32.TryParse(args.Parameters[2], out priceset))
                                {
                                    List<SqlValue> where = new List<SqlValue>();
                                    List<SqlValue> list = new List<SqlValue>();
                                    where.Add(new SqlValue("ID", TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type));
                                    list.Add(new SqlValue("Price", priceset));
                                    SQLEditor.UpdateValues("ServerShopCatalogue", list, where);
                                    args.Player.SendMessage("" + TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].name + " will now be sold for " + priceset.ToString(), Color.Green);
                                    GetSSItemByID(TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type).Price = priceset;
                                }
                                else
                                    args.Player.SendMessage("/setshop itemprice [item] [price -- must be a number]", Color.Yellow);
                            }
                            else
                                args.Player.SendMessage("More than 1 item matched!", Color.Red);

                        }
                        else
                            args.Player.SendMessage("/setshop itemprice [item] [price]", Color.Yellow);
                        break;
                        #endregion
                    case "deliver":
                        #region
                        if (args.Parameters.Count > 1)
                        {
                            if (TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1]).Count == 1)
                            {
                                List<SqlValue> where = new List<SqlValue>();
                                List<SqlValue> list = new List<SqlValue>();
                                where.Add(new SqlValue("ID", TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type));
                                list.Add(new SqlValue("InStock", 1));
                                SQLEditor.UpdateValues("ServerShopCatalogue", list, where);
                                GetSSItemByID(TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type).InStock = true;
                                if(!ShopList.Contains(GetSSItemByID(TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type).ShopName))
                                    ShopList.Add(GetSSItemByID(TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type).ShopName);
                                TShockAPI.TShock.Utils.Broadcast("New Item In Stock: " + TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].name + "!", Color.Yellow);
                            }
                            else
                                args.Player.SendMessage("More than 1 item matched!", Color.Red);
                        }
                        else
                            args.Player.SendMessage("/setshop deliver [item]", Color.Yellow);
                        break;
                        #endregion
                    case "recall":
                        #region
                        if (args.Parameters.Count > 1)
                        {
                            if (TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1]).Count == 1)
                            {
                                List<SqlValue> where = new List<SqlValue>();
                                List<SqlValue> list = new List<SqlValue>();
                                where.Add(new SqlValue("ID", TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type));
                                list.Add(new SqlValue("InStock", 0));
                                GetSSItemByID(TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type).InStock = false;
                                SQLEditor.UpdateValues("ServerShopCatalogue", list, where);
                                bool flag1 = false;
                                string oldshop = GetSSItemByID(TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type).ShopName;
                                    
                                    GetSSItemByID(TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].type).ShopName = " ";
                                    foreach (ServerShopCatalogueItem item in SSCatalogueInStock)
                                    {
                                        if (item.ShopName == oldshop)
                                            flag1 = true;
                                    }
                                    if(!flag1)
                                    {
                                        for (int i = 0; i < ShopList.Count; i++)
                                        {
                                            if (ShopList[i] == oldshop)
                                                ShopList.RemoveAt(i);
                                        }
                                    }
                                TShockAPI.TShock.Utils.Broadcast("Item no longer for sale: " + TShockAPI.TShock.Utils.GetItemByIdOrName(args.Parameters[1])[0].name + "!", Color.Yellow);
                            }
                            else
                                args.Player.SendMessage("More than 1 item matched!", Color.Red);
                        }
                        else
                            args.Player.SendMessage("/setshop recall [item]", Color.Yellow);
                        break;
                        #endregion
                    default:
                        args.Player.SendMessage("try /setshop [itemprice|hideitem|hideshop|itempermission|shoppermission]", Color.Yellow);
                        args.Player.SendMessage("or /setshop [recall|deliver|close|move|item]", Color.Yellow);
                        break;
                }
            }
            else
            {
                args.Player.SendMessage("try /setshop [itemprice|hideitem|hideshop|itempermission|shoppermission]", Color.Yellow);
                args.Player.SendMessage("or /setshop [recall|deliver|close|move|item]", Color.Yellow);
            }
        }
        private static bool RegionIsAShop(string region)
        {
            if (ShopList.Contains(region))
                return true;
            else return false;
        }
        private static bool RegionExists(string region)
        {
            for (int i = 0; i < SQLEditor.ReadColumn("Regions", "RegionName", new List<SqlValue>()).Count; i++)
                if (region == SQLEditor.ReadColumn("Regions", "RegionName", new List<SqlValue>())[i].ToString())
                    return true;
            return false;
        }
        private static ServerShopCatalogueItem GetSSItemByID(int ID)
        {
            ServerShopCatalogueItem item = null;
            foreach (ServerShopCatalogueItem itemctr in SSCatalogue)
            {
                if (itemctr.ID == ID)
                    item = itemctr;
            }
            return item;
        }
        private static List<ServerShopCatalogueItem> GetItemsInStock()
        {
            List<ServerShopCatalogueItem> IIS = new List<ServerShopCatalogueItem>();
            foreach (ServerShopCatalogueItem item in SSCatalogue)
            {
                if (item.InStock)
                    IIS.Add(item);
            }
            return IIS;
        }
        private static void SetupConfig()
        {
            try
            {
                if (File.Exists(SSConfigPath))
                {
                    SSConfig = SSConfigFile.Read(SSConfigPath);
                    // Add all the missing config properties in the json file
                }
                SSConfig.Write(SSConfigPath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error in (SS) config file");
                Console.ForegroundColor = ConsoleColor.Gray;
                Log.Error("(SS) Config Exception");
                Log.Error(ex.ToString());
            }
        }
        private static SSPlayer GetSSPlayerByID(int ID)
        {
            SSPlayer player = null;
            foreach(SSPlayer playerctr in SSPlayers)
            {
                if(playerctr.Index == ID)
                {
                    player = playerctr;
                    break;
                }
            }
            return player;
        }
        private static double GetBuyRate(int ID)
        {
            double BuyRate = 1;
            if (GetSSPlayerByID(ID).PBREnabled)
            {
                BuyRate = GetSSPlayerByID(ID).PrivateBuyRate;
            }
            else
            {
                for (int i = 0; i < BuyRatePermissions.Count; i++)
                {
                    if (TShock.Players[ID].Group.HasPermission(BuyRatePermissions[i]))
                    {
                        BuyRate = BuyRates[i];
                        break;
                    }
                }
            }
            return BuyRate;
        }

    }
}