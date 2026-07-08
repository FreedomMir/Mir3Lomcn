using Library;
using Library.SystemModels;

namespace Client.Scenes.Views
{
    public static class NPCSellHelper
    {
        public static bool IsSellable(ClientUserItem item)
        {
            if (item?.Info == null) return false;
            if (!item.Info.CanSell) return false;
            if ((item.Flags & UserItemFlags.Locked) == UserItemFlags.Locked) return false;
            if ((item.Flags & UserItemFlags.Worthless) == UserItemFlags.Worthless) return false;
            if ((item.Flags & UserItemFlags.Bound) == UserItemFlags.Bound) return false;
            if ((item.Flags & UserItemFlags.Marriage) == UserItemFlags.Marriage) return false;
            return true;
        }

        public static AutoSellMode GetMode(ItemInfo info)
        {
            ClientDropFilterRule rule = GameScene.Game?.GetDropFilterRule(info);
            if (rule != null)
                return rule.AutoSellMode;

            return GameScene.Game?.AutoPlay?.Settings?.SellMode ?? AutoSellMode.Never;
        }

        public static bool ShouldAutoSell(ClientUserItem item)
        {
            if (!IsSellable(item)) return false;
            return MatchesMode(item, GetMode(item.Info));
        }

        public static bool MatchesMode(ClientUserItem item, AutoSellMode mode)
        {
            if (item?.Info == null || mode == AutoSellMode.Never) return false;

            // Keep restock / utility items out of bulk auto-sell.
            if (IsProtectedUtility(item.Info.ItemType))
                return false;

            // Zircon NormalOnly = no added stats (plain gear), not rarity Common.
            if (mode == AutoSellMode.NormalOnly)
            {
                if (item.AddedStats != null && item.AddedStats.Count > 0)
                    return false;
            }

            return mode == AutoSellMode.All || mode == AutoSellMode.NormalOnly;
        }

        private static bool IsProtectedUtility(ItemType type)
        {
            switch (type)
            {
                case ItemType.Consumable:
                case ItemType.Poison:
                case ItemType.Amulet:
                case ItemType.Torch:
                case ItemType.Book:
                case ItemType.Scroll:
                case ItemType.CompanionFood:
                case ItemType.Nothing:
                case ItemType.System:
                    return true;
                default:
                    return false;
            }
        }
    }
}
