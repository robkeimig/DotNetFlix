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

    public static void EncryptFile(string inputFilePath, string outputFilePath, byte[] encryptionKey)
    {
        var key = SHA256.HashData(encryptionKey);
        byte[] iv = new byte[InitializationVectorSize];  // AES block size (128-bit IV)

        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(iv); // Generate a new IV for encryption
        }

        using FileStream fsInput = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read);
        using FileStream fsOutput = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        fsOutput.Write(iv, 0, iv.Length);
        using CryptoStream cryptoStream = new CryptoStream(fsOutput, aes.CreateEncryptor(), CryptoStreamMode.Write);
        fsInput.CopyTo(cryptoStream);
    }

    public static void DecryptFile(string inputFilePath, string outputFilePath, byte[] encryptionKey)
    {
        var key = SHA256.HashData(encryptionKey);
        byte[] iv = new byte[InitializationVectorSize];
        using FileStream fsInput = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read);
        using FileStream fsOutput = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
        using Aes aes = Aes.Create();
        fsInput.Read(iv, 0, iv.Length);
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using CryptoStream cryptoStream = new CryptoStream(fsInput, aes.CreateDecryptor(), CryptoStreamMode.Read);
        cryptoStream.CopyTo(fsOutput);
    }
}