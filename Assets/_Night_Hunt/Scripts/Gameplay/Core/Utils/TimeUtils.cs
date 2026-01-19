using System;
using UnityEngine;

namespace NightHunt.Gameplay.Core.Utils
{
    /// <summary>
    /// Time-related utilities
    /// </summary>
    public static class TimeUtils
    {
        /// <summary>
        /// Format time as MM:SS
        /// </summary>
        public static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes:D2}:{secs:D2}";
        }

        /// <summary>
        /// Format time as HH:MM:SS
        /// </summary>
        public static string FormatTimeLong(float seconds)
        {
            int hours = Mathf.FloorToInt(seconds / 3600f);
            int minutes = Mathf.FloorToInt((seconds % 3600f) / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);
            return $"{hours:D2}:{minutes:D2}:{secs:D2}";
        }

        /// <summary>
        /// Get elapsed time since start
        /// </summary>
        public static float ElapsedTime(float startTime)
        {
            return Time.time - startTime;
        }

        /// <summary>
        /// Get remaining time
        /// </summary>
        public static float RemainingTime(float startTime, float duration)
        {
            return Mathf.Max(0f, duration - ElapsedTime(startTime));
        }

        /// <summary>
        /// Check if time has elapsed
        /// </summary>
        public static bool HasElapsed(float startTime, float duration)
        {
            return ElapsedTime(startTime) >= duration;
        }

        /// <summary>
        /// Get time progress (0-1)
        /// </summary>
        public static float GetProgress(float startTime, float duration)
        {
            if (duration <= 0f) return 1f;
            return Mathf.Clamp01(ElapsedTime(startTime) / duration);
        }

        /// <summary>
        /// Create countdown timer
        /// </summary>
        public static string Countdown(float remainingTime)
        {
            if (remainingTime <= 0f) return "00:00";
            return FormatTime(remainingTime);
        }
    }
}

