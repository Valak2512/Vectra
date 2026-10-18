using System.Text.Json;
using System.Text.Json.Serialization;

namespace DualTune.Core;

/// <summary>Profils JSON dans {root}\profiles\, réglages dans {root}\settings.json (spec §9).</summary>
public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter(), new ResponseCurveConverter() },
    };

    private readonly string _root;
    private readonly string _profilesDir;

    public ProfileStore(string rootDir)
    {
        _root = rootDir;
        _profilesDir = Path.Combine(rootDir, "profiles");
    }

    public IReadOnlyList<Profile> LoadAll(out IReadOnlyList<string> errors)
    {
        Directory.CreateDirectory(_profilesDir);
        var profiles = new List<Profile>();
        var errs = new List<string>();

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(_profilesDir, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var p = Read(file);
                if (!names.Add(p.Name))
                    throw new JsonException($"un autre fichier porte déjà le nom « {p.Name} »");
                profiles.Add(p);
            }
            catch (Exception e) when (e is JsonException or IOException or NotSupportedException)
            {
                errs.Add($"Profil ignoré « {Path.GetFileName(file)} » : {e.Message}");
            }
        }

        if (profiles.Count == 0)
        {
            var def = Profile.CreateDefault();
            Save(def);
            profiles.Add(def);
        }

        errors = errs;
        return profiles;
    }

    public void Save(Profile p)
    {
        Directory.CreateDirectory(_profilesDir);
        WriteAtomic(PathFor(p.Name), JsonSerializer.Serialize(p, Json));
    }

    /// <summary>Écrit un profil dans un fichier choisi par l'utilisateur (pour le partager).</summary>
    public void Export(Profile p, string path) => WriteAtomic(path, JsonSerializer.Serialize(p, Json));

    /// <summary>Lit un profil partagé ; lève JsonException / IOException s'il est invalide.</summary>
    public Profile Import(string path) => Read(path);

    /// <summary>Lit, vérifie et ramène dans les plages un fichier de profil.</summary>
    private static Profile Read(string file)
    {
        var p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(file), Json)
                ?? throw new JsonException("fichier vide");
        if (p.SchemaVersion > Profile.CurrentSchemaVersion)
            throw new JsonException($"version {p.SchemaVersion} non prise en charge");
        Validate(p);
        return Normalize(p);
    }

    public void Delete(string name)
    {
        var path = PathFor(name);
        if (File.Exists(path)) File.Delete(path);
    }

    public void Rename(string oldName, string newName)
    {
        var p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(PathFor(oldName)), Json)!;
        Save(p with { Name = newName });
        if (!string.Equals(PathFor(oldName), PathFor(newName), StringComparison.OrdinalIgnoreCase))
            Delete(oldName);
    }

    public AppSettings LoadSettings()
    {
        var path = Path.Combine(_root, "settings.json");
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json) ?? new() : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public void SaveSettings(AppSettings s)
    {
        Directory.CreateDirectory(_root);
        WriteAtomic(Path.Combine(_root, "settings.json"), JsonSerializer.Serialize(s, Json));
    }

    /// <summary>Rejette un profil dont un élément obligatoire vaut null (JSON valide mais incomplet).</summary>
    private static void Validate(Profile p)
    {
        if (string.IsNullOrWhiteSpace(p.Name)) throw new JsonException("nom manquant");
        if (p.LeftStick?.Curve is null || p.RightStick?.Curve is null) throw new JsonException("réglages de stick incomplets");
        if (p.L2?.Curve is null || p.R2?.Curve is null) throw new JsonException("réglages de gâchette incomplets");
        if (p.ButtonMap is null) throw new JsonException("correspondance des boutons manquante");
    }

    /// <summary>Ramène chaque valeur dans la plage de la spec (§5, §6.1) ; NaN devient le minimum.</summary>
    private static Profile Normalize(Profile p) => p with
    {
        LeftStick = Normalize(p.LeftStick),
        RightStick = Normalize(p.RightStick),
        L2 = Normalize(p.L2),
        R2 = Normalize(p.R2),
    };

    private static StickSettings Normalize(StickSettings s)
    {
        s = Migrate(s);
        return s with
        {
            CenterOffsetX = Clamp(s.CenterOffsetX, -0.2f, 0.2f),
            CenterOffsetY = Clamp(s.CenterOffsetY, -0.2f, 0.2f),
            InnerDeadzone = Clamp(s.InnerDeadzone, 0f, 0.5f),
            AxialDeadzone = Clamp(s.AxialDeadzone, 0f, 0.5f),
            OuterDeadzone = Clamp(s.OuterDeadzone, 0f, 0.3f),
            HorizontalSnapAngle = Clamp(s.HorizontalSnapAngle, 0f, 30f),
            VerticalSnapAngle = Clamp(s.VerticalSnapAngle, 0f, 30f),
            AntiDeadzone = Clamp(s.AntiDeadzone, 0f, 0.5f),
            Sensitivity = Clamp(s.Sensitivity, 0.5f, 2f),
        };
    }

    /// <summary>Ancien accrochage unique (avant la v1.1) : repris sur les deux axes s'ils ne sont pas déjà réglés.</summary>
    private static StickSettings Migrate(StickSettings s) => s.LegacyAxisSnapAngle is not { } old ? s
        : s.HorizontalSnapAngle == 0 && s.VerticalSnapAngle == 0
            ? s with { HorizontalSnapAngle = old, VerticalSnapAngle = old, LegacyAxisSnapAngle = null }
            : s with { LegacyAxisSnapAngle = null };

    private static TriggerSettings Normalize(TriggerSettings t) => t with
    {
        InnerDeadzone = Clamp(t.InnerDeadzone, 0f, 0.5f),
        OuterDeadzone = Clamp(t.OuterDeadzone, 0f, 0.5f),
        AntiDeadzone = Clamp(t.AntiDeadzone, 0f, 0.5f),
    };

    private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : Math.Clamp(v, lo, hi);

    private string PathFor(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return Path.Combine(_profilesDir, safe + ".json");
    }

    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }

    private sealed class ResponseCurveConverter : JsonConverter<ResponseCurve>
    {
        public override ResponseCurve Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(JsonSerializer.Deserialize<List<CurvePoint>>(ref reader, options) ?? new());

        public override void Write(Utf8JsonWriter writer, ResponseCurve value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.Points, options);
    }
}
