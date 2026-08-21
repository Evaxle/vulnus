using Godot;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Compatibility.SSP
{
    public static class SspmImporter
    {
        public static string Import(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
                {
                    if (reader.ReadUInt32() != 0x6D2B5353) return null;
                    var version = reader.ReadUInt16();
                    stream.Position = 0;
                    if (version == 1) return ImportV1(reader);
                    if (version == 2)
                    {
                        var full = ImportV2(reader);
                        if (full != null) return full;
                        stream.Position = 0;
                        return ImportV2OptimizedFromStart(reader);
                    }
                    return null;
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"SSPM import failed: {e.Message}");
                return null;
            }
        }

        public static void ImportDirectory(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var path in Directory.GetFiles(directory, "*.sspm", SearchOption.TopDirectoryOnly)) Import(path);
        }

        public static string ReadMapId(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
                {
                    if (reader.ReadUInt32() != 0x6D2B5353) return null;
                    var version = reader.ReadUInt16();
                    if (version == 1)
                    {
                        reader.ReadUInt16();
                        return ReadLine(reader);
                    }
                    if (version != 2) return null;
                    reader.ReadUInt32();
                    reader.ReadBytes(20);
                    reader.ReadUInt32();
                    reader.ReadUInt32();
                    reader.ReadUInt32();
                    reader.ReadByte();
                    reader.ReadUInt16();
                    reader.ReadByte();
                    reader.ReadByte();
                    reader.ReadByte();
                    for (var i = 0; i < 10; i++) reader.ReadUInt64();
                    return ReadString(reader);
                }
            }
            catch { return null; }
        }

        private static string ImportV1(BinaryReader reader)
        {
            reader.ReadUInt32();
            reader.ReadUInt16();
            reader.ReadUInt16();
            var id = ReadLine(reader) ?? string.Empty;
            var mapName = ReadLine(reader) ?? id;
            var mapper = ReadLine(reader) ?? "Unknown";
            reader.ReadUInt32();
            var count = reader.ReadUInt32();
            var difficulty = DifficultyName(reader.ReadByte());
            var coverType = reader.ReadByte();
            var cover = coverType == 2 ? ReadSizedBlock(reader) : Array.Empty<byte>();
            var audioType = reader.ReadByte();
            var audio = audioType == 1 ? ReadSizedBlock(reader) : Array.Empty<byte>();
            var notes = new List<JObject>();
            for (var i = 0; i < count; i++)
            {
                var time = reader.ReadUInt32();
                var storage = reader.ReadByte();
                float x;
                float y;
                if (storage == 0)
                {
                    x = reader.ReadByte() - 1;
                    y = -(reader.ReadByte() - 1);
                }
                else if (storage == 1)
                {
                    x = -(reader.ReadSingle() - 1f);
                    y = -(reader.ReadSingle() - 1f);
                }
                else return null;
                notes.Add(Note(time, x, y, id));
            }
            var separator = mapName.IndexOf(" - ", StringComparison.Ordinal);
            var artist = separator > 0 ? mapName.Substring(0, separator) : string.Empty;
            var title = separator > 0 ? mapName.Substring(separator + 3) : mapName;
            return WriteVulnus(id, artist, title, mapper, difficulty, notes, audio, cover);
        }

        private static string ImportV2(BinaryReader reader)
        {
            reader.ReadUInt32();
            reader.ReadUInt16();
            reader.ReadUInt32();
            reader.ReadBytes(20);
            reader.ReadUInt32();
            var noteCount = reader.ReadUInt32();
            var markerCount = reader.ReadUInt32();
            var difficulty = DifficultyName(reader.ReadByte());
            reader.ReadUInt16();
            var hasAudio = reader.ReadByte() != 0;
            var hasCover = reader.ReadByte() != 0;
            reader.ReadByte();
            reader.ReadUInt64();
            reader.ReadUInt64();
            var audioOffset = reader.ReadUInt64();
            var audioLength = reader.ReadUInt64();
            var coverOffset = reader.ReadUInt64();
            var coverLength = reader.ReadUInt64();
            var definitionsOffset = reader.ReadUInt64();
            reader.ReadUInt64();
            var markersOffset = reader.ReadUInt64();
            reader.ReadUInt64();
            var id = ReadString(reader);
            var mapName = ReadString(reader);
            ReadString(reader);
            var mapperCount = reader.ReadUInt16();
            var mappers = new List<string>();
            for (var i = 0; i < mapperCount; i++) mappers.Add(ReadString(reader));
            if (definitionsOffset == 0) return null;
            reader.BaseStream.Position = (long)definitionsOffset;
            var definitionCount = reader.ReadByte();
            var definitions = new List<List<byte>>();
            var noteDefinition = -1;
            for (var i = 0; i < definitionCount; i++)
            {
                var name = ReadString(reader);
                var valueCount = reader.ReadByte();
                var types = new List<byte>();
                for (var j = 0; j < valueCount; j++) types.Add(reader.ReadByte());
                reader.ReadByte();
                definitions.Add(types);
                if (name == "ssp_note") noteDefinition = i;
            }
            if (noteDefinition < 0) return null;
            var audio = hasAudio ? ReadBlock(reader, audioOffset, audioLength) : Array.Empty<byte>();
            var cover = hasCover ? ReadBlock(reader, coverOffset, coverLength) : Array.Empty<byte>();
            reader.BaseStream.Position = (long)markersOffset;
            var notes = new List<JObject>();
            for (var i = 0; i < markerCount; i++)
            {
                var time = reader.ReadUInt32();
                var markerType = reader.ReadByte();
                if (markerType >= definitions.Count) return null;
                var types = definitions[markerType];
                if (markerType == noteDefinition && types.Count == 1 && types[0] == 0x07)
                {
                    var storage = reader.ReadByte();
                    float x;
                    float y;
                    if (storage == 0)
                    {
                        x = reader.ReadByte() - 1;
                        y = -(reader.ReadByte() - 1);
                    }
                    else if (storage == 1)
                    {
                        x = -(reader.ReadSingle() - 1f);
                        y = -(reader.ReadSingle() - 1f);
                    }
                    else return null;
                    notes.Add(Note(time, x, y, id));
                }
                else
                {
                    foreach (var type in types) SkipValue(reader, type);
                }
            }
            if (notes.Count != noteCount) return null;
            var separator = mapName.IndexOf(" - ", StringComparison.Ordinal);
            var artist = separator > 0 ? mapName.Substring(0, separator) : string.Join(" & ", mappers);
            var title = separator > 0 ? mapName.Substring(separator + 3) : mapName;
            return WriteVulnus(id, artist, title, string.Join(" & ", mappers), difficulty, notes, audio, cover);
        }

        private static string ImportV2OptimizedFromStart(BinaryReader reader)
        {
            reader.ReadUInt32();
            reader.ReadUInt16();
            reader.ReadUInt32();
            reader.ReadBytes(20);
            reader.ReadUInt32();
            var noteCount = reader.ReadUInt32();
            var markerCount = reader.ReadUInt32();
            var difficulty = DifficultyName(reader.ReadByte());
            reader.ReadUInt16();
            var hasAudio = reader.ReadByte() != 0;
            var hasCover = reader.ReadByte() != 0;
            reader.ReadByte();
            reader.ReadUInt64();
            reader.ReadUInt64();
            var audioOffset = reader.ReadUInt64();
            var audioLength = reader.ReadUInt64();
            var coverOffset = reader.ReadUInt64();
            var coverLength = reader.ReadUInt64();
            reader.ReadUInt64();
            reader.ReadUInt64();
            var markersOffset = reader.ReadUInt64();
            reader.ReadUInt64();
            var id = ReadString(reader);
            var mapName = ReadString(reader);
            ReadString(reader);
            var mapperCount = reader.ReadUInt16();
            var mappers = new List<string>();
            for (var i = 0; i < mapperCount; i++) mappers.Add(ReadString(reader));
            var audio = hasAudio ? ReadBlock(reader, audioOffset, audioLength) : Array.Empty<byte>();
            var cover = hasCover ? ReadBlock(reader, coverOffset, coverLength) : Array.Empty<byte>();
            reader.BaseStream.Position = (long)markersOffset;
            var notes = new List<JObject>();
            for (var i = 0; i < markerCount; i++)
            {
                var time = reader.ReadUInt32();
                var storage = reader.ReadByte();
                float x;
                float y;
                if (storage == 0)
                {
                    x = reader.ReadByte() - 1;
                    y = -(reader.ReadByte() - 1);
                }
                else if (storage == 1)
                {
                    x = -(reader.ReadSingle() - 1f);
                    y = -(reader.ReadSingle() - 1f);
                }
                else return null;
                notes.Add(Note(time, x, y, id));
            }
            if (notes.Count != noteCount) return null;
            var separator = mapName.IndexOf(" - ", StringComparison.Ordinal);
            var artist = separator > 0 ? mapName.Substring(0, separator) : string.Join(" & ", mappers);
            var title = separator > 0 ? mapName.Substring(separator + 3) : mapName;
            return WriteVulnus(id, artist, title, string.Join(" & ", mappers), difficulty, notes, audio, cover);
        }

        private static JObject Note(uint time, float x, float y, string id)
        {
            var note = new JObject();
            note["_time"] = time / 1000f;
            note["_x"] = x;
            note["_y"] = y;
            note["rhythiansMapId"] = id;
            return note;
        }

        private static string WriteVulnus(string id, string artist, string title, string mapper, string difficulty, List<JObject> notes, byte[] audio, byte[] cover)
        {
            if (string.IsNullOrWhiteSpace(id) || notes.Count == 0) return null;
            Directory.CreateDirectory(Global.MapPath);
            var output = Global.MapPath.PlusFile("rhythians_" + Sanitize(id) + ".vul");
            var temp = output + ".tmp";
            if (File.Exists(temp)) File.Delete(temp);
            using (var stream = File.Create(temp))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteJson(zip, "meta.json", new JObject
                {
                    ["_version"] = 1,
                    ["_artist"] = artist,
                    ["_title"] = title,
                    ["_mappers"] = new JArray(mapper),
                    ["_difficulties"] = new JArray("converted.json"),
                    ["_music"] = audio.Length > 0 ? "music.bin" : "",
                    ["rhythiansMapId"] = id
                });
                WriteJson(zip, "converted.json", new JObject
                {
                    ["_version"] = 1,
                    ["_name"] = difficulty,
                    ["_notes"] = new JArray(notes),
                    ["rhythiansMapId"] = id
                });
                if (audio.Length > 0) WriteBytes(zip, "music.bin", audio);
                if (cover.Length > 0) WriteBytes(zip, "cover.png", cover);
            }
            if (File.Exists(output)) File.Delete(output);
            File.Move(temp, output);
            return output;
        }

        private static void SkipValue(BinaryReader reader, byte type)
        {
            switch (type)
            {
                case 0x01: reader.ReadByte(); break;
                case 0x02: reader.ReadUInt16(); break;
                case 0x03: reader.ReadUInt32(); break;
                case 0x04: reader.ReadUInt64(); break;
                case 0x05: reader.ReadSingle(); break;
                case 0x06: reader.ReadDouble(); break;
                case 0x07:
                    var storage = reader.ReadByte();
                    if (storage == 0) { reader.ReadByte(); reader.ReadByte(); }
                    else if (storage == 1) { reader.ReadSingle(); reader.ReadSingle(); }
                    else throw new InvalidDataException("Invalid SSPM position storage type.");
                    break;
                case 0x08:
                case 0x09:
                    reader.ReadBytes(reader.ReadUInt16());
                    break;
                case 0x0A:
                case 0x0B:
                    var length = reader.ReadUInt32();
                    if (length > int.MaxValue) throw new InvalidDataException("SSPM value is too large.");
                    reader.ReadBytes((int)length);
                    break;
                case 0x0C:
                    var arrayType = reader.ReadByte();
                    var arrayLength = reader.ReadUInt16();
                    for (var i = 0; i < arrayLength; i++) SkipValue(reader, arrayType);
                    break;
                default: throw new InvalidDataException($"Unsupported SSPM data type: {type:X2}");
            }
        }

        private static byte[] ReadSizedBlock(BinaryReader reader)
        {
            var length = reader.ReadUInt64();
            return length > int.MaxValue ? Array.Empty<byte>() : reader.ReadBytes((int)length);
        }

        private static byte[] ReadBlock(BinaryReader reader, ulong offset, ulong length)
        {
            if (length > int.MaxValue || offset > (ulong)reader.BaseStream.Length || length > (ulong)reader.BaseStream.Length - offset) return Array.Empty<byte>();
            reader.BaseStream.Position = (long)offset;
            return reader.ReadBytes((int)length);
        }

        private static string ReadString(BinaryReader reader)
        {
            var length = reader.ReadUInt16();
            return System.Text.Encoding.UTF8.GetString(reader.ReadBytes(length));
        }

        private static string ReadLine(BinaryReader reader)
        {
            var bytes = new List<byte>();
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var value = reader.ReadByte();
                if (value == 10) break;
                if (value != 13) bytes.Add(value);
            }
            return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
        }

        private static string DifficultyName(byte value)
        {
            switch (value)
            {
                case 1: return "Easy";
                case 2: return "Medium";
                case 3: return "Hard";
                case 4: return "Logic";
                case 5: return "Tasukete";
                default: return "Unknown";
            }
        }

        private static string Sanitize(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value;
        }

        private static void WriteJson(ZipArchive zip, string name, JObject json)
        {
            using (var writer = new StreamWriter(zip.CreateEntry(name).Open(), new System.Text.UTF8Encoding(false))) writer.Write(json.ToString(Formatting.None));
        }

        private static void WriteBytes(ZipArchive zip, string name, byte[] data)
        {
            using (var stream = zip.CreateEntry(name).Open()) stream.Write(data, 0, data.Length);
        }
    }
}
