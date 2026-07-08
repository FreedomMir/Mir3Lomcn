using MirDB;
using System;

namespace Library.SystemModels
{
    /// <summary>
    /// Configures a rentable guild-only territory instance.
    /// Ranks: 0 base, 1 summon one, 2 summon all, 3 self-recall, 4 cheaper renew, 5 territory buff.
    /// </summary>
    public sealed class GuildTerritoryInfo : DBObject
    {
        public const int MaxRank = 5;

        [IsIdentity]
        public string Name
        {
            get { return _Name; }
            set
            {
                if (_Name == value) return;

                var oldValue = _Name;
                _Name = value;

                OnChanged(oldValue, value, "Name");
            }
        }
        private string _Name;

        public InstanceInfo Instance
        {
            get { return _Instance; }
            set
            {
                if (_Instance == value) return;

                var oldValue = _Instance;
                _Instance = value;

                OnChanged(oldValue, value, "Instance");
            }
        }
        private InstanceInfo _Instance;

        /// <summary>Guild funds required for the first lease.</summary>
        public long RentCost
        {
            get { return _RentCost; }
            set
            {
                if (_RentCost == value) return;

                var oldValue = _RentCost;
                _RentCost = value;

                OnChanged(oldValue, value, "RentCost");
            }
        }
        private long _RentCost = 1_000_000;

        /// <summary>Guild funds required to extend an active lease (before rank discount).</summary>
        public long RenewCost
        {
            get { return _RenewCost; }
            set
            {
                if (_RenewCost == value) return;

                var oldValue = _RenewCost;
                _RenewCost = value;

                OnChanged(oldValue, value, "RenewCost");
            }
        }
        private long _RenewCost = 500_000;

        /// <summary>Lease length added on rent or renew.</summary>
        public TimeSpan Duration
        {
            get { return _Duration; }
            set
            {
                if (_Duration == value) return;

                var oldValue = _Duration;
                _Duration = value;

                OnChanged(oldValue, value, "Duration");
            }
        }
        private TimeSpan _Duration = TimeSpan.FromDays(7);

        public int MinGuildLevel
        {
            get { return _MinGuildLevel; }
            set
            {
                if (_MinGuildLevel == value) return;

                var oldValue = _MinGuildLevel;
                _MinGuildLevel = value;

                OnChanged(oldValue, value, "MinGuildLevel");
            }
        }
        private int _MinGuildLevel;

        public bool Enabled
        {
            get { return _Enabled; }
            set
            {
                if (_Enabled == value) return;

                var oldValue = _Enabled;
                _Enabled = value;

                OnChanged(oldValue, value, "Enabled");
            }
        }
        private bool _Enabled = true;

        public long Rank1Cost
        {
            get { return _Rank1Cost; }
            set
            {
                if (_Rank1Cost == value) return;
                var oldValue = _Rank1Cost;
                _Rank1Cost = value;
                OnChanged(oldValue, value, "Rank1Cost");
            }
        }
        private long _Rank1Cost = 500_000;

        public long Rank2Cost
        {
            get { return _Rank2Cost; }
            set
            {
                if (_Rank2Cost == value) return;
                var oldValue = _Rank2Cost;
                _Rank2Cost = value;
                OnChanged(oldValue, value, "Rank2Cost");
            }
        }
        private long _Rank2Cost = 1_000_000;

        public long Rank3Cost
        {
            get { return _Rank3Cost; }
            set
            {
                if (_Rank3Cost == value) return;
                var oldValue = _Rank3Cost;
                _Rank3Cost = value;
                OnChanged(oldValue, value, "Rank3Cost");
            }
        }
        private long _Rank3Cost = 1_500_000;

        public long Rank4Cost
        {
            get { return _Rank4Cost; }
            set
            {
                if (_Rank4Cost == value) return;
                var oldValue = _Rank4Cost;
                _Rank4Cost = value;
                OnChanged(oldValue, value, "Rank4Cost");
            }
        }
        private long _Rank4Cost = 2_000_000;

        public long Rank5Cost
        {
            get { return _Rank5Cost; }
            set
            {
                if (_Rank5Cost == value) return;
                var oldValue = _Rank5Cost;
                _Rank5Cost = value;
                OnChanged(oldValue, value, "Rank5Cost");
            }
        }
        private long _Rank5Cost = 3_000_000;

        /// <summary>Percent off renew cost at rank 4+ (default 25).</summary>
        public int RenewDiscountPercent
        {
            get { return _RenewDiscountPercent; }
            set
            {
                if (_RenewDiscountPercent == value) return;
                var oldValue = _RenewDiscountPercent;
                _RenewDiscountPercent = value;
                OnChanged(oldValue, value, "RenewDiscountPercent");
            }
        }
        private int _RenewDiscountPercent = 25;

        public long GetRankUpgradeCost(int nextRank)
        {
            return nextRank switch
            {
                1 => Rank1Cost,
                2 => Rank2Cost,
                3 => Rank3Cost,
                4 => Rank4Cost,
                5 => Rank5Cost,
                _ => 0,
            };
        }

        public long GetRenewCost(int territoryRank)
        {
            if (territoryRank < 4 || RenewDiscountPercent <= 0)
                return RenewCost;

            int percent = Math.Clamp(RenewDiscountPercent, 0, 90);
            return RenewCost * (100 - percent) / 100;
        }

        public static string GetRankPerkDescription(int rank)
        {
            return rank switch
            {
                0 => "Base lease (enter only)",
                1 => "Summon one online member",
                2 => "Summon all online members",
                3 => "Members can recall themselves",
                4 => "Cheaper renew",
                5 => "Territory buff for members",
                _ => "Unknown",
            };
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
