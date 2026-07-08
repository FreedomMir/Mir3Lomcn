namespace Client.Scenes.Automation;

public interface IWalkGrid
{
	int Width { get; }

	int Height { get; }

	bool IsBlocked(int x, int y);
}
