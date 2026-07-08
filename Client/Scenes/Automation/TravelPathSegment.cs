using System.Collections.Generic;
using System.Drawing;
using Library.SystemModels;

namespace Client.Scenes.Automation;

public sealed class TravelPathSegment
{
	public int MapIndex;

	public List<Point> Points = new List<Point>();

	public Point Objective;

	public MovementInfo? ExitMovement;
}
