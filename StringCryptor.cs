using System.Security.Cryptography;
using System.Text;

namespace KrakenMobile;

internal static class StringCryptor
{
    private static readonly byte[] _MasterKey = new byte[] {
        0x4B, 0x72, 0x61, 0x6B, 0x65, 0x6E, 0x58, 0x62,
        0x6F, 0x78, 0x55, 0x6E, 0x6C, 0x6F, 0x63, 0x6B,
        0x65, 0x72, 0x5F, 0x41, 0x45, 0x53, 0x32, 0x35,
        0x36, 0x5F, 0x4D, 0x61, 0x73, 0x74, 0x65, 0x72
    };

    private static byte[] DeriveKey(byte[] salt)
    {
        using var derive = new Rfc2898DeriveBytes(_MasterKey, salt, 1000, HashAlgorithmName.SHA256);
        return derive.GetBytes(32);
    }

    private static byte[] DeriveIV(byte[] salt)
    {
        using var derive = new Rfc2898DeriveBytes(_MasterKey, salt, 1000, HashAlgorithmName.SHA256);
        return derive.GetBytes(16);
    }

    public static string Decode(byte[] data)
    {
        if (data.Length < 16) return FallbackDecode(data);

        try
        {
            var iv = data[..16];
            var ciphertext = data[16..];
            var key = DeriveKey(iv);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            return Encoding.UTF8.GetString(plaintext).TrimEnd('\0');
        }
        catch
        {
            return FallbackDecode(data);
        }
    }

    private static string FallbackDecode(byte[] data)
    {
        var buf = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
            buf[i] = (byte)(data[i] ^ ((i + 0x5A) & 0xFF));
        return Encoding.UTF8.GetString(buf).TrimEnd('\0');
    }
}
