using System.Security.Cryptography;
using System.Text;
using Dapper;
using DotNetFlix.Data;
using Microsoft.Data.Sqlite;

namespace DotNetFlix;

internal class Cryptography
{
    static byte[] Salt = Encoding.UTF8.GetBytes("!!!!!!!!!!!!!");
    const int KeyLengthBytes = 64;
    const int Iterations = 1_000_000;

    public static byte[] GetBytes(string password)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, Salt, Iterations, HashAlgorithmName.SHA512);
        return pbkdf2.GetBytes(KeyLengthBytes);
    }

    public static byte[] GetBytes()
    {
        byte[] bytes = new byte[KeyLengthBytes];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes); 
        }
        return bytes;
    }

    public static void EncryptFile(string inputFilePath, string outputFilePath, byte[] encryptionKey)
    {
        byte[] key = new byte[32]; // 256-bit AES key
        byte[] iv = new byte[16];  // AES block size (128-bit IV)

        using (SHA256 sha256 = SHA256.Create())
        {
            key = sha256.ComputeHash(encryptionKey);
        }

        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(iv); // Generate a new IV for encryption
        }

        using (FileStream fsInput = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read))
        using (FileStream fsOutput = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Write the IV to the beginning of the file (needed for decryption)
            fsOutput.Write(iv, 0, iv.Length);

            using (CryptoStream cryptoStream = new CryptoStream(fsOutput, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                fsInput.CopyTo(cryptoStream);
            }
        }
    }

    public static void DecryptFile(string inputFilePath, string outputFilePath, byte[] encryptionKey)
    {
        byte[] key = new byte[32]; // 256-bit AES key
        byte[] iv = new byte[16];  // AES block size (128-bit IV)

        using (SHA256 sha256 = SHA256.Create())
        {
            key = sha256.ComputeHash(encryptionKey);
        }

        using (FileStream fsInput = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read))
        using (FileStream fsOutput = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
        using (Aes aes = Aes.Create())
        {
            // Read the IV from the beginning of the encrypted file
            fsInput.Read(iv, 0, iv.Length);

            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (CryptoStream cryptoStream = new CryptoStream(fsInput, aes.CreateDecryptor(), CryptoStreamMode.Read))
            {
                cryptoStream.CopyTo(fsOutput);
            }
        }
    }
}

public static class CryptographyExtensions
{ 
    public static void InitializeCryptography(this SqliteConnection sql, string systemPassword)
    {
        var masterEncryptionKey = Cryptography.GetBytes(systemPassword);

        sql.Execute($@"UPDATE {SettingsTable.TableName}
            SET     {nameof(SettingsTable.Value)} = @{nameof(SettingsTable.Value)}
            WHERE   {nameof(SettingsTable.Key)} = @{nameof(SettingsTable.Key)}", new
        {
            Key = nameof(Configuration.MasterEncryptionKey),
            Value = Convert.ToBase64String(masterEncryptionKey)
        });
    }
}

