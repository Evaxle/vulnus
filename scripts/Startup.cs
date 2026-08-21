using Godot;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class Startup : Node
{
	private Label label;
	private string text;
	private float counter;
	private static bool hasRun = false;
	private static Task task;
	public Action<string, bool> StageReached = (string stage, bool end) => { };
	public override void _Ready()
	{
		label = GetNode<Label>("Label");
		if (hasRun)
			return;
		hasRun = true;
		if (TryHandleRhythKitConversion())
			return;
		StageReached += OnStageReached;
		task = Task.Run(Run);
	}
	private bool TryHandleRhythKitConversion()
	{
		var args = OS.GetCmdlineArgs();
		for (var i = 0; i < args.Length - 1; i++)
		{
			if (!string.Equals(args[i], "--rhythkit-convert-sspm", StringComparison.OrdinalIgnoreCase)) continue;
			var input = args[i + 1];
			var result = Compatibility.SSP.SspmImporter.Import(input);
			GD.Print(result == null ? "RhythKit SSPM conversion failed" : $"RhythKit SSPM conversion complete: {result}");
			GetTree().Quit(result == null ? 1 : 0);
			return true;
		}
		return false;
	}
	public void OnStageReached(string stage, bool end)
	{
		text = stage;
		counter = 0;
		if (end)
			Global.Instance.GotoScene("res://scenes/MainMenu.tscn");
	}
	public void Run()
	{
		if (OS.HasFeature("Android"))
		{
			StageReached("Request permissions", false);
			OS.RequestPermissions();
		}
		StageReached("Loading settings", false);
		Settings.UpdateSettings(true);
		StageReached("Adding overlays", false);
		Global.Instance.AddOverlay();
		StageReached("Loading maps", false);
		LoadMaps();
		Global.Instance.FinishedLoading();
		StageReached("All done", true);
	}
	public override void _Process(float delta)
	{
		base._Process(delta);
		counter += delta;
		var dots = (int)(counter * 3) % 4;
		label.Text = text + new string('.', dots);
		if (task != null && task.IsFaulted)
			throw task.Exception;
	}
	public void LoadMaps()
	{
		var start = OS.GetTicksUsec();
		bool loaded = Content.Beatmaps.BeatmapLoader.LoadMapsFromDirectory(Global.MapPath);
		var end = OS.GetTicksUsec();
		if (!loaded)
		{
			GD.PrintErr($"Failed to load maps after {(end - start) / 1000}ms");
			return;
		}
		GD.Print($"Took {(end - start) / 1000}ms to load maps");
	}
}
