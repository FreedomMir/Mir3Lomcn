using System.Drawing;
using Library.SystemModels;

namespace Client.Scenes.Automation
{
    public sealed class TownNPCRef
    {
        public NPCInfo NPCInfo { get; set; }
        public MapInfo Map { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public Point Location => new Point(X, Y);
    }
}
