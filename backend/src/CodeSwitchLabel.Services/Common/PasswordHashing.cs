using System.Security.Cryptography;

namespace CodeSwitchLabel.Services.Common;

/// <summary>
/// Sinh mật khẩu tạm khi Admin tạo tài khoản hoặc cấp lại mật khẩu.
/// Dùng bộ sinh số ngẫu nhiên mật mã học (RandomNumberGenerator), không dùng Random —
/// Random đoán trước được nếu biết thời điểm sinh.
/// </summary>
public static class TemporaryPassword
{
    /// <summary>Bỏ các ký tự dễ đọc nhầm khi chép tay hoặc đọc qua điện thoại: 0/O, 1/l/I.</summary>
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

    /// <summary>12 ký tự từ bảng 56 ký tự — khoảng 70 bit ngẫu nhiên.</summary>
    public const int Length = 12;

    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, Length);
}

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
