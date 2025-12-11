using System;

namespace NightHunt.Common
{
    /// <summary>
    /// ApiResult - Match với backend ApiResponse format (lowercase fields)
    /// Backend trả về: { "success": true/false, "data": {...}, "message": "..." }
    /// </summary>
    [Serializable]
    public class ApiResult<T>
    {
        public bool success; // lowercase để match với backend
        public T data; // lowercase để match với backend
        public string message; // lowercase để match với backend
        public string errorCode; // lowercase để match với backend

        // Properties để access (uppercase cho C# convention)
        public bool Success => success;
        public T Data => data;
        public string Message => message;
        public string ErrorCode => errorCode;

        public static ApiResult<T> Ok(T data)
        {
            return new ApiResult<T>
            {
                success = true,
                data = data
            };
        }

        public static ApiResult<T> Error(string message, string errorCode = null)
        {
            return new ApiResult<T>
            {
                success = false,
                message = message,
                errorCode = errorCode
            };
        }
    }

    [Serializable]
    public class ApiResult : ApiResult<object>
    {
        public static ApiResult Ok()
        {
            return new ApiResult { success = true };
        }

        public static new ApiResult Error(string message, string errorCode = null)
        {
            return new ApiResult { success = false, message = message, errorCode = errorCode };
        }
    }
}

