using System;
using System.IO;
using Client.Envir;

namespace Client.Scenes.Automation;

public sealed class MapData
{
	public string FileName = string.Empty;

	public int Width;

	public int Height;

	private bool[] _walls = Array.Empty<bool>();

	public bool InBounds(int x, int y)
	{
		if (x >= 0 && y >= 0 && x < Width)
		{
			return y < Height;
		}
		return false;
	}

	public bool IsWall(int x, int y)
	{
		if (InBounds(x, y))
		{
			return _walls[x * Height + y];
		}
		return true;
	}

	public static MapData? Load(string fileName)
	{
		string path = Config.MapPath + fileName + ".map";
		if (!File.Exists(path))
		{
			return null;
		}
		byte[] array = File.ReadAllBytes(path);
		short num = BitConverter.ToInt16(array, 22);
		short num2 = BitConverter.ToInt16(array, 24);
		if (num <= 0 || num2 <= 0)
		{
			return null;
		}
		int num3 = 28 + num / 2 * (num2 / 2) * 3;
		if (num3 + num * num2 * 14 > array.Length)
		{
			return null;
		}
		bool[] array2 = new bool[num * num2];
		for (int i = 0; i < array2.Length; i++)
		{
			byte b = array[num3 + i * 14];
			array2[i] = (b & 1) != 1 || (b & 2) != 2;
		}
		return new MapData
		{
			FileName = fileName,
			Width = num,
			Height = num2,
			_walls = array2
		};
	}
}
