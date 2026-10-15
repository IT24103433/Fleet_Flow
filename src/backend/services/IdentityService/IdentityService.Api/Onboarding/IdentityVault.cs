using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IdentityService.Api.Onboarding;

public sealed class IdentityVault(IConfiguration configuration)
{
    public bool Enabled => configuration.GetValue<bool>("ONBOARDING_ENABLED");
    private byte[] Key(string name)
    {
        try { var key=Convert.FromBase64String(configuration[name]??""); if(key.Length==32)return key; }
        catch(FormatException) { }
        throw new InvalidOperationException($"Configure {name} as a private base64-encoded 32-byte key.");
    }
    public void Validate()
    {
        if(!Enabled)return;
        var data=Key("ONBOARDING_DATA_KEY");var hash=Key("ONBOARDING_HASH_KEY");
        if(CryptographicOperations.FixedTimeEquals(data,hash))throw new InvalidOperationException("Onboarding data and lookup keys must differ.");
    }
    public string Encrypt<T>(T value,string purpose)
    {
        var plain=JsonSerializer.SerializeToUtf8Bytes(value);var nonce=RandomNumberGenerator.GetBytes(12);
        var cipher=new byte[plain.Length];var tag=new byte[16];
        using var aes=new AesGcm(Key("ONBOARDING_DATA_KEY"),16);
        aes.Encrypt(nonce,plain,cipher,tag,Encoding.UTF8.GetBytes(purpose));
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }
    public T Decrypt<T>(string value,string purpose)
    {
        var data=Convert.FromBase64String(value);if(data.Length<28)throw new CryptographicException("Invalid protected record.");
        var plain=new byte[data.Length-28];using var aes=new AesGcm(Key("ONBOARDING_DATA_KEY"),16);
        aes.Decrypt(data.AsSpan(0,12),data.AsSpan(28),data.AsSpan(12,16),plain,Encoding.UTF8.GetBytes(purpose));
        return JsonSerializer.Deserialize<T>(plain)??throw new CryptographicException("Empty protected record.");
    }
    public string Fingerprint(string purpose,string value)=>Convert.ToHexString(HMACSHA256.HashData(Key("ONBOARDING_HASH_KEY"),Encoding.UTF8.GetBytes(purpose+"\n"+value)));
    public bool Matches(string actual,string expected)=>CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual),Encoding.UTF8.GetBytes(expected));
}
