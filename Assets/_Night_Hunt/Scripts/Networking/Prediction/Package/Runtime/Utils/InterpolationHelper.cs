using UnityEngine;

namespace NightHunt.Networking.Prediction.Utils
{
    public static class InterpolationHelper
    {
        public static float CalculateInterpolationFactor(float startTime, float endTime, float currentTime)
        {
            if (endTime <= startTime)
                return 1f;

            return Mathf.Clamp01((currentTime - startTime) / (endTime - startTime));
        }

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, t);
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) => Quaternion.Slerp(a, b, t);
        public static Vector3 Extrapolate(Vector3 start, Vector3 velocity, float deltaTime) => start + velocity * deltaTime;
    }
}

