using UnityEngine;

namespace NightHunt.Networking.Prediction.Input
{
    /// <summary>
    /// Server-side validator cho input data.
    /// Kiểm tra input có hợp lệ không trước khi apply trên server.
    /// </summary>
    /// <typeparam name="TInput">Type của input</typeparam>
    public class InputValidator<TInput> where TInput : struct, IInputData
    {
        private readonly float _maxInputMagnitude;
        private readonly float _validationTolerance;

        /// <summary>
        /// Khởi tạo InputValidator với validation settings.
        /// </summary>
        /// <param name="maxInputMagnitude">Magnitude tối đa cho input (default: 1.0)</param>
        /// <param name="validationTolerance">Tolerance cho validation (default: 0.1)</param>
        public InputValidator(float maxInputMagnitude = 1f, float validationTolerance = 0.1f)
        {
            _maxInputMagnitude = maxInputMagnitude;
            _validationTolerance = validationTolerance;
        }

        /// <summary>
        /// Validate input từ client.
        /// </summary>
        /// <param name="input">Input cần validate</param>
        /// <param name="lastValidInput">Input hợp lệ cuối cùng</param>
        /// <returns>Validation result</returns>
        public InputValidationResult Validate(TInput input, TInput lastValidInput)
        {
            // Kiểm tra input có thay đổi không
            if (!input.HasChanged(lastValidInput))
            {
                return new InputValidationResult
                {
                    IsValid = true,
                    Reason = "Input unchanged"
                };
            }

            // Validate input magnitude (nếu là Vector2/Vector3)
            if (TryGetMagnitude(input, out float magnitude))
            {
                if (magnitude > _maxInputMagnitude + _validationTolerance)
                {
                    return new InputValidationResult
                    {
                        IsValid = false,
                        Reason = $"Input magnitude too large: {magnitude:F2} > {_maxInputMagnitude:F2}"
                    };
                }
            }

            // Custom validation (override trong derived classes)
            var customResult = ValidateCustom(input, lastValidInput);
            if (!customResult.IsValid)
            {
                return customResult;
            }

            return new InputValidationResult
            {
                IsValid = true,
                Reason = "Input valid"
            };
        }

        /// <summary>
        /// Lấy magnitude của input (nếu là Vector2/Vector3).
        /// Override method này trong derived classes để support các input types khác.
        /// </summary>
        /// <param name="input">Input cần lấy magnitude</param>
        /// <param name="magnitude">Output magnitude</param>
        /// <returns>True nếu có thể lấy magnitude</returns>
        protected virtual bool TryGetMagnitude(TInput input, out float magnitude)
        {
            // Default: Không support magnitude
            magnitude = 0f;
            return false;
        }

        /// <summary>
        /// Custom validation logic.
        /// Override method này trong derived classes để thêm validation rules.
        /// </summary>
        /// <param name="input">Input cần validate</param>
        /// <param name="lastValidInput">Input hợp lệ cuối cùng</param>
        /// <returns>Validation result</returns>
        protected virtual InputValidationResult ValidateCustom(TInput input, TInput lastValidInput)
        {
            return new InputValidationResult
            {
                IsValid = true,
                Reason = "Custom validation passed"
            };
        }
    }

    /// <summary>
    /// Result của input validation.
    /// </summary>
    public struct InputValidationResult
    {
        /// <summary>
        /// Input có hợp lệ không.
        /// </summary>
        public bool IsValid;

        /// <summary>
        /// Lý do validation (cho debugging).
        /// </summary>
        public string Reason;
    }
}

