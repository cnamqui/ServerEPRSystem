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

namespace ServerShopSystem
{
    public class ServerShopCatalogueItem
    {
        internal int ID { get; set; }
        internal string Name { get; set; }
        internal int Price { get; set; }
        internal bool InStock { get; set; }
        internal string ShopName { get; set; }
        internal string Permission { get; set; }
        internal bool Hidden { get; set; }
        internal ServerShopCatalogueItem(int id, string  name, int price, bool instock, string shopname, string permission = null, bool hidden = false)
        {
            ID = id;
            Name = name;
            Price = price;
            InStock = instock;
            ShopName = shopname;
            Permission = permission;
            Hidden = hidden;
        }
    }
}
