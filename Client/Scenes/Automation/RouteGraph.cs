using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Library;
using Library.SystemModels;

namespace Client.Scenes.Automation;

public static class RouteGraph
{
	private sealed class SearchNode
	{
		public MovementInfo Movement;

		public Point ArrivalPoint;

		public int Cost;

		public SearchNode? Parent;
	}

	private static readonly ConcurrentDictionary<MapRegion, Point[]> RegionPoints = new ConcurrentDictionary<MapRegion, Point[]>();

	public static async Task<Point[]?> GetRegionPointsAsync(MapRegion? region)
	{
		if (region?.Map == null)
		{
			return null;
		}
		if (RegionPoints.TryGetValue(region, out Point[] value))
		{
			return value;
		}
		MapData data = await MapDataCache.GetAsync(region.Map.FileName).ConfigureAwait(continueOnCapturedContext: false);
		if (data == null)
		{
			return null;
		}
		Point[] array = (from p in region.GetPoints(data.Width)
			where !data.IsWall(p.X, p.Y)
			select p).ToArray();
		if (array.Length == 0)
		{
			array = region.GetPoints(data.Width).ToArray();
		}
		RegionPoints[region] = array;
		return array;
	}

	public static bool CanUse(MovementInfo movement, MirClass userClass, int userLevel, Func<ItemInfo, bool>? hasItem)
	{
		if (movement.SourceRegion?.Map == null || movement.DestinationRegion?.Map == null)
		{
			return false;
		}
		if (movement.NeedItem != null && (hasItem == null || !hasItem(movement.NeedItem)))
		{
			return false;
		}
		RequiredClass requiredClass = userClass switch
		{
			MirClass.Warrior => RequiredClass.Warrior, 
			MirClass.Wizard => RequiredClass.Wizard, 
			MirClass.Taoist => RequiredClass.Taoist, 
			MirClass.Assassin => RequiredClass.Assassin, 
			_ => RequiredClass.None, 
		};
		if (movement.RequiredClass != RequiredClass.None && (movement.RequiredClass & requiredClass) != requiredClass)
		{
			return false;
		}
		MapInfo map = movement.DestinationRegion.Map;
		if (map.DisableAutoPlay)
		{
			return false;
		}
		if (map.MinimumLevel > 0 && userLevel < map.MinimumLevel)
		{
			return false;
		}
		if (map.MaximumLevel > 0 && userLevel > map.MaximumLevel)
		{
			return false;
		}
		return true;
	}

	public static async Task<List<RouteHop>?> FindRouteAsync(Step start, Step destination, MirClass userClass, int userLevel, Func<ItemInfo, bool>? hasItem)
	{
		if (start.MapIndex == destination.MapIndex)
		{
			return new List<RouteHop>();
		}
		Dictionary<int, List<MovementInfo>> movements = (from x in Globals.MovementInfoList.Binding
			where CanUse(x, userClass, userLevel, hasItem)
			group x by x.SourceRegion.Map.Index).ToDictionary((IGrouping<int, MovementInfo> x) => x.Key, (IGrouping<int, MovementInfo> x) => x.ToList());
		if (!movements.ContainsKey(start.MapIndex))
		{
			return null;
		}
		PriorityQueue<SearchNode, int> open = new PriorityQueue<SearchNode, int>();
		Dictionary<MovementInfo, int> best = new Dictionary<MovementInfo, int>();
		SearchNode result = null;
		int resultCost = int.MaxValue;
		await EnqueueDoors(open, best, movements, start.MapIndex, start.Location, null, 0).ConfigureAwait(continueOnCapturedContext: false);
		int expansions = 0;
		while (open.Count > 0)
		{
			int num = expansions + 1;
			expansions = num;
			if (num > 4000)
			{
				break;
			}
			SearchNode searchNode = open.Dequeue();
			if (searchNode.Cost >= resultCost || (best.TryGetValue(searchNode.Movement, out var value) && searchNode.Cost > value))
			{
				continue;
			}
			int index = searchNode.Movement.DestinationRegion.Map.Index;
			if (index == destination.MapIndex)
			{
				int num2 = searchNode.Cost + Functions.Distance(searchNode.ArrivalPoint, destination.Location);
				if (num2 < resultCost)
				{
					resultCost = num2;
					result = searchNode;
				}
			}
			else
			{
				await EnqueueDoors(open, best, movements, index, searchNode.ArrivalPoint, searchNode, searchNode.Cost).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		if (result == null)
		{
			return null;
		}
		List<RouteHop> hops = new List<RouteHop>();
		for (SearchNode node = result; node != null; node = node.Parent)
		{
			Point[] array = await GetRegionPointsAsync(node.Movement.SourceRegion).ConfigureAwait(continueOnCapturedContext: false);
			Point point = node.Parent?.ArrivalPoint ?? start.Location;
			hops.Add(new RouteHop
			{
				Movement = node.Movement,
				SourcePoint = ((array != null && array.Length > 0) ? NearestPoint(array, point) : point),
				DestinationPoint = node.ArrivalPoint
			});
		}
		hops.Reverse();
		return hops;
	}

	private static async Task EnqueueDoors(PriorityQueue<SearchNode, int> open, Dictionary<MovementInfo, int> best, Dictionary<int, List<MovementInfo>> movements, int mapIndex, Point from, SearchNode? parent, int baseCost)
	{
		if (!movements.TryGetValue(mapIndex, out List<MovementInfo> value))
		{
			return;
		}
		foreach (MovementInfo movement in value)
		{
			Point[] sourcePoints = await GetRegionPointsAsync(movement.SourceRegion).ConfigureAwait(continueOnCapturedContext: false);
			if (sourcePoints == null || sourcePoints.Length == 0)
			{
				continue;
			}
			Point[] array = await GetRegionPointsAsync(movement.DestinationRegion).ConfigureAwait(continueOnCapturedContext: false);
			if (array != null && array.Length != 0)
			{
				Point dest = NearestPoint(sourcePoints, from);
				int num = baseCost + Functions.Distance(from, dest) + 2;
				if (!best.TryGetValue(movement, out var value2) || value2 > num)
				{
					best[movement] = num;
					SearchNode element = new SearchNode
					{
						Movement = movement,
						ArrivalPoint = Centroid(array),
						Cost = num,
						Parent = parent
					};
					open.Enqueue(element, num);
				}
			}
		}
	}

	public static Dictionary<int, int> GetMapHops(int startMapIndex, MirClass userClass, int userLevel, Func<ItemInfo, bool>? hasItem)
	{
		Dictionary<int, int> dictionary = new Dictionary<int, int> { [startMapIndex] = 0 };
		Queue<int> queue = new Queue<int>();
		queue.Enqueue(startMapIndex);
		while (queue.Count > 0)
		{
			int num = queue.Dequeue();
			int value = dictionary[num] + 1;
			foreach (MovementInfo item in Globals.MovementInfoList.Binding)
			{
				MapRegion sourceRegion = item.SourceRegion;
				if (sourceRegion != null && sourceRegion.Map?.Index == num && CanUse(item, userClass, userLevel, hasItem))
				{
					int index = item.DestinationRegion.Map.Index;
					if (!dictionary.ContainsKey(index))
					{
						dictionary[index] = value;
						queue.Enqueue(index);
					}
				}
			}
		}
		return dictionary;
	}

	private static Point NearestPoint(Point[] points, Point from)
	{
		Point result = points[0];
		int num = int.MaxValue;
		foreach (Point point in points)
		{
			int num2 = Functions.Distance(from, point);
			if (num2 < num)
			{
				num = num2;
				result = point;
			}
		}
		return result;
	}

	private static Point Centroid(Point[] points)
	{
		long num = 0L;
		long num2 = 0L;
		for (int i = 0; i < points.Length; i++)
		{
			Point point = points[i];
			num += point.X;
			num2 += point.Y;
		}
		return new Point((int)(num / points.Length), (int)(num2 / points.Length));
	}
}
