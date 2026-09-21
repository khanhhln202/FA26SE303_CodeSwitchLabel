namespace CodeSwitchLabel.Services.Common;

/// <summary>
/// Băm mật khẩu bằng bcrypt. Lược đồ để password_hash là VARCHAR(60) — đúng độ dài
/// chuỗi bcrypt sinh ra, nên không dùng thuật toán khác được nếu không nới cột.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// 2^11 vòng lặp. Càng cao càng chậm cho cả người dùng lẫn kẻ dò mật khẩu;
    /// 11 là mức thư viện khuyến nghị và cũng là mức ghi trong lược đồ ($2a$11$...).
    /// </summary>
    private const int WorkFactor = 11;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Hash trong database không đúng định dạng bcrypt — ví dụ hàng mồi chưa thay hash thật.
            return false;
        }
    }
}
