using System;

namespace NightHunt.Networking.Prediction
{
    /// <summary>
    /// Lightweight, type-safe wrapper quanh uint để dùng làm requestId cho prediction.
    /// Giúp tránh nhầm lẫn với các uint khác và gom logic tạo ID về một chỗ.
    /// </summary>
    [Serializable]
    public struct PredictionRequestId : IEquatable<PredictionRequestId>
    {
        public uint Value;

        public PredictionRequestId(uint value)
        {
            Value = value;
        }

        public static readonly PredictionRequestId Invalid = new PredictionRequestId(0);

        public bool IsValid => Value != 0;

        public bool Equals(PredictionRequestId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PredictionRequestId other && Equals(other);
        public override int GetHashCode() => (int)Value;

        public static bool operator ==(PredictionRequestId left, PredictionRequestId right) => left.Value == right.Value;
        public static bool operator !=(PredictionRequestId left, PredictionRequestId right) => left.Value != right.Value;

        public override string ToString() => Value.ToString();
    } 
}


