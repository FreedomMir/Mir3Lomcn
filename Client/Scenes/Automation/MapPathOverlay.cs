using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Client.Controls;
using Client.Scenes;
using Library.SystemModels;

namespace Client.Scenes.Automation;

public sealed class MapPathOverlay : IDisposable
{
	private const int DotSpacing = 3;

	private static readonly Color TrailColour = Color.Cyan;

	private static readonly Color DestinationColour = Color.Yellow;

	private readonly DXControl _image;

	private readonly Func<MapInfo?> _displayedMap;

	private readonly Func<float> _scaleX;

	private readonly Func<float> _scaleY;

	private readonly List<DXControl> _dots = new List<DXControl>();

	private AutoPlayer? _autoPlayer;

	public MapPathOverlay(DXControl image, Func<MapInfo?> displayedMap, Func<float> scaleX, Func<float> scaleY)
	{
		_image = image;
		_displayedMap = displayedMap;
		_scaleX = scaleX;
		_scaleY = scaleY;
		_autoPlayer = GameScene.Game?.AutoPlay;
		if (_autoPlayer != null)
		{
			_autoPlayer.PathChanged += AutoPlayer_Changed;
			_autoPlayer.StateChanged += AutoPlayer_Changed;
		}
	}

	private void AutoPlayer_Changed(object? sender, EventArgs e)
	{
		Refresh();
	}

	public void Refresh()
	{
		Clear();
		AutoPlayer autoPlayer = GameScene.Game?.AutoPlay;
		if (autoPlayer == null || !autoPlayer.Enabled || _image == null || _image.IsDisposed)
		{
			return;
		}
		MapInfo map = _displayedMap();
		if (map == null)
		{
			return;
		}
		int num = GameScene.Game.MapControl?.MapInfo?.Index ?? (-1);
		List<Point> list = null;
		Point? point = null;
		if (map.Index == num && autoPlayer.Path != null)
		{
			list = autoPlayer.Path.Skip(autoPlayer.PathIndex).ToList();
			if (list.Count > 0)
			{
				List<Point> list2 = list;
				point = list2[list2.Count - 1];
			}
		}
		if (list == null && autoPlayer.Segments != null)
		{
			TravelPathSegment travelPathSegment = autoPlayer.Segments.FirstOrDefault((TravelPathSegment x) => x.MapIndex == map.Index);
			if (travelPathSegment != null)
			{
				list = travelPathSegment.Points;
				point = travelPathSegment.Objective;
			}
		}
		if (list == null || list.Count == 0)
		{
			return;
		}
		float num2 = _scaleX();
		float num3 = _scaleY();
		if (!(num2 <= 0f) && !(num3 <= 0f))
		{
			for (int num4 = 0; num4 < list.Count; num4 += 3)
			{
				_dots.Add(CreateDot(list[num4], TrailColour, 2, num2, num3));
			}
			if (point.HasValue)
			{
				_dots.Add(CreateDot(point.Value, DestinationColour, 5, num2, num3));
			}
		}
	}

	private DXControl CreateDot(Point cell, Color colour, int size, float scaleX, float scaleY)
	{
		return new DXControl
		{
			Parent = _image,
			DrawTexture = true,
			BackColour = colour,
			IsControl = false,
			Size = new Size(size, size),
			Opacity = _image.Opacity,
			Location = new Point((int)(scaleX * (float)cell.X) - size / 2, (int)(scaleY * (float)cell.Y) - size / 2)
		};
	}

	private void Clear()
	{
		foreach (DXControl dot in _dots)
		{
			if (!dot.IsDisposed)
			{
				dot.Dispose();
			}
		}
		_dots.Clear();
	}

	public void Dispose()
	{
		if (_autoPlayer != null)
		{
			_autoPlayer.PathChanged -= AutoPlayer_Changed;
			_autoPlayer.StateChanged -= AutoPlayer_Changed;
			_autoPlayer = null;
		}
		Clear();
	}
}
