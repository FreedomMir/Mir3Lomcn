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
            return true;
        }

        public static AutoSellMode GetMode(ItemInfo info)
        {
            return GameScene.Game?.GetDropFilterRule(info)?.AutoSellMode ?? AutoSellMode.Never;
        }

        public static bool ShouldAutoSell(ClientUserItem item)
        {
            if (!IsSellable(item)) return false;
            return MatchesMode(item, GetMode(item.Info));
        }

        private static bool MatchesMode(ClientUserItem item, AutoSellMode mode)
        {
            if (item == null || mode == AutoSellMode.Never) return false;
            if (mode == AutoSellMode.NormalOnly && item.AddedStats != null && item.AddedStats.Count > 0)
                return false;
            return mode == AutoSellMode.All || mode == AutoSellMode.NormalOnly;
        }
    }
}
