namespace Client.Scenes.Automation;

public sealed class StaticGrid : IWalkGrid
{
	private readonly MapData _data;

	public int Width => _data.Width;

	public int Height => _data.Height;

	public StaticGrid(MapData data)
	{
		_data = data;
	}

	public bool IsBlocked(int x, int y)
	{
		return _data.IsWall(x, y);
	}
}
