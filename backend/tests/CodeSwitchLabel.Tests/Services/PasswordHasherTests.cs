using CodeSwitchLabel.Services.Common;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>Bcrypt: băm đúng work factor, verify đúng/sai, hash lạ không ném mà trả false.</summary>
[Trait("Category", "Unit")]
public sealed class PasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_RaChuoiBcrypt60KyTu()
    {
        // Arrange + Act
        var hash = _hasher.Hash("MatKhau2026");

        // Assert — lược đồ để password_hash VARCHAR(60), sai độ dài là migration hỏng.
        Assert.Equal(60, hash.Length);
        Assert.StartsWith("$2", hash);
    }

    [Fact]
    public void Verify_DungMatKhau_TraTrue()
    {
        // Arrange
        var hash = _hasher.Hash("MatKhau2026");

        // Act
        var ok = _hasher.Verify("MatKhau2026", hash);

        // Assert
        Assert.True(ok);
    }

    [Fact]
    public void Verify_SaiMatKhau_TraFalse()
    {
        // Arrange
        var hash = _hasher.Hash("MatKhau2026");

        // Act
        var ok = _hasher.Verify("SaiMatKhau", hash);

        // Assert
        Assert.False(ok);
    }

    [Fact]
    public void Verify_HashSaiDinhDang_TraFalseChuKhongNem()
    {
        // Arrange + Act — hàng mồi chưa thay hash thật vẫn phải trả false êm.
        var ok = _hasher.Verify("MatKhau2026", "khong-phai-bcrypt");

        // Assert
        Assert.False(ok);
    }

    [Fact]
    public void Verify_HashThatBiSuaMotKyTu_TraFalse()
    {
        // Arrange — giữ nguyên định dạng bcrypt, chỉ đổi một ký tự cuối: so sánh phải trượt êm.
        var hash = _hasher.Hash("MatKhau2026");
        var corrupted = hash[..^1] + (hash[^1] == 'a' ? 'b' : 'a');

        // Act
        var ok = _hasher.Verify("MatKhau2026", corrupted);

        // Assert
        Assert.False(ok);
    }

    [Theory]
    [InlineData("")]
    [InlineData("$2a$11$ngan")]
    [InlineData("$2a$11$REPLACE_WITH_REAL_BCRYPT_HASH_BEFORE_FIRST_LOGIN")]
    public void Verify_HashCucNganHoacSaiDinhDang_TraFalse(string hash)
    {
        // chuỗi quá ngắn hoặc sai định dạng bcrypt đều được xử lý êm, trả false
        var ok = _hasher.Verify("MatKhau2026", hash);
        Assert.False(ok);
    }
}
