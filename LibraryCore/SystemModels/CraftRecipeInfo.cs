using MirDB;
using System;

namespace Library.SystemModels
{
    public sealed class CraftRecipeInfo : DBObject
    {
        [IsIdentity]
        public string Description
        {
            get { return _Description; }
            set
            {
                if (_Description == value) return;

                var oldValue = _Description;
                _Description = value;

                OnChanged(oldValue, value, "Description");
            }
        }
        private string _Description;

        public ItemInfo ResultItem
        {
            get { return _ResultItem; }
            set
            {
                if (_ResultItem == value) return;

                var oldValue = _ResultItem;
                _ResultItem = value;

                OnChanged(oldValue, value, "ResultItem");
            }
        }
        private ItemInfo _ResultItem;

        public int ResultAmount
        {
            get { return _ResultAmount; }
            set
            {
                if (_ResultAmount == value) return;

                var oldValue = _ResultAmount;
                _ResultAmount = value;

                OnChanged(oldValue, value, "ResultAmount");
            }
        }
        private int _ResultAmount = 1;

        public long GoldCost
        {
            get { return _GoldCost; }
            set
            {
                if (_GoldCost == value) return;

                var oldValue = _GoldCost;
                _GoldCost = value;

                OnChanged(oldValue, value, "GoldCost");
            }
        }
        private long _GoldCost;

        public int SuccessChance
        {
            get { return _SuccessChance; }
            set
            {
                if (_SuccessChance == value) return;

                var oldValue = _SuccessChance;
                _SuccessChance = value;

                OnChanged(oldValue, value, "SuccessChance");
            }
        }
        private int _SuccessChance = 100;

        public int RequiredLevel
        {
            get { return _RequiredLevel; }
            set
            {
                if (_RequiredLevel == value) return;

                var oldValue = _RequiredLevel;
                _RequiredLevel = value;

                OnChanged(oldValue, value, "RequiredLevel");
            }
        }
        private int _RequiredLevel;

        public RequiredClass RequiredClass
        {
            get { return _RequiredClass; }
            set
            {
                if (_RequiredClass == value) return;

                var oldValue = _RequiredClass;
                _RequiredClass = value;

                OnChanged(oldValue, value, "RequiredClass");
            }
        }
        private RequiredClass _RequiredClass;

        public string Category
        {
            get { return _Category; }
            set
            {
                if (_Category == value) return;

                var oldValue = _Category;
                _Category = value;

                OnChanged(oldValue, value, "Category");
            }
        }
        private string _Category;

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

        [Association("Ingredients", true)]
        public DBBindingList<CraftRecipeIngredientInfo> Ingredients { get; set; }

        protected internal override void OnLoaded()
        {
            base.OnLoaded();

            ResultAmount = Math.Max(1, ResultAmount);
            SuccessChance = Math.Clamp(SuccessChance, 0, 100);
        }
    }

    public sealed class CraftRecipeIngredientInfo : DBObject
    {
        [Association("Ingredients")]
        public CraftRecipeInfo Recipe
        {
            get { return _Recipe; }
            set
            {
                if (_Recipe == value) return;

                var oldValue = _Recipe;
                _Recipe = value;

                OnChanged(oldValue, value, "Recipe");
            }
        }
        private CraftRecipeInfo _Recipe;

        public ItemInfo Item
        {
            get { return _Item; }
            set
            {
                if (_Item == value) return;

                var oldValue = _Item;
                _Item = value;

                OnChanged(oldValue, value, "Item");
            }
        }
        private ItemInfo _Item;

        public int Amount
        {
            get { return _Amount; }
            set
            {
                if (_Amount == value) return;

                var oldValue = _Amount;
                _Amount = value;

                OnChanged(oldValue, value, "Amount");
            }
        }
        private int _Amount = 1;

        protected internal override void OnLoaded()
        {
            base.OnLoaded();

            Amount = Math.Max(1, Amount);
        }
    }
}
