using System.Security.Cryptography;
using System.Text;

namespace ControlInventarioMovil.Helpers
{
    public static class CifradoHelper
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("AppInventarioM0vil$Key2026Secure");
        private static readonly byte[] IV = Encoding.UTF8.GetBytes("Vect0rSeguro2026");

        public static string Encriptar(string textoPlano)
        {
            if (string.IsNullOrEmpty(textoPlano)) return string.Empty;
            using Aes aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;
            using MemoryStream ms = new();
            using CryptoStream cs = new(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            byte[] bytes = Encoding.UTF8.GetBytes(textoPlano);
            cs.Write(bytes, 0, bytes.Length);
            cs.FlushFinalBlock();
            return Convert.ToBase64String(ms.ToArray());
        }

        public static string Desencriptar(string textoCifrado)
        {
            if (string.IsNullOrEmpty(textoCifrado)) return string.Empty;
            try
            {
                using Aes aes = Aes.Create();
                aes.Key = Key;
                aes.IV = IV;
                using MemoryStream ms = new(Convert.FromBase64String(textoCifrado));
                using CryptoStream cs = new(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using StreamReader reader = new(cs);
                return reader.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}