using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Library;
using Library.SystemModels;

namespace Client.Scenes.Automation
{
    public static class TownNPCHelper
    {
        public static IEnumerable<TownNPCRef> Enumerate(int mapIndex)
        {
            if (Globals.NPCInfoList?.Binding == null) yield break;

            foreach (NPCInfo npc in Globals.NPCInfoList.Binding)
            {
                MapRegion region = npc?.Region;
                MapInfo map = region?.Map;
                if (map == null || map.Index != mapIndex) continue;

                Point? location = GetRegionPoint(region);
                if (!location.HasValue) continue;

                yield return new TownNPCRef
                {
                    NPCInfo = npc,
                    Map = map,
                    X = location.Value.X,
                    Y = location.Value.Y
                };
            }
        }

        public static TownNPCRef FindTownNPC(int mapIndex, AutoTownAction action)
        {
            TownNPCRef best = null;
            int bestDistance = int.MaxValue;

            foreach (TownNPCRef npc in Enumerate(mapIndex))
            {
                if (!MatchesAction(npc.NPCInfo, action)) continue;

                int distance = npc.X + npc.Y;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = npc;
                }
            }

            return best;
        }

        public static bool StocksItem(int mapIndex, ItemInfo info)
        {
            foreach (TownNPCRef npc in Enumerate(mapIndex))
            {
                if (FindGood(npc.NPCInfo, info) != null)
                    return true;
            }
            return false;
        }

        public static NPCGood FindGood(NPCInfo npc, ItemInfo info)
        {
            if (npc?.EntryPage == null || info == null) return null;

            foreach (NPCPage page in EnumeratePages(npc.EntryPage))
            {
                if (page.DialogType != NPCDialogType.BuySell || page.Goods == null) continue;

                NPCGood good = page.Goods.FirstOrDefault(x => x.Item == info);
                if (good != null) return good;
            }

            return null;
        }

        private static bool MatchesAction(NPCInfo npc, AutoTownAction action)
        {
            if (npc?.EntryPage == null) return false;

            foreach (NPCPage page in EnumeratePages(npc.EntryPage))
            {
                switch (action)
                {
                    case AutoTownAction.Sell:
                    case AutoTownAction.Buy:
                        if (page.DialogType == NPCDialogType.BuySell) return true;
                        break;
                    case AutoTownAction.Repair:
                        if (page.DialogType == NPCDialogType.Repair) return true;
                        break;
                }
            }

            return false;
        }

        private static IEnumerable<NPCPage> EnumeratePages(NPCPage root)
        {
            if (root == null) yield break;

            var visited = new HashSet<NPCPage>();
            var stack = new Stack<NPCPage>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                NPCPage page = stack.Pop();
                if (page == null || !visited.Add(page)) continue;

                yield return page;

                if (page.SuccessPage != null)
                    stack.Push(page.SuccessPage);

                if (page.Buttons == null) continue;

                foreach (NPCButton button in page.Buttons)
                {
                    if (button?.DestinationPage != null)
                        stack.Push(button.DestinationPage);
                }
            }
        }

        private static Point? GetRegionPoint(MapRegion region)
        {
            if (region == null) return null;

            if (region.PointRegion != null && region.PointRegion.Length > 0)
                return region.PointRegion[0];

            if (region.PointList != null && region.PointList.Count > 0)
                return region.PointList[0];

            return null;
        }
    }
}
