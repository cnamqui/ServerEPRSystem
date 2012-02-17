using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommandShop
{
    public enum ChargeType : byte
    {
        StartsWith = 0x00,
        EqualsTo = 0x01
    }
    public enum BlockType : byte
    {
        NoBlock = 0x00,
        BlockStartsWith = 0x01,
        BlockEqualsTo = 0x02
    }
    internal class CommandParser
    {
        internal string Command { get; set; }
        internal int Cost { get; set; }
        internal ChargeType ChargeType { get; set; }
        internal string CostOverridePermission { get; set; }
        internal BlockType BlockType { get; set; }
        internal string BlockOverridePermission { get; set; }
        internal CommandParser(string cmd, int cost, ChargeType ct, string costoverride, BlockType bt, string blockoverride)
        {
            Command = cmd;
            Cost = cost;
            ChargeType = ct;
            CostOverridePermission = costoverride;
            BlockType = bt;
            BlockOverridePermission = blockoverride;
        }
    }
}
