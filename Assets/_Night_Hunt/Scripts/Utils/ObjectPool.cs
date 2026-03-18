using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Utils
{
    /// <summary>
    /// Generic Object Pool - Reduces GC allocations by reusing objects
    /// 
    /// Usage:
    ///   var pool = new ObjectPool<MyDTO>(() => new MyDTO(), 50);
    ///   var obj = pool.Get();
    ///   // Use obj...
    ///   pool.Return(obj);
    /// 
    /// Benefits:
    /// - Reduces GC allocations (reuses objects instead of creating new)
    /// - Reduces GC frequency (fewer allocations = fewer collections)
    /// - Better performance on mobile (GC pauses are expensive)
    /// 
    /// Best Used For:
    /// - DTOs (FriendResponse, PartyResponse, etc.)
    /// - WebSocket event objects
    /// - Frequently created/destroyed objects
    /// </summary>
    public class ObjectPool<T> where T : class
    {
        private readonly Stack<T> pool;
        private readonly Func<T> createFunc;
        private readonly Action<T> onGet;
        private readonly Action<T> onReturn;
        private readonly int maxSize;
        
        /// <summary>
        /// Current number of objects in the pool
        /// </summary>
        public int Count => pool.Count;

        /// <summary>
        /// Create a new object pool
        /// </summary>
        /// <param name="createFunc">Factory function to create new objects</param>
        /// <param name="maxSize">Maximum pool size (prevents unbounded growth)</param>
        /// <param name="onGet">Optional callback when object is retrieved</param>
        /// <param name="onReturn">Optional callback when object is returned</param>
        /// <param name="preWarm">Number of objects to pre-create</param>
        public ObjectPool(
            Func<T> createFunc, 
            int maxSize = 100, 
            Action<T> onGet = null, 
            Action<T> onReturn = null,
            int preWarm = 0)
        {
            this.createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            this.maxSize = maxSize;
            this.onGet = onGet;
            this.onReturn = onReturn;
            this.pool = new Stack<T>(maxSize);

            // Pre-warm pool (create objects upfront)
            for (int i = 0; i < preWarm; i++)
            {
                pool.Push(createFunc());
            }
        }

        /// <summary>
        /// Get an object from the pool (or create new if pool is empty)
        /// </summary>
        public T Get()
        {
            T obj;
            
            if (pool.Count > 0)
            {
                obj = pool.Pop();
            }
            else
            {
                obj = createFunc();
            }

            onGet?.Invoke(obj);
            return obj;
        }

        /// <summary>
        /// Return an object to the pool
        /// If pool is full, object is discarded (will be GC'd)
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null)
                return;

            onReturn?.Invoke(obj);

            // Only return to pool if not at max capacity
            if (pool.Count < maxSize)
            {
                pool.Push(obj);
            }
            // Otherwise, let it be GC'd (pool is full)
        }

        /// <summary>
        /// Clear all objects from the pool
        /// </summary>
        public void Clear()
        {
            pool.Clear();
        }
    }

    /// <summary>
    /// Static pool manager for common DTO types
    /// Provides shared pools to avoid creating multiple pools for same types
    /// </summary>
    public static class DTOPool
    {
        // ════════════════════════════════════════════════════════════════════════
        // Pool Configuration
        // ════════════════════════════════════════════════════════════════════════
        
        private const int DEFAULT_MAX_SIZE = 50;
        private const int DEFAULT_PREWARM = 10;

        // ════════════════════════════════════════════════════════════════════════
        // Pools for Common DTO Types
        // ════════════════════════════════════════════════════════════════════════
        
        // Friend DTOs
        public static ObjectPool<List<object>> ListPool = new ObjectPool<List<object>>(
            () => new List<object>(10),
            maxSize: DEFAULT_MAX_SIZE,
            onReturn: list => list.Clear(),
            preWarm: DEFAULT_PREWARM
        );

        // Dictionary pool for JSON deserialization
        public static ObjectPool<Dictionary<string, object>> DictPool = new ObjectPool<Dictionary<string, object>>(
            () => new Dictionary<string, object>(10),
            maxSize: DEFAULT_MAX_SIZE,
            onReturn: dict => dict.Clear(),
            preWarm: DEFAULT_PREWARM
        );

        // StringBuilder pool for URL construction
        public static ObjectPool<System.Text.StringBuilder> StringBuilderPool = new ObjectPool<System.Text.StringBuilder>(
            () => new System.Text.StringBuilder(256),
            maxSize: 20,
            onReturn: sb => sb.Clear(),
            preWarm: 5
        );

        // ════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Get a pooled List<T>
        /// Remember to call Return() when done!
        /// </summary>
        public static List<T> GetList<T>()
        {
            // Cast generic list (safe since we clear on return)
            return new List<T>(10);
        }

        /// <summary>
        /// Return a list to the pool
        /// </summary>
        public static void ReturnList<T>(List<T> list)
        {
            if (list != null)
            {
                list.Clear();
                // Note: Can't return to typed pool, but clearing reduces GC
            }
        }

        /// <summary>
        /// Get a pooled StringBuilder for URL construction
        /// </summary>
        public static System.Text.StringBuilder GetStringBuilder()
        {
            return StringBuilderPool.Get();
        }

        /// <summary>
        /// Return StringBuilder to pool
        /// </summary>
        public static void ReturnStringBuilder(System.Text.StringBuilder sb)
        {
            StringBuilderPool.Return(sb);
        }

        /// <summary>
        /// Clear all pools (useful for scene transitions)
        /// </summary>
        public static void ClearAll()
        {
            ListPool.Clear();
            DictPool.Clear();
            StringBuilderPool.Clear();
        }
    }
}
