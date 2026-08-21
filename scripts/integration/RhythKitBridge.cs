using Godot;
using System;
using System.IO;
using System.Text.Json;

public static class RhythKitBridge
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CapoRhythia", "Rhythians", "bridge");
    private static readonly string EventPath = Path.Combine(DirectoryPath, "events-vulnus.jsonl");

    public static void Send(string eventName, bool running, string? mapId = null, string? clientScoreId = null, double? accuracy = null, int? misses = null, double? speed = null, bool? qualified = null)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var payload = new
            {
                Event = eventName,
                Running = running,
                IntegrationConnected = true,
                MapCaptureReady = !string.IsNullOrWhiteSpace(mapId),
                Game = "Vulnus",
                GameVersion = "Vulnus",
                MapId = mapId,
                ClientScoreId = clientScoreId,
                Accuracy = accuracy,
                Misses = misses,
                Speed = speed,
                CompletedAt = DateTimeOffset.UtcNow,
                ResultQualified = qualified
            };
            File.AppendAllText(EventPath, JsonSerializer.Serialize(payload) + Environment.NewLine);
        }
        catch (Exception e)
        {
            GD.PrintErr($"RhythKit bridge error: {e.Message}");
        }
    }
}
