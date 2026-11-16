using OLED_Sleeper.Storage.Interfaces;

namespace OLED_Sleeper.Features.KeepAwakeApps
{
    /// <summary>
    /// The apps that keep screens awake: a ready-made list to tick, plus apps the user picked. Remembered in
    /// <c>keep-awake-apps.json</c>. Thread-safe: the idle loop reads <see cref="Rules"/> while the UI edits.
    /// </summary>
    public class KeepAwakeAppsSettings
    {
        private const string FileName = "keep-awake-apps.json";

        /// <summary>Common video players, streaming, recording and call apps.</summary>
        public static IReadOnlyList<KeepAwakeRule> Presets { get; } = new List<KeepAwakeRule>
        {
            Preset("VLC", "vlc"),
            Preset("Windows Mediaspeler", "Microsoft.Media.Player", "wmplayer"),
            Preset("Films & TV", "Video.UI"),
            Preset("MPC-HC / MPC-BE", "mpc-hc64", "mpc-hc", "mpc-be64", "mpc-be"),
            Preset("PotPlayer", "PotPlayerMini64", "PotPlayerMini", "PotPlayer64"),
            Preset("Kodi", "kodi"),
            Preset("Plex", "Plex", "Plex HTPC"),
            Preset("Netflix (app)", "Netflix"),
            Preset("OBS Studio", "obs64", "obs32"),
            Preset("Zoom", "Zoom"),
            Preset("Microsoft Teams", "ms-teams", "Teams"),
            Preset("Discord", "Discord"),
        };

        private static KeepAwakeRule Preset(string name, params string[] processes) =>
            new() { Name = name, ProcessNames = processes.ToList(), IsPreset = true };

        private sealed class Stored
        {
            public List<KeepAwakeRule> Rules { get; set; } = new();
        }

        private readonly IAppDataFileStore? _fileStore;
        private readonly object _lock = new();
        private List<KeepAwakeRule> _rules;

        public KeepAwakeAppsSettings(IAppDataFileStore? fileStore = null)
        {
            _fileStore = fileStore;
            _rules = Merge(_fileStore?.Read<Stored>(FileName)?.Rules);
        }

        /// <summary>Fires after the rules changed.</summary>
        public event Action? Changed;

        /// <summary>A copy of all rules: the ready-made ones first, then the user's own.</summary>
        public IReadOnlyList<KeepAwakeRule> Rules
        {
            get { lock (_lock) return _rules.Select(Clone).ToList(); }
        }

        /// <summary>Only the rules that are switched on (what the idle loop needs).</summary>
        public IReadOnlyList<KeepAwakeRule> ActiveRules
        {
            get { lock (_lock) return _rules.Where(r => r.Enabled).Select(Clone).ToList(); }
        }

        public void SetRules(IEnumerable<KeepAwakeRule> rules)
        {
            lock (_lock)
            {
                _rules = Merge(rules.ToList());
                _fileStore?.TryWrite(FileName, new Stored { Rules = _rules });
            }
            Changed?.Invoke();
        }

        /// <summary>
        /// Every ready-made entry is always present (with the user's on/off and scope kept); the user's own
        /// entries follow, without duplicates of a ready-made one.
        /// </summary>
        private static List<KeepAwakeRule> Merge(List<KeepAwakeRule>? stored)
        {
            stored ??= new();
            var result = new List<KeepAwakeRule>();
            foreach (var preset in Presets)
            {
                var saved = stored.FirstOrDefault(r => r.IsPreset && r.Name == preset.Name);
                var rule = Clone(preset);
                if (saved != null) { rule.Enabled = saved.Enabled; rule.Scope = saved.Scope; }
                result.Add(rule);
            }

            foreach (var own in stored.Where(r => !r.IsPreset && r.ProcessNames.Count > 0))
            {
                if (result.Any(r => r.ProcessNames.Any(own.Matches))) continue;
                result.Add(Clone(own));
            }
            return result;
        }

        private static KeepAwakeRule Clone(KeepAwakeRule r) => new()
        {
            Name = r.Name,
            ProcessNames = r.ProcessNames.ToList(),
            Scope = r.Scope,
            Enabled = r.Enabled,
            IsPreset = r.IsPreset
        };
    }
}
