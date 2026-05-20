using System;
using System.Collections.Generic;

namespace NightHunt.UI
{
    /// <summary>
    /// Small runtime registry for mutually-exclusive UI context menus.
    /// Opening one menu closes the others, independent of which feature owns it.
    /// </summary>
    public static class UIContextMenuRegistry
    {
        private sealed class Entry
        {
            public object Owner;
            public Action Hide;
        }

        private static readonly List<Entry> Entries = new();

        public static void Register(object owner, Action hide)
        {
            if (owner == null || hide == null)
                return;

            Unregister(owner);
            Entries.Add(new Entry { Owner = owner, Hide = hide });
        }

        public static void Unregister(object owner)
        {
            if (owner == null)
                return;

            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(Entries[i].Owner, owner))
                    Entries.RemoveAt(i);
            }
        }

        public static void CloseAllExcept(object owner)
        {
            var snapshot = Entries.ToArray();
            foreach (var entry in snapshot)
            {
                if (entry == null || entry.Hide == null)
                    continue;
                if (owner != null && ReferenceEquals(entry.Owner, owner))
                    continue;

                entry.Hide.Invoke();
            }
        }

        public static void CloseAll() => CloseAllExcept(null);
    }
}
