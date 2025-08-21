using System.Security.Cryptography;
using System.Text;

namespace BuggyNotes.Api.Crypto
{
    public static class CryptoService
    {
        public static AesEncryptResponse AesGcmEncrypt(string plaintext, string? base64Key = null)
        {
            var key = base64Key is { Length: > 0 } ? Convert.FromBase64String(base64Key) : RandomNumberGenerator.GetBytes(32);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var pt = Encoding.UTF8.GetBytes(plaintext);
            var ct = new byte[pt.Length];
            var tag = new byte[16];

            using var aesgcm = new AesGcm(key, 16);
            aesgcm.Encrypt(nonce, pt, ct, tag);

            return new(
                Base64Key: Convert.ToBase64String(key),
                Base64Nonce: Convert.ToBase64String(nonce),
                Base64Ciphertext: Convert.ToBase64String(ct),
                Base64Tag: Convert.ToBase64String(tag)
            );
        }



        public static AesDecryptResponse AesGcmDecrypt(string base64Key, string base64Nonce, string base64Ciphertext, string base64Tag)
        {
            var key = Convert.FromBase64String(base64Key);
            var nonce = Convert.FromBase64String(base64Nonce);
            var ct = Convert.FromBase64String(base64Ciphertext);
            var tag = Convert.FromBase64String(base64Tag);
            var pt = new byte[ct.Length];

            using var aesgcm = new AesGcm(key, 16);
            aesgcm.Decrypt(nonce, ct, tag, pt);


            return new(Encoding.UTF8.GetString(pt));
        }

        //Unsafe CBC encryption for demo purposes only
        public static AesEncryptResponse AesCbcInsecureEncrypt(string plaintext, string? base64Key = null)
        {
            var key = base64Key is { Length: > 0 } ? Convert.FromBase64String(base64Key) : RandomNumberGenerator.GetBytes(32);
            var iv = new byte[16];
            var pt = Encoding.UTF8.GetBytes(plaintext);

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var enc = aes.CreateEncryptor(key, iv);
            var ct = enc.TransformFinalBlock(pt, 0, pt.Length);


            return new(
                Base64Key: Convert.ToBase64String(key),
                Base64Nonce: Convert.ToBase64String(iv),
                Base64Ciphertext: Convert.ToBase64String(ct),
                Base64Tag: ""
            );
        }

        public static HashResponse HashPasswordPbkdf2(string password, int iterations)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var bytes = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
            sw.Stop();

            var packed = Convert.ToBase64String(salt.Concat(bytes).ToArray());
            return new("PBKDF2-SHA256", packed, iterations, sw.ElapsedMilliseconds);
        }

        public static VerifyResponse VerifyPasswordPbkdf2(string password, string packedBase64, int iterations)
        {
            var data = Convert.FromBase64String(packedBase64);
            var salt = data[..16];
            var hash = data[16..];
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var test = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, hash.Length);
            sw.Stop();
            var ok = CryptographicOperations.FixedTimeEquals(test, hash);
            return new(ok, sw.ElapsedMilliseconds);
        }

        public static HashResponse HashPasswordSha256(string password)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            sw.Stop();
            return new("SHA-256 (INSECURE for passwords)", Convert.ToBase64String(bytes), 1, sw.ElapsedMilliseconds);
        }


    }
}
