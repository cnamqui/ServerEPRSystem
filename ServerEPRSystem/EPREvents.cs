using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerPointSystem
{
    public delegate void MonsterPointAwardEventHandler(MonsterAwardArgs e);
    public delegate void PointUsageHandler(PointUseArgs e);
    public delegate void PointPaymentHandler(PointPayArgs e);
    public delegate void PointOperationHandler(PointOperateArgs e);

    public enum PointUsage : byte
    {
        SentPayment = 0x00,
        Shop = 0x01,
        Command = 0x02,
        Rank = 0x03,
        Bet = 0x04,
    }
    public enum PointOperateReason : byte
    {
        PlayerUse = 0x00,
        Death = 0x01,
        PVP = 0x02,
        MonsterKill = 0x03,
        ReceivedPayment = 0x04,
        Award = 0x05,
        Deduct =0x06,
        PVPEvent = 0x07,
        TimeReward = 0x08
    }

    public class EPREvents
    {
        public static event MonsterPointAwardEventHandler OnMonsterPointAward;
        public static event PointUsageHandler OnPointUse;
        public static event PointPaymentHandler OnPointPay;
        public static event PointOperationHandler OnPointOperate;

        public static void MonsterPointAward(int npcid, int npctype, int awardamount, EPRPlayer player)
        {
            MonsterAwardArgs e = new MonsterAwardArgs();
            e.Handled = false;
            e.NPCID = npcid;
            e.NPCType = npctype;
            e.AwardAmount = awardamount;
            e.Player = player;
            if (OnMonsterPointAward != null)
                OnMonsterPointAward(e);
        }
        public static void PointUse(EPRPlayer player, int amount, PointUsage reason)
        {
            PointUseArgs e = new PointUseArgs();
            e.Handled = false;
            e.Player = player;
            e.Amount = amount;
            e.Reason = reason;
            if (OnPointUse != null)
                OnPointUse(e);
        }
        public static void PointPay(EPRPlayer sender, EPRPlayer receiver, int amount)
        {
            PointPayArgs e = new PointPayArgs();
            e.Handled = false;
            e.Sender = sender;
            e.Receiver = receiver;
            e.Amount = amount;
            if (OnPointPay != null)
                OnPointPay(e);
        }
        public static void PointOperate(EPRPlayer player, int amount, PointOperateReason reason)
        {
            PointOperateArgs e = new PointOperateArgs();
            e.Handled = false;
            e.Player = player;
            e.Amount = amount;
            e.Reason = reason;
            if (OnPointOperate != null)
                OnPointOperate(e);
        }
    }

    public class MonsterAwardArgs : EventArgs
    {
        public bool Handled;
        public int NPCID;
        public int NPCType;
        public int AwardAmount;
        public EPRPlayer Player;
    }
    public class PointUseArgs : EventArgs
    {
        public bool Handled;
        public EPRPlayer Player;
        public int Amount;
        public PointUsage Reason;
    }
    public class PointPayArgs : EventArgs
    {
        public bool Handled;
        public EPRPlayer Sender;
        public EPRPlayer Receiver;
        public int Amount;
    }
    public class PointOperateArgs : EventArgs
    {
        public bool Handled;
        public EPRPlayer Player;
        public int Amount;
        public PointOperateReason Reason;
    }
}
