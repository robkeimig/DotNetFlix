using System.Security.Cryptography;

namespace DotNetFlix;

internal class Cryptography
{
    const int KeyLength = 32;
    const int InitializationVectorSize = 16;

    public static string GenerateTokenString() =>
       Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeyLength))
       .Replace("/", "")
       .Replace("=", "")
       .Replace("+", "");

    public static byte[] GenerateEncryptionKey()
    {
        byte[] bytes = new byte[KeyLength];
        
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes); 
        }

        return bytes;
    }

    public static void EncryptStream(Stream inputStream, Stream outputStream, byte[] encryptionKey)
    {
        var key = SHA256.HashData(encryptionKey);
        byte[] iv = new byte[InitializationVectorSize]; 

        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(iv);
        }

        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        outputStream.Write(iv, 0, iv.Length);
        using CryptoStream cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write, true);
        inputStream.CopyTo(cryptoStream);  
    }

    public static void DecryptStream(Stream inputStream, Stream outputStream, byte[] encryptionKey)
    {
        var key = SHA256.HashData(encryptionKey);
        byte[] iv = new byte[InitializationVectorSize];
        using Aes aes = Aes.Create();
        inputStream.Read(iv, 0, iv.Length);
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using CryptoStream cryptoStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read, true);
        cryptoStream.CopyTo(outputStream);  
    }
}