using System.Security.Cryptography;

namespace Quiz_Application_College.Services.Security
{
    public static class StudentPasswordHasher
    {
        // Hash roll number with per-user salt; return base64 hash
        public static string Hash(string password, byte[] salt, int iterations = 10000, int size = 32)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            return Convert.ToBase64String(pbkdf2.GetBytes(size));
        }

        public static byte[] NewSalt(int size = 16)
        {
            var salt = new byte[size];
            RandomNumberGenerator.Fill(salt);
            return salt;
        }

        public static bool Verify(string password, string base64Salt, string base64Hash)
        {
            var salt = Convert.FromBase64String(base64Salt);
            var hash = Hash(password, salt);
            return ConstantTimeEquals(hash, base64Hash);
        }

        private static bool ConstantTimeEquals(string a, string b)
        {
            var ba = System.Text.Encoding.UTF8.GetBytes(a);
            var bb = System.Text.Encoding.UTF8.GetBytes(b);
            if (ba.Length != bb.Length) return false;
            int diff = 0;
            for (int i = 0; i < ba.Length; i++) diff |= ba[i] ^ bb[i];
            return diff == 0;
        }
    }
}
