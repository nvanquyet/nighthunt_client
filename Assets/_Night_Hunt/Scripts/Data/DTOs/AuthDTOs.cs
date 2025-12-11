using System;

namespace NightHunt.Data.DTOs
{
    [Serializable]
    public class RegisterRequest
    {
        public string username;
        public string email;
        public string password;
        public string confirmPassword;
    }

    [Serializable]
    public class LoginRequest
    {
        public string identifier; // username or email
        public string password;
    }

    [Serializable]
    public class AutoLoginRequest
    {
        public string accessToken;
        public string sessionId;
    }

    [Serializable]
    public class ChangePasswordRequest
    {
        public string oldPassword;
        public string newPassword;
        public string confirmNewPassword;
    }

    [Serializable]
    public class AuthResponse
    {
        public string accessToken;
        public string sessionId;
        public long userId;
        public string username;
        public string email;
    }
}

