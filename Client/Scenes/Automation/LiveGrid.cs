using Client.Scenes.Views;

namespace Client.Scenes.Automation;

public sealed class LiveGrid : IWalkGrid
{
	private readonly MapControl _map;

	private readonly bool _includeObjects;

	public int Width => _map.Width;

	public int Height => _map.Height;

	public LiveGrid(MapControl map, bool includeObjects)
	{
		_map = map;
		_includeObjects = includeObjects;
	}

	public bool IsBlocked(int x, int y)
	{
		if (x < 0 || y < 0 || x >= _map.Width || y >= _map.Height)
		{
			return true;
		}
		Cell cell = _map.Cells[x, y];
		if (!_includeObjects)
		{
			return cell.Flag;
		}
		return cell.Blocking();
	}
}
