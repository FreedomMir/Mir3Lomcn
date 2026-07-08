using System;
using System.Collections.Generic;
using System.Drawing;
using Client.Scenes.Views;
using Library;

namespace Client.Scenes.Automation;

public static class PathFinder
{
	private sealed class Node
	{
		public Point Location;

		public int G;

		public int H;

		public MirDirection Direction;

		public Node? Parent;

		public bool Closed;

		public int F => G + H;
	}

	private const int StepCost = 10;

	private const int TurnPenalty = 3;

	public const int CombatBudget = 2000;

	public const int TravelBudget = 40000;

	public static List<Point>? FindPath(IWalkGrid grid, Point start, Point target, MirDirection startDirection, int stopDistance = 0, int maxNodes = 40000, int boundary = int.MaxValue, Func<Point, bool>? avoid = null)
	{
		if (start == target)
		{
			return new List<Point>();
		}
		if (grid == null || grid.Width <= 0 || grid.Height <= 0)
		{
			return null;
		}
		PriorityQueue<Node, long> priorityQueue = new PriorityQueue<Node, long>();
		Dictionary<int, Node> dictionary = new Dictionary<int, Node>();
		Node node = new Node
		{
			Location = start,
			G = 0,
			H = Functions.Distance(start, target) * 10,
			Direction = startDirection
		};
		dictionary[start.X + start.Y * grid.Width] = node;
		priorityQueue.Enqueue(node, Priority(node, target));
		int num = 0;
		while (priorityQueue.Count > 0)
		{
			Node node2 = priorityQueue.Dequeue();
			if (node2.Closed)
			{
				continue;
			}
			node2.Closed = true;
			if (Functions.Distance(node2.Location, target) <= stopDistance)
			{
				return BuildPath(node2);
			}
			if (++num > maxNodes)
			{
				return null;
			}
			MirDirection dir = Functions.DirectionFromPoint(node2.Location, target);
			for (int i = 0; i < 8; i++)
			{
				MirDirection mirDirection = Functions.ShiftDirection(dir, (((i & 1) == 0) ? 1 : (-1)) * ((i + 1) / 2));
				Point point = Functions.Move(node2.Location, mirDirection);
				if (point.X < 0 || point.Y < 0 || point.X >= grid.Width || point.Y >= grid.Height || Math.Abs(point.X - start.X) > boundary || Math.Abs(point.Y - start.Y) > boundary || ((!(point == target) || stopDistance > 0) && (grid.IsBlocked(point.X, point.Y) || (avoid != null && avoid(point)))))
				{
					continue;
				}
				int num2 = node2.G + 10 + ((mirDirection != node2.Direction) ? 3 : 0);
				int key = point.X + point.Y * grid.Width;
				if (dictionary.TryGetValue(key, out var value))
				{
					if (!value.Closed && num2 < value.G)
					{
						value.G = num2;
						value.Direction = mirDirection;
						value.Parent = node2;
						priorityQueue.Enqueue(value, Priority(value, target));
					}
				}
				else
				{
					Node node3 = (dictionary[key] = new Node
					{
						Location = point,
						G = num2,
						H = Functions.Distance(point, target) * 10,
						Direction = mirDirection,
						Parent = node2
					});
					priorityQueue.Enqueue(node3, Priority(node3, target));
				}
			}
		}
		return null;
	}

	public static List<Point>? FindLivePath(MapControl map, Point start, Point target, MirDirection startDirection, int stopDistance = 0, int maxNodes = 40000, int boundary = int.MaxValue, Func<Point, bool>? avoid = null)
	{
		return FindPath(new LiveGrid(map, includeObjects: true), start, target, startDirection, stopDistance, maxNodes, boundary, avoid) ?? FindPath(new LiveGrid(map, includeObjects: false), start, target, startDirection, stopDistance, maxNodes, boundary, avoid);
	}

	private static long Priority(Node node, Point target)
	{
		int num = DirectionDelta(node.Direction, Functions.DirectionFromPoint(node.Location, target));
		return ((long)node.F << 20) | (uint)Math.Min(node.H * 8 + num, 1048575);
	}

	private static int DirectionDelta(MirDirection a, MirDirection b)
	{
		int num = Math.Abs((int)(a - b));
		return Math.Min(num, 8 - num);
	}

	private static List<Point> BuildPath(Node node)
	{
		List<Point> list = new List<Point>();
		while (node.Parent != null)
		{
			list.Add(node.Location);
			node = node.Parent;
		}
		list.Reverse();
		return list;
	}
}
