using System;
using System.Collections.Generic;

namespace Orion
{
    /// <summary>
    /// Per-entity shortcut dispatch (macOS parity): user-recorded chords that
    /// switch to a tab group or a profile, read from the entity records
    /// (TabGroup.KeyboardShortcut / Profile.KeyboardShortcut, GTK accelerator
    /// names). Dispatch order matches macOS: tab groups, then profiles; the
    /// active profile's own shortcut falls through. Built-in shortcuts always
    /// run first — callers try the shortcut table before this. UI-thread only.
    /// </summary>
    internal static class EntityShortcuts
    {
        // Profiles live in the common registry; cached so dispatch never hits
        // the DB per keypress. Groups are read live from the window's
        // ViewModel (saved groups are shared models, so any window sees all).
        private static List<(KeyCombo Combo, Profile Profile)> _profileBindings = [];

        // Built-in shortcut combos → display names, for the recorder conflict
        // check (macOS blocks chords taken by the menu bar, naming the item).
        private static readonly Dictionary<KeyCombo, string> _builtinNames = [];

        /// <summary>Registered by MainWindowContent for every built-in shortcut.</summary>
        public static void RegisterBuiltin(KeyCombo combo, string displayName) =>
            _builtinNames[combo] = displayName;

        /// <summary>
        /// Display name of the built-in shortcut holding combo, or null.
        /// Recorders refuse such chords — a built-in always wins at dispatch,
        /// so an entity binding on the same combo would be silently dead.
        /// </summary>
        public static string? BuiltinUsing(KeyCombo combo) =>
            _builtinNames.TryGetValue(combo, out string? name) ? name : null;

        /// <summary>Raised after Refresh; windows rebuild their entity accelerators.</summary>
        public static event Action? Changed;

        /// <summary>Call after any in-process entity shortcut write (profile save, group update).</summary>
        public static void Refresh()
        {
            var bindings = new List<(KeyCombo, Profile)>();
            foreach (Profile profile in OrionBackend.GetProfiles())
            {
                // Unparseable values (legacy free text) are inert, never rewritten.
                if (KeyCombo.TryParseGtkAccel(profile.KeyboardShortcut, out KeyCombo combo))
                    bindings.Add((combo, profile));
            }
            _profileBindings = bindings;
            Changed?.Invoke();
        }

        public static bool Dispatch(KeyCombo combo, MainWindowViewModel window)
        {
            if (GroupWith(combo, window, exclude: null) is TabGroup group)
            {
                window.SelectTabGroup(group);
                return true;
            }
            foreach ((KeyCombo c, Profile profile) in _profileBindings)
            {
                if (c != combo)
                    continue;
                // The active profile's shortcut deliberately falls through (macOS parity).
                if (profile.Id == OrionBackend.GetCurrentProfileId())
                    return false;
                MainWindow.FocusOrOpenForProfile(profile);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Name of another group already using combo — the rename dialog's
        /// conflict check (conflicts block, never steal).
        /// </summary>
        public static string? GroupUsing(KeyCombo combo, MainWindowViewModel window, TabGroup exclude) =>
            GroupWith(combo, window, exclude)?.Name;

        /// <summary>
        /// Name of another profile already using combo — the profile
        /// recorder's conflict check. A duplicate would silently shadow the
        /// later profile (dispatch is first match), so it is blocked.
        /// </summary>
        public static string? ProfileUsing(KeyCombo combo, long excludeProfileId)
        {
            foreach ((KeyCombo c, Profile profile) in _profileBindings)
            {
                if (c == combo && profile.Id != excludeProfileId)
                    return profile.Name;
            }
            return null;
        }

        /// <summary>Combos a window registers chrome-focus accelerators for.</summary>
        public static IEnumerable<KeyCombo> CandidateCombos(MainWindowViewModel window)
        {
            var seen = new HashSet<KeyCombo>();
            foreach (TabGroupViewModel vm in window.TabGroups)
            {
                if (KeyCombo.TryParseGtkAccel(vm.Model.KeyboardShortcut, out KeyCombo combo) && seen.Add(combo))
                    yield return combo;
            }
            foreach ((KeyCombo combo, Profile _) in _profileBindings)
            {
                if (seen.Add(combo))
                    yield return combo;
            }
        }

        private static TabGroup? GroupWith(KeyCombo combo, MainWindowViewModel window, TabGroup? exclude)
        {
            foreach (TabGroupViewModel vm in window.TabGroups)
            {
                TabGroup group = vm.Model;
                if (group != exclude
                    && KeyCombo.TryParseGtkAccel(group.KeyboardShortcut, out KeyCombo c)
                    && c == combo)
                {
                    return group;
                }
            }
            return null;
        }
    }
}
