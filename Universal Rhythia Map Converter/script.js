// script.js

var tabSettings = document.getElementById("tabSettings");
var tabConverter = document.getElementById("tabConverter");
var settingsView = document.getElementById("settingsView");
var converterView = document.getElementById("converterView");
var selectorGroup = document.querySelector(".selector-group");
var modeRadios = document.querySelectorAll('input[name="mode"]');
var downloadRadios = document.querySelectorAll('input[name="downloadFormat"]');
var converterR2VBtn = document.getElementById("converterR2VBtn");
var converterV2RBtn = document.getElementById("converterV2RBtn");
var converterToggle = document.querySelector(".converter-toggle");
var r2vForm = document.getElementById("r2vForm");
var v2rForm = document.getElementById("v2rForm");
var statusR2V = document.getElementById("statusR2V");
var statusV2R = document.getElementById("statusV2R");
var currentDownloadFormat = "zip";

/* Tab switching */

tabSettings.addEventListener("click", function () {
  tabSettings.classList.add("active");
  tabConverter.classList.remove("active");
  selectorGroup.classList.add("settings-active");
  selectorGroup.classList.remove("converter-active");
  settingsView.classList.remove("hidden");
  converterView.classList.add("hidden");
});

tabConverter.addEventListener("click", function () {
  tabConverter.classList.add("active");
  tabSettings.classList.remove("active");
  selectorGroup.classList.add("converter-active");
  selectorGroup.classList.remove("settings-active");
  converterView.classList.remove("hidden");
  settingsView.classList.add("hidden");
});

/* Display mode */

modeRadios.forEach(function (r) {
  r.addEventListener("change", function () {
    if (!r.checked) return;
    if (r.value === "full") {
      document.body.classList.remove("mode-lyte");
      document.body.classList.add("mode-full");
    } else {
      document.body.classList.remove("mode-full");
      document.body.classList.add("mode-lyte");
    }
  });
});

/* Download format */

downloadRadios.forEach(function (r) {
  r.addEventListener("change", function () {
    if (r.checked) {
      currentDownloadFormat = r.value;
    }
  });
});

/* Converter toggle */

converterR2VBtn.addEventListener("click", function () {
  converterR2VBtn.classList.add("active");
  converterV2RBtn.classList.remove("active");
  converterToggle.classList.add("r2v-active");
  converterToggle.classList.remove("v2r-active");
  r2vForm.classList.remove("hidden");
  v2rForm.classList.add("hidden");
  statusR2V.textContent = "";
  statusV2R.textContent = "";
});

converterV2RBtn.addEventListener("click", function () {
  converterV2RBtn.classList.add("active");
  converterR2VBtn.classList.remove("active");
  converterToggle.classList.add("v2r-active");
  converterToggle.classList.remove("r2v-active");
  v2rForm.classList.remove("hidden");
  r2vForm.classList.add("hidden");
  statusR2V.textContent = "";
  statusV2R.textContent = "";
});

/* Quantum-aware parsing and mapping
   Correlation:
   - Rhythia lanes are treated as a continuous axis [0, 2]
   - Vulnus coordinates are continuous [-1, 1]
   - Mapping: x = laneX - 1, y = laneY - 1
   - Inverse: laneX = x + 1, laneY = y + 1
   This preserves integer lanes (0,1,2) and supports fractional "quantum" lanes.
*/

function parseRhythia(txt) {
  var trimmed = txt.trim();
  if (!trimmed) return null;

  var firstComma = trimmed.indexOf(",");
  if (firstComma === -1) return null;

  var mp3Prefix = trimmed.substring(0, firstComma);
  var rest = trimmed.substring(firstComma + 1);
  var parts = rest.split(",");

  var notes = [];

  for (var i = 0; i < parts.length; i++) {
    var p = parts[i].trim();
    if (!p) continue;

    var segs = p.split("|");
    if (segs.length !== 3) continue;

    // Quantum-aware: allow fractional lanes and times
    var laneX = parseFloat(segs[0]);
    var laneY = parseFloat(segs[1]);
    var timeMs = parseFloat(segs[2]);

    if (isNaN(laneX) || isNaN(laneY) || isNaN(timeMs)) continue;

    // Continuous mapping: lane 0 → -1, lane 1 → 0, lane 2 → 1
    // Fractional lanes map linearly (e.g., 0.5 → -0.5, 1.5 → 0.5)
    var x = laneX - 1;
    var y = laneY - 1;

    var time = timeMs / 1000;
    notes.push({ _time: time, _x: x, _y: y });
  }

  return { mp3Prefix: mp3Prefix, notes: notes };
}

function buildConvertedJson(mapName, notes) {
  return {
    _approachDistance: 50,
    _approachTime: 1,
    _name: mapName || "",
    _notes: notes
  };
}

function buildMetaJson(title, artist, mapper, musicName, difficultyFile) {
  return {
    _artist: artist || "",
    _difficulties: [difficultyFile || "official.json"],
    _mappers: [mapper || ""],
    _music: musicName || "",
    _title: title || "",
    _version: 1
  };
}

// Inverse mapping: continuous coordinates back to continuous lanes
function xToLane(x) {
  return x + 1;
}

function yToLane(y) {
  return y + 1;
}

function buildRhythiaFromConverted(jsonObj, mp3Prefix) {
  var notes = jsonObj._notes || [];
  var prefix = mp3Prefix || "";
  var parts = [];

  for (var i = 0; i < notes.length; i++) {
    var n = notes[i];

    var laneX = xToLane(n._x);
    var laneY = yToLane(n._y);

    // Preserve fractional lanes; times converted to ms with rounding
    var timeMs = Math.round(n._time * 1000);

    // Use minimal string representation for fractional values
    var laneXStr = Number.isInteger(laneX) ? String(laneX) : laneX.toString();
    var laneYStr = Number.isInteger(laneY) ? String(laneY) : laneY.toString();

    parts.push(laneXStr + "|" + laneYStr + "|" + timeMs);
  }

  return prefix + "," + parts.join(",");
}

function downloadBlob(content, filename, type) {
  var blob = new Blob([content], { type: type || "application/octet-stream" });
  saveAs(blob, filename);
}

/* Rhythia → Vulnus */

document.getElementById("convertR2V").addEventListener("click", function () {
  statusR2V.textContent = "";

  var txt = document.getElementById("rhythiaInput").value;
  var title = document.getElementById("titleInput").value;
  var artist = document.getElementById("artistInput").value;
  var mapper = document.getElementById("mapperInput").value;
  var mapName = document.getElementById("mapNameInput").value;
  var difficultyFile = document.getElementById("difficultyInput").value || "official.json";
  var mp3Fallback = document.getElementById("mp3NameInput").value;
  var mp3FileInput = document.getElementById("mp3FileInput");

  var parsed = parseRhythia(txt);
  if (!parsed || parsed.notes.length === 0) {
    statusR2V.textContent = "Invalid Rhythia data (no valid quantum/fractional notes found).";
    return;
  }

  var converted = buildConvertedJson(
    mapName || title || parsed.mp3Prefix,
    parsed.notes
  );

  var mp3File = mp3FileInput.files && mp3FileInput.files[0] ? mp3FileInput.files[0] : null;
  var mp3Name;

  if (mp3File) {
    mp3Name = mp3File.name;
  } else if (mp3Fallback) {
    mp3Name = mp3Fallback;
  } else {
    mp3Name = parsed.mp3Prefix + ".mp3";
  }

  var meta = buildMetaJson(title, artist, mapper, mp3Name, difficultyFile);
  var convertedJsonString = JSON.stringify(converted);
  var metaJsonString = JSON.stringify(meta);
  var zipNameBase = (artist || "Artist") + " - " + (title || "Map");

  if (currentDownloadFormat === "zip") {
    var zip = new JSZip();
    zip.file(difficultyFile, convertedJsonString);
    zip.file("meta.json", metaJsonString);
    if (mp3File) {
      zip.file(mp3Name, mp3File);
    }
    zip
      .generateAsync({ type: "blob" })
      .then(function (content) {
        saveAs(content, zipNameBase + ".zip");
        statusR2V.textContent = "Zipped map downloaded (quantum lanes preserved as fractional coordinates).";
      })
      .catch(function () {
        statusR2V.textContent = "Error creating zip.";
      });
  } else {
    downloadBlob(convertedJsonString, difficultyFile, "application/json");
    downloadBlob(metaJsonString, "meta.json", "application/json");
    if (mp3File) {
      saveAs(mp3File, mp3Name);
    }
    statusR2V.textContent = "Files downloaded separately (quantum lanes preserved).";
  }
});

/* Vulnus → Rhythia */

document.getElementById("convertV2R").addEventListener("click", function () {
  statusV2R.textContent = "";

  var jsonText = document.getElementById("convertedJsonInput").value;
  var mp3Prefix = document.getElementById("mp3PrefixInput").value;

  if (!jsonText.trim()) {
    statusV2R.textContent = "converted.json data is required.";
    return;
  }

  var obj;
  try {
    obj = JSON.parse(jsonText);
  } catch (e) {
    statusV2R.textContent = "Invalid JSON.";
    return;
  }

  var rhythiaData = buildRhythiaFromConverted(obj, mp3Prefix);
  var fileName = (mp3Prefix || "map") + ".txt";

  downloadBlob(rhythiaData, fileName, "text/plain");
  statusV2R.textContent = "Rhythia .txt downloaded (fractional quantum lanes encoded as continuous lane values).";
});
