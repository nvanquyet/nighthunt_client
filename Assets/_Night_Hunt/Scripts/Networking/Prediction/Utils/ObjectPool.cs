using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Networking.Prediction.Utils
{
    /// <summary>
    /// Generic object pool cho zero-allocation prediction system.
    /// Dùng để pool state snapshots và các objects khác.
    /// </summary>
    /// <typeparam name="T">Type của object cần pool (phải là class với parameterless constructor)</typeparam>
    public class ObjectPool<T> where T : class, new()
    {
        private readonly Stack<T> _pool;
        private readonly int _maxSize;
        private int _currentSize;

        /// <summary>
        /// Số lượng objects hiện có trong pool.
        /// </summary>
        public int Count => _pool.Count;

        /// <summary>
        /// Khởi tạo ObjectPool với max size.
        /// </summary>
        /// <param name="maxSize">Số lượng objects tối đa trong pool (default: 100)</param>
        public ObjectPool(int maxSize = 100)
        {
            _maxSize = maxSize;
            _pool = new Stack<T>(maxSize);
            _currentSize = 0;
        }

        /// <summary>
        /// Lấy object từ pool hoặc tạo mới nếu pool rỗng.
        /// </summary>
        /// <returns>Object từ pool hoặc object mới</returns>
        public T Get()
        {
            if (_pool.Count > 0)
            {
                return _pool.Pop();
            }

            // Pool rỗng, tạo object mới
            _currentSize++;
            return new T();
        }

        /// <summary>
        /// Trả object về pool để tái sử dụng.
        /// </summary>
        /// <param name="obj">Object cần trả về pool</param>
        public void Return(T obj)
        {
            if (obj == null)
                return;

            // Nếu pool đã đầy, không thêm vào
            if (_pool.Count >= _maxSize)
            {
                return;
            }

            // Reset object (nếu có Reset method)
            if (obj is IPoolable poolable)
            {
                poolable.Reset();
            }

            _pool.Push(obj);
        }

        /// <summary>
        /// Xóa tất cả objects trong pool.
        /// </summary>
        public void Clear()
        {
            _pool.Clear();
            _currentSize = 0;
        }

        /// <summary>
        /// Pre-allocate một số objects vào pool.
        /// </summary>
        /// <param name="count">Số lượng objects cần pre-allocate</param>
        public void PreAllocate(int count)
        {
            count = Mathf.Min(count, _maxSize - _currentSize);
            for (int i = 0; i < count; i++)
            {
                var obj = new T();
                _pool.Push(obj);
                _currentSize++;
            }
        }
    }

    /// <summary>
    /// Interface cho objects có thể được reset khi trả về pool.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Reset object về trạng thái ban đầu.
        /// </summary>
        void Reset();
    }
}

