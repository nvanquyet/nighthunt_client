using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Gameplay.Core.Config
{
    /// <summary>
    /// Validates config data
    /// </summary>
    public static class ConfigValidator
    {
        /// <summary>
        /// Validation result
        /// </summary>
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
            public List<string> Warnings { get; set; } = new List<string>();

            public void AddError(string error)
            {
                Errors.Add(error);
                IsValid = false;
            }

            public void AddWarning(string warning)
            {
                Warnings.Add(warning);
            }
        }

        /// <summary>
        /// Validate config is not null
        /// </summary>
        public static ValidationResult ValidateNotNull<T>(T config, string configName)
        {
            var result = new ValidationResult { IsValid = true };

            if (config == null)
            {
                result.AddError($"{configName} is null");
            }

            return result;
        }

        /// <summary>
        /// Validate string is not empty
        /// </summary>
        public static ValidationResult ValidateString(string value, string fieldName, bool allowEmpty = false)
        {
            var result = new ValidationResult { IsValid = true };

            if (string.IsNullOrEmpty(value) && !allowEmpty)
            {
                result.AddError($"{fieldName} is null or empty");
            }

            return result;
        }

        /// <summary>
        /// Validate value is in range
        /// </summary>
        public static ValidationResult ValidateRange(float value, float min, float max, string fieldName)
        {
            var result = new ValidationResult { IsValid = true };

            if (value < min || value > max)
            {
                result.AddError($"{fieldName} ({value}) is out of range [{min}, {max}]");
            }

            return result;
        }

        /// <summary>
        /// Validate value is positive
        /// </summary>
        public static ValidationResult ValidatePositive(float value, string fieldName)
        {
            return ValidateRange(value, 0f, float.MaxValue, fieldName);
        }

        /// <summary>
        /// Combine multiple validation results
        /// </summary>
        public static ValidationResult Combine(params ValidationResult[] results)
        {
            var combined = new ValidationResult { IsValid = true };

            foreach (var result in results)
            {
                if (!result.IsValid)
                {
                    combined.IsValid = false;
                }

                combined.Errors.AddRange(result.Errors);
                combined.Warnings.AddRange(result.Warnings);
            }

            return combined;
        }
    }
}

