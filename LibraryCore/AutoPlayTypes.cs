using System.Drawing;

namespace Library
{
    public sealed class ClientAutoPlaySettings
    {
        public bool KiteEnabled { get; set; } = true;
        public bool ReturnOnDeath { get; set; }
        public int MaxDeathReturns { get; set; } = 3;
        public bool LootEnabled { get; set; } = true;
        public bool HarvestEnabled { get; set; } = true;
        public bool TownOnBagFull { get; set; } = true;
        public bool TownOnNoHealthPotion { get; set; } = true;
        public bool TownOnNoManaPotion { get; set; } = true;
        public bool TownOnNoPoison { get; set; } = true;
        public bool TownOnNoTalisman { get; set; } = true;
        public bool TownOnEquipmentBroken { get; set; } = true;
        public bool CanAutoSell { get; set; } = true;
        /// <summary>
        /// What unlocked sellable inventory items to dump during town runs.
        /// NormalOnly = unlocked sellable with no added stats; All = every unlocked sellable item.
        /// </summary>
        public AutoSellMode SellMode { get; set; } = AutoSellMode.NormalOnly;
        public bool CanAutoRepair { get; set; } = true;
        public bool ShouldSpecialRepair { get; set; }
        public bool CanAutoBuy { get; set; } = true;
        public bool AutoEquipTorch { get; set; } = true;
        public bool AutoEquipPoison { get; set; } = true;
        public bool AutoEquipTalisman { get; set; } = true;
    }

    public enum AutoTownAction : byte
    {
        Sell,
        Repair,
        Buy
    }

    public enum AutoSellMode
    {
        Never,
        NormalOnly,
        All
    }

    public enum PickUpRule
    {
        Never,
        Always,
        WhenBagSpace,
        OnlySpecial
    }

    public sealed class AutoBuyItem
    {
        public int Index { get; set; }
        public long Amount { get; set; }
    }

    public sealed class ClientDropFilterRule
    {
        public PickUpRule Rule { get; set; } = PickUpRule.Always;
        public AutoSellMode AutoSellMode { get; set; } = AutoSellMode.Never;
    }

    public sealed class ClientDropFilterInfo
    {
        public int Index { get; set; }
        public string Name { get; set; }
    }

    public sealed class ClientQuickBuyTarget
    {
        public int ItemInfoIndex { get; set; }
        public int TargetAmount { get; set; }
    }
}
