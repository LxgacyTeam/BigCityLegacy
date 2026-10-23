using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

internal sealed class LegacyProfileValue
{
    public string Key, Type, Value;
}
internal sealed class LegacyProfileData
{
    public int Version;
    public string Company, Product, Nickname;
    public List<LegacyProfileValue> Values;
}
internal static class LegacyProfileCodec
{
    internal const int MaxBytes = 128 * 1024 * 1024;
    private const int Iterations = 200000;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("BCLPROFILE1\n");
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    private static LegacyLocalizedText InvalidPlayerProfile = new LegacyLocalizedText("Invalid player profile.", "Некорректный профиль игрока");
    private static LegacyLocalizedText InvalidPlayerPrefs = new LegacyLocalizedText("Invalid or duplicate PlayerPrefs key.", "Некорректная PlayerPrefs запись");
    private static LegacyLocalizedText InvalidInt = new LegacyLocalizedText("Invalid integer preference.", "Некорректное int значение записи");
    private static LegacyLocalizedText InvalidFloat = new LegacyLocalizedText("Invalid float preference.", "Некорректное float значение записи");
    private static LegacyLocalizedText InvalidType = new LegacyLocalizedText("Unsupported preference type.", "Неподдерживаемый тип записи");
    private static LegacyLocalizedText Over128MB = new LegacyLocalizedText("Player profile exceeds 128 MiB.", "Размер профиля игрока превышает 128MB");
    private static LegacyLocalizedText PasswordTooLong = new LegacyLocalizedText("Password is too long.", "Слишком длинный пароль");
    private static LegacyLocalizedText InvalidProfileSize = new LegacyLocalizedText("Invalid player profile size.", "Недопустмный размер профиля игрока");
    private static LegacyLocalizedText PasswordIsNeeded = new LegacyLocalizedText("A profile password is required.", "Необходимо ввести пароль профиля");
    private static LegacyLocalizedText IncorrectPassword = new LegacyLocalizedText("Incorrect password.", "Неверный пароль");

    internal static void Validate(LegacyProfileData profile)
    {
        if (profile == null || profile.Version != 1 || string.IsNullOrEmpty(profile.Company) || string.IsNullOrEmpty(profile.Product) ||
            profile.Company.Length > 512 || profile.Product.Length > 512 || profile.Values == null || profile.Values.Count > 100000)
            throw new InvalidDataException(InvalidPlayerProfile.ToString());
        var keys = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        foreach (LegacyProfileValue item in profile.Values)
        {
            if (item == null || string.IsNullOrEmpty(item.Key) || item.Key.Length > 16383 || item.Key.IndexOf('\0') >= 0 ||
                !keys.Add(item.Key) || item.Value == null) throw new InvalidDataException(InvalidPlayerPrefs.ToString());
            int integer; float real;
            if (item.Type == "int")
            { if (!int.TryParse(item.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer)) throw new InvalidDataException(InvalidInt.ToString()); }
            else if (item.Type == "float")
            { if (!float.TryParse(item.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out real)) throw new InvalidDataException(InvalidFloat.ToString()); }
            else if (item.Type != "string") throw new InvalidDataException(InvalidType.ToString());
            total += Utf8.GetByteCount(item.Key) + (long)Utf8.GetByteCount(item.Value);
            if (total > MaxBytes) throw new InvalidDataException(Over128MB.ToString());
        }
    }
    internal static byte[] Encode(LegacyProfileData profile, string password)
    {
        Validate(profile);
        byte[] plain = Utf8.GetBytes(JsonConvert.SerializeObject(profile, Formatting.None));
        if (plain.Length > MaxBytes - 128) throw new InvalidDataException(Over128MB.ToString());
        if (string.IsNullOrEmpty(password)) return plain;
        if (password.Length > 1024) throw new InvalidDataException(PasswordTooLong.ToString());
        byte[] salt = new byte[16], iv = new byte[16];
        using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(salt); rng.GetBytes(iv); }
        byte[] keys = Derive(password, salt);
        try
        {
            byte[] cipher;
            using (var aes = Aes.Create())
            {
                aes.Key = Slice(keys, 0, 32); aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using (var encryptor = aes.CreateEncryptor()) cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
            }
            using (var output = new MemoryStream())
            {
                output.Write(Magic, 0, Magic.Length); output.Write(salt, 0, 16); output.Write(iv, 0, 16); output.Write(cipher, 0, cipher.Length);
                byte[] body = output.ToArray();
                using (var hmac = new HMACSHA256(Slice(keys, 32, 32)))
                { byte[] tag = hmac.ComputeHash(body); output.Write(tag, 0, tag.Length); }
                return output.ToArray();
            }
        }
        finally { Array.Clear(keys, 0, keys.Length); Array.Clear(plain, 0, plain.Length); }
    }
    internal static bool IsEncrypted(Stream input)
    {
        foreach (byte expected in Magic)
            if (input.ReadByte() != expected) return false;
        return true;
    }
    internal static LegacyProfileData Decode(byte[] file, string password)
    {
        if (file == null || file.Length == 0 || file.Length > MaxBytes) throw new InvalidDataException(InvalidProfileSize.ToString());
        byte[] plain = file;
        bool encrypted;
        using (var input = new MemoryStream(file, false)) encrypted = IsEncrypted(input);
        if (encrypted)
        {
            if (file.Length < Magic.Length + 16 + 16 + 16 + 32 || string.IsNullOrEmpty(password) || password.Length > 1024)
                throw new InvalidDataException(PasswordIsNeeded.ToString());
            byte[] salt = Slice(file, Magic.Length, 16), keys = Derive(password, salt);
            try
            {
                using (var hmac = new HMACSHA256(Slice(keys, 32, 32)))
                {
                    byte[] tag = hmac.ComputeHash(file, 0, file.Length - 32);
                    int different = 0;
                    for (int i = 0; i < 32; i++) different |= tag[i] ^ file[file.Length - 32 + i];
                    if (different != 0) throw new InvalidDataException(IncorrectPassword.ToString());
                }
                using (var aes = Aes.Create())
                {
                    aes.Key = Slice(keys, 0, 32); aes.IV = Slice(file, Magic.Length + 16, 16);
                    using (var decryptor = aes.CreateDecryptor()) plain = decryptor.TransformFinalBlock(file, Magic.Length + 32, file.Length - Magic.Length - 64);
                }
            }
            finally { Array.Clear(keys, 0, keys.Length); }
        }
        try
        {
            var profile = JsonConvert.DeserializeObject<LegacyProfileData>(Utf8.GetString(plain), new JsonSerializerSettings {
                MaxDepth = 8, TypeNameHandling = TypeNameHandling.None, CheckAdditionalContent = true });
            Validate(profile);
            return profile;
        }
        finally { if (encrypted) Array.Clear(plain, 0, plain.Length); }
    }
    private static byte[] Derive(string password, byte[] salt)
    {
        // This overload works on the game's older Mono as well as desktop .NET.
        using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations)) return kdf.GetBytes(64);
    }
    private static byte[] Slice(byte[] input, int start, int length)
    { var output = new byte[length]; Buffer.BlockCopy(input, start, output, 0, length); return output; }
}
