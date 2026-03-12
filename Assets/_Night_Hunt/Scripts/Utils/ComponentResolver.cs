using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Utilities
{
    /// <summary>
    /// Fluent, allocation-minimal component resolver.
    ///
    /// USAGE — simple one-liner:
    ///   _weaponSystem = ComponentResolver.Find&lt;IWeaponSystem&gt;(this)
    ///                       .OnSelf().InChildren().InParent().OrLogError("WeaponSystem missing")
    ///                       .Resolve();
    ///
    /// USAGE — with an explicit SerializeField fallback (recommended pattern):
    ///   _weaponSystem = ComponentResolver.Find&lt;IWeaponSystem&gt;(this)
    ///                       .UseExisting(_weaponSystemSource as IWeaponSystem)  // Inspector field first
    ///                       .OnSelf().InChildren().InParent()
    ///                       .OrLogWarning("WeaponSystem not found — assign in Inspector")
    ///                       .Resolve();
    ///
    /// SEARCH ORDER:
    ///   Steps are executed left-to-right in the order you call them.
    ///   Resolution stops at the first non-null hit.
    ///
    ///   UseExisting(T)   — use a pre-assigned value (from Inspector SerializeField)
    ///   OnSelf()         — GetComponent on the calling GameObject
    ///   InChildren()     — GetComponentInChildren (includes self, includes inactive)
    ///   InParent()       — GetComponentInParent (includes self, includes inactive)
    ///   OnRoot()         — GetComponent on transform.root
    ///   InRootChildren() — GetComponentInChildren on transform.root (full hierarchy)
    ///   OnGameObject(go) — GetComponent on a specific GameObject
    ///   Via(Func&lt;T&gt;)    — custom lambda (e.g. singleton lookup, ServiceLocator)
    ///
    /// FALLBACK / LOGGING:
    ///   OrDefault(T)     — return a default if nothing found (no log)
    ///   OrLogWarning(msg)— log warning if nothing found, return null
    ///   OrLogError(msg)  — log error if nothing found, return null
    ///   OrThrow(msg)     — throw InvalidOperationException if nothing found
    ///
    /// MULTIPLE RESULTS:
    ///   ResolveAll()     — returns List&lt;T&gt; of all found components
    ///                      (runs every registered step, no early-exit)
    ///
    /// NOTE: Each search step runs Unity's native GetComponent API first.
    ///       If native returns null (e.g. interface on FishNet NetworkBehaviour),
    ///       a manual MonoBehaviour scan is performed as a transparent fallback.
    ///       Public API is unchanged — callers need no modification.
    /// </summary>
    public static class ComponentResolver
    {
        // ── Entry point ────────────────────────────────────────────────────────

        /// <summary>Begin a resolution chain for <typeparamref name="T"/> relative to <paramref name="context"/>.</summary>
        public static Builder<T> Find<T>(Component context) where T : class
            => new Builder<T>(context.gameObject);

        /// <summary>Begin a resolution chain for <typeparamref name="T"/> relative to <paramref name="go"/>.</summary>
        public static Builder<T> Find<T>(GameObject go) where T : class
            => new Builder<T>(go);

        // ── Internal helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Scans <paramref name="monos"/> for the first entry that implements <typeparamref name="T"/>.
        /// Uses indexed for-loop to avoid enumerator allocation. Early-exits on first match.
        /// </summary>
        private static T ManualScan<T>(MonoBehaviour[] monos) where T : class
        {
            for (int i = 0; i < monos.Length; i++)
                if (monos[i] is T match) return match;
            return null;
        }

        // ── Builder ────────────────────────────────────────────────────────────

        /// <summary>
        /// Fluent builder. Each search step follows the same two-phase pattern:
        ///   1. Unity native GetComponent API  — fast path, zero extra allocation.
        ///   2. Manual MonoBehaviour scan      — fallback for interfaces on
        ///      NetworkBehaviour (FishNet) that Unity's reflection may miss.
        /// The fallback is transparent: callers keep using the same public API.
        /// </summary>
        public sealed class Builder<T> where T : class
        {
            private readonly GameObject    _go;
            private readonly List<Func<T>> _steps   = new List<Func<T>>(4);
            private          T             _default = null;
            private          string        _warnMsg = null;
            private          string        _errMsg  = null;
            private          bool          _throws  = false;
            private          string        _throwMsg;

            internal Builder(GameObject go) => _go = go;

            // ── Search steps ──────────────────────────────────────────────────

            /// <summary>Use an already-resolved value (e.g. a SerializeField) — skips search if non-null.</summary>
            public Builder<T> UseExisting(T existing)
            {
                if (existing != null) _steps.Add(() => existing);
                return this;
            }

            /// <summary>
            /// GetComponent on the context GO itself.
            /// Falls back to manual MonoBehaviour scan if native returns null.
            /// </summary>
            public Builder<T> OnSelf()
            {
                _steps.Add(() =>
                {
                    var native = _go.GetComponent<T>();
                    if (native != null) return native;

                    return ManualScan<T>(_go.GetComponents<MonoBehaviour>());
                });
                return this;
            }

            /// <summary>
            /// GetComponentInChildren (includes self + inactive) on the context GO.
            /// Falls back to manual MonoBehaviour scan if native returns null.
            /// </summary>
            public Builder<T> InChildren(bool includeInactive = true)
            {
                _steps.Add(() =>
                {
                    var native = _go.GetComponentInChildren<T>(includeInactive);
                    if (native != null) return native;

                    return ManualScan<T>(_go.GetComponentsInChildren<MonoBehaviour>(includeInactive));
                });
                return this;
            }

            /// <summary>
            /// GetComponentInParent (includes self + inactive) on the context GO.
            /// Falls back to manual per-node MonoBehaviour scan if native returns null.
            /// </summary>
            public Builder<T> InParent(bool includeInactive = true)
            {
                _steps.Add(() =>
                {
                    var native = _go.GetComponentInParent<T>(includeInactive);
                    if (native != null) return native;

                    // Manual: walk up the hierarchy node-by-node, early-exit on first match.
                    var current = _go.transform;
                    while (current != null)
                    {
                        if (includeInactive || current.gameObject.activeInHierarchy)
                        {
                            var match = ManualScan<T>(current.GetComponents<MonoBehaviour>());
                            if (match != null) return match;
                        }
                        current = current.parent;
                    }
                    return null;
                });
                return this;
            }

            /// <summary>
            /// GetComponent on transform.root (the very top of the prefab hierarchy).
            /// Falls back to manual MonoBehaviour scan if native returns null.
            /// </summary>
            public Builder<T> OnRoot()
            {
                _steps.Add(() =>
                {
                    var root   = _go.transform.root;
                    var native = root.GetComponent<T>();
                    if (native != null) return native;

                    return ManualScan<T>(root.GetComponents<MonoBehaviour>());
                });
                return this;
            }

            /// <summary>
            /// GetComponentInChildren on transform.root — searches the ENTIRE prefab hierarchy.
            /// Falls back to manual MonoBehaviour scan if native returns null.
            /// </summary>
            public Builder<T> InRootChildren(bool includeInactive = true)
            {
                _steps.Add(() =>
                {
                    var rootGo = _go.transform.root.gameObject;
                    var native = rootGo.GetComponentInChildren<T>(includeInactive);
                    if (native != null) return native;

                    return ManualScan<T>(rootGo.GetComponentsInChildren<MonoBehaviour>(includeInactive));
                });
                return this;
            }

            /// <summary>GetComponent on a specific named child, with deep-search fallback.</summary>
            public Builder<T> InNamedChild(string childName, bool includeInactive = true)
            {
                _steps.Add(() =>
                {
                    var child = _go.transform.root.Find(childName)
                             ?? FindDeepByName(_go.transform.root, childName);
                    if (child == null) return null;

                    var native = child.GetComponent<T>();
                    if (native != null) return native;

                    return ManualScan<T>(child.GetComponents<MonoBehaviour>());
                });
                return this;
            }

            /// <summary>
            /// GetComponent on a specific GameObject (e.g. a manager singleton).
            /// Falls back to manual MonoBehaviour scan if native returns null.
            /// </summary>
            public Builder<T> OnGameObject(GameObject target)
            {
                if (target != null)
                {
                    _steps.Add(() =>
                    {
                        var native = target.GetComponent<T>();
                        if (native != null) return native;

                        return ManualScan<T>(target.GetComponents<MonoBehaviour>());
                    });
                }
                return this;
            }

            /// <summary>Custom resolution delegate — use for singletons, ServiceLocators, etc.</summary>
            public Builder<T> Via(Func<T> resolver)
            {
                if (resolver != null) _steps.Add(resolver);
                return this;
            }

            // ── Fallback / logging config ─────────────────────────────────────

            /// <summary>Return <paramref name="fallback"/> if nothing is found (no log emitted).</summary>
            public Builder<T> OrDefault(T fallback) { _default = fallback; return this; }

            /// <summary>Log a <b>warning</b> if nothing is found.</summary>
            public Builder<T> OrLogWarning(string message) { _warnMsg = message; return this; }

            /// <summary>Log an <b>error</b> if nothing is found.</summary>
            public Builder<T> OrLogError(string message) { _errMsg = message; return this; }

            /// <summary>Throw <see cref="InvalidOperationException"/> if nothing is found.</summary>
            public Builder<T> OrThrow(string message) { _throws = true; _throwMsg = message; return this; }

            // ── Terminal: resolve ─────────────────────────────────────────────

            /// <summary>
            /// Execute the resolution chain and return the first non-null result.
            /// Stops at the first hit (early-exit).
            /// </summary>
            public T Resolve()
            {
                foreach (var step in _steps)
                {
                    T result = step();
                    if (result != null) return result;
                }

                // Nothing found — apply fallback / logging.
                if (_default != null) return _default;
                if (_warnMsg != null) Debug.LogWarning($"[ComponentResolver] {_warnMsg} (on '{_go.name}')");
                if (_errMsg  != null) Debug.LogError  ($"[ComponentResolver] {_errMsg}  (on '{_go.name}')");
                if (_throws)          throw new InvalidOperationException(
                                          $"[ComponentResolver] {_throwMsg} (on '{_go.name}')");
                return null;
            }

            /// <summary>
            /// Execute EVERY step and return all non-null results.
            /// Does NOT stop at first hit — useful when you want all implementations
            /// (e.g. every IAimSystem in the hierarchy).
            /// </summary>
            public List<T> ResolveAll()
            {
                var results = new List<T>(_steps.Count);
                foreach (var step in _steps)
                {
                    T r = step();
                    if (r != null && !results.Contains(r))
                        results.Add(r);
                }
                return results;
            }

            // ── Private helpers ───────────────────────────────────────────────

            private static Transform FindDeepByName(Transform root, string name)
            {
                foreach (Transform child in root)
                {
                    if (child.name == name) return child;
                    var found = FindDeepByName(child, name);
                    if (found != null) return found;
                }
                return null;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Convenience extension methods — call directly on any Component / MonoBehaviour
    // ═══════════════════════════════════════════════════════════════════════════

    public static class ComponentResolverExtensions
    {
        /// <summary>
        /// Shorthand: resolve T with a sensible default search order.
        ///   1. Same GO  2. Children  3. Parent  4. Full root hierarchy
        /// Logs a warning if not found.
        /// </summary>
        public static T ResolveComponent<T>(this Component self, string missingWarning = null) where T : class
        {
            return ComponentResolver.Find<T>(self)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .OrLogWarning(missingWarning ?? $"{typeof(T).Name} not found")
                .Resolve();
        }

        /// <summary>
        /// Shorthand: resolve T checking the SerializeField value first, then the default search order.
        /// Pattern: call this inside Awake when you have an optional Inspector slot.
        ///   [SerializeField] private MonoBehaviour _source;
        ///   _system = this.ResolveWithFallback&lt;IMySystem&gt;(_source as IMySystem);
        /// </summary>
        public static T ResolveWithFallback<T>(this Component self, T existing,
            string missingWarning = null) where T : class
        {
            return ComponentResolver.Find<T>(self)
                .UseExisting(existing)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .OrLogWarning(missingWarning ?? $"{typeof(T).Name} not found")
                .Resolve();
        }

        /// <summary>
        /// Shorthand: resolve T starting from transform.root (full prefab search).
        /// Useful when component location may change during refactoring.
        /// </summary>
        public static T ResolveFromRoot<T>(this Component self,
            string missingWarning = null) where T : class
        {
            return ComponentResolver.Find<T>(self)
                .OnRoot()
                .InRootChildren()
                .OrLogWarning(missingWarning ?? $"{typeof(T).Name} not found on root hierarchy")
                .Resolve();
        }
    }
}