namespace NightHunt.Networking.Prediction.Input
{
    /// <summary>
    /// Interface cho input data trong prediction system.
    /// Tất cả input types phải implement interface này để có thể serialize và gửi qua network.
    /// </summary>
    public interface IInputData
    {
        /// <summary>
        /// Kiểm tra xem input có thay đổi so với input trước đó không.
        /// Dùng để optimize network bandwidth (chỉ gửi khi có thay đổi).
        /// </summary>
        /// <param name="other">Input khác để so sánh</param>
        /// <returns>True nếu input khác nhau</returns>
        bool HasChanged(IInputData other);

        /// <summary>
        /// Reset input về giá trị mặc định.
        /// </summary>
        void Reset();
    }
}

