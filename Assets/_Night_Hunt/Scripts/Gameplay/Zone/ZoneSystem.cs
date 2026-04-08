using System;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.Core;

namespace NightHunt.Gameplay.Zone
{
    /// <summary>
    /// Lightweight zone registry — server-authoritative zone lifecycle.
    /// Individual zones (LockdownZone, ZoneBuff…) self-register here on OnEnable.
    ///
    /// LockdownZone.cs handles its OWN FishNet sync — ZoneSystem is a pure C# registry
    /// that lets other systems (e.g. MinimapUI, AI) query all active zones without coupling.
    /// </summary>
    [DisallowMultipleComponent]
    public class ZoneSystem : Singleton<ZoneSystem>
    {
        // ── Registry ─────────────────────────────────────────────────────────────
        private readonly List<IZoneAreaInfo> _zones = new List<IZoneAreaInfo>(8);

        public event Action<IZoneAreaInfo> OnZoneRegistered;
        public event Action<IZoneAreaInfo> OnZoneUnregistered;

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>Register a zone so other systems can query it.</summary>
        public void RegisterZone(IZoneAreaInfo zone)
        {
            if (zone == null || _zones.Contains(zone)) return;
            _zones.Add(zone);
            OnZoneRegistered?.Invoke(zone);
        }

        /// <summary>Unregister a zone (call from OnDisable).</summary>
        public void UnregisterZone(IZoneAreaInfo zone)
        {
            if (zone == null) return;
            if (_zones.Remove(zone))
                OnZoneUnregistered?.Invoke(zone);
        }

        /// <summary>All currently active zones.</summary>
        public IReadOnlyList<IZoneAreaInfo> ActiveZones => _zones;

        /// <summary>Returns every zone affecting <paramref name="position"/>.</summary>
        public void GetZonesAtPosition(Vector3 position, List<IZoneAreaInfo> result)
        {
            result.Clear();
            for (int i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                if (z != null && z.IsActive && z.ContainsPoint(position))
                    result.Add(z);
            }
        }

        /// <summary>Returns the highest-priority zone at position (first registered wins).</summary>
        public IZoneAreaInfo GetPrimaryZoneAtPosition(Vector3 position)
        {
            for (int i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                if (z != null && z.IsActive && z.ContainsPoint(position))
                    return z;
            }
            return null;
        }

        public bool IsAnyZoneActive() => _zones.Count > 0;

        // ── Debug ────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (_zones.Count == 0) return;

            GUILayout.BeginArea(new Rect(Screen.width - 270f, 10f, 260f, 200f),
                GUI.skin.box);
            GUILayout.Label($"<b>ZoneSystem — {_zones.Count} zone(s)</b>");
            foreach (var z in _zones)
            {
                if (z == null) continue;
                GUILayout.Label($"  • {z.ZoneId}  r={z.Radius:F0}  {(z.IsActive ? "ACTIVE" : "off")}");
            }
            GUILayout.EndArea();
        }
#endif
    }

    // ── Zone interface (implemented by LockdownZone, ZoneBuff, future zones) ──

    /// <summary>Minimal contract so ZoneSystem doesn't depend on concrete zone types.</summary>
    public interface IZoneAreaInfo
    {
        string  ZoneId    { get; }
        Vector3 Center    { get; }
        float   Radius    { get; }
        bool    IsActive  { get; }
        bool ContainsPoint(Vector3 worldPos);
    }
}
