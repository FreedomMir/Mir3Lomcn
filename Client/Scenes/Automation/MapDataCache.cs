using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Client.Envir;

namespace Client.Scenes.Automation;

public static class MapDataCache
{
	private sealed class Entry
	{
		public Task<MapData?> Load;

		public long LastAccess;
	}

	private const int Capacity = 24;

	private static readonly ConcurrentDictionary<string, Entry> Cache = new ConcurrentDictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

	private static long _accessCounter;

	public static MapData? TryGet(string? fileName)
	{
		if (string.IsNullOrEmpty(fileName))
		{
			return null;
		}
		Entry entry = Touch(fileName);
		if (!entry.Load.IsCompletedSuccessfully)
		{
			return null;
		}
		return entry.Load.Result;
	}

	public static Task<MapData?> GetAsync(string? fileName)
	{
		if (string.IsNullOrEmpty(fileName))
		{
			return Task.FromResult<MapData>(null);
		}
		return Touch(fileName).Load;
	}

	private static Entry Touch(string fileName)
	{
		Entry orAdd = Cache.GetOrAdd(fileName, delegate(string key)
		{
			Evict();
			return new Entry
			{
				Load = Task.Run(delegate
				{
					try
					{
						return MapData.Load(key);
					}
					catch (Exception ex)
					{
						CEnvir.SaveError(ex.ToString());
						return (MapData)null;
					}
				})
			};
		});
		orAdd.LastAccess = Interlocked.Increment(ref _accessCounter);
		return orAdd;
	}

	private static void Evict()
	{
		if (Cache.Count < 24)
		{
			return;
		}
		string text = null;
		long num = long.MaxValue;
		foreach (KeyValuePair<string, Entry> item in Cache)
		{
			if (item.Value.Load.IsCompleted && item.Value.LastAccess < num)
			{
				num = item.Value.LastAccess;
				text = item.Key;
			}
		}
		if (text != null)
		{
			Cache.TryRemove(text, out Entry _);
		}
	}
}
