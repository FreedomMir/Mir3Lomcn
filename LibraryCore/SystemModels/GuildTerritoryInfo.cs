using MirDB;
using System;

namespace Library.SystemModels
{
    /// <summary>
    /// Configures a rentable guild-only territory instance (V1: rent / renew / enter).
    /// Create an InstanceInfo with Type = Guild, then link it here and set costs.
    /// </summary>
    public sealed class GuildTerritoryInfo : DBObject
    {
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

        /// <summary>Guild funds required to extend an active lease.</summary>
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

        public override string ToString()
        {
            return Name;
        }
    }
}
