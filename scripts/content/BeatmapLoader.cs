using Godot;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using File = Godot.File;
using Directory = Godot.Directory;
using Compatibility.SSP;

namespace Content.Beatmaps
{
	public static class BeatmapLoader
	{
		public static List<BeatmapSet> LoadedMaps = new List<BeatmapSet>();
		public static bool LoadMapsFromDirectory(string directory, bool reset = false)
		{
			if (reset)
				LoadedMaps = new List<BeatmapSet>();
			SspmImporter.ImportDirectory(directory);
			GD.Print("Loading maps from " + directory);
			var cachePath = directory.PlusFile(".cache");
			var cacheDir = new Directory();
			if (cacheDir.Open(cachePath) != Error.Ok)
			{
				cacheDir.MakeDirRecursive(cachePath);
				cacheDir.Open(cachePath);
			}
			List<string> caches = new List<string>();
			cacheDir.ListDirBegin(true, true);
			var cacheFileName = cacheDir.GetNext();
			while (cacheFileName != "")
			{
				if (cacheDir.CurrentIsDir()) caches.Add(cacheFileName);
				cacheFileName = cacheDir.GetNext();
			}
			var hashes = new List<string>();
			var mapsDir = new Directory();
			mapsDir.Open(directory);
			mapsDir.ListDirBegin(true, true);
			var mapFileName = mapsDir.GetNext();
			var mapFile = new File();
			while (mapFileName != "")
			{
				if (mapFileName.Extension() == "vul")
				{
					var hash = mapFile.GetMd5(directory.PlusFile(mapFileName));
					hashes.Add(hash);
					if (LoadedMaps.Find(map => map.Hash == hash) == null)
					{
						if (!caches.Contains(hash))
						{
							mapFile.Open(directory.PlusFile(mapFileName), File.ModeFlags.Read);
							using var stream = new MemoryStream(mapFile.GetBuffer((long)mapFile.GetLen()));
							using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
							zip.ExtractToDirectory(cachePath.PlusFile(hash));
						}
						try
						{
							var map = BeatmapSet.LoadFromPath(cachePath.PlusFile(hash), hash);
							LoadedMaps.Add(map);
						}
						catch (Exception e)
						{
							GD.PrintErr($"{hash}: {e.Message}");
						}
					}
				}
				mapFileName = mapsDir.GetNext();
			}
			foreach (string hash in caches)
			{
				if (!hashes.Contains(hash)) System.IO.Directory.Delete(cachePath.PlusFile(hash), true);
			}
			if (mapFile.IsOpen()) mapFile.Close();
			return true;
		}
	}
}
