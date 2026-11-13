using CodeSwitchLabel.Services.Common;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// Các hàm tính toán chủ đề (mã, khóa, chuẩn hóa tên) là thuần tuý — kiểm thử
/// đơn lẻ không cần DbContext.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DomainCodeGeneratorTests
{
    [Fact]
    public void CleanName_TenHopLe_TraVeGiongNguyen()
    {
        Assert.Equal("Y tế", DomainCodeGenerator.CleanName("Y tế"));
        Assert.Equal("Công nghệ", DomainCodeGenerator.CleanName("  Công nghệ  "));
    }

    [Fact]
    public void CleanName_TrongHoacKhongGhiChu_NgoaiLeInvalidDomainName()
    {
        foreach (var name in new[] { null!, "", "   ", "\t" })
        {
            var ex = Assert.Throws<UnprocessableException>(() => DomainCodeGenerator.CleanName(name));
            Assert.Equal("invalid_domain_name", ex.Code);
            Assert.Equal("Tên chủ đề không được để trống.", ex.Message);
        }
    }

    [Fact]
    public void CleanName_QuaNganHoacQuaDai_NgoaiLeInvalidDomainName()
    {
        var ex1 = Assert.Throws<UnprocessableException>(() => DomainCodeGenerator.CleanName("a"));
        Assert.Equal("invalid_domain_name", ex1.Code);
        Assert.Equal("Tên chủ đề phải từ 2 đến 100 ký tự.", ex1.Message);

        var ex2 = Assert.Throws<UnprocessableException>(() => DomainCodeGenerator.CleanName(new string('a', 101)));
        Assert.Equal("invalid_domain_name", ex2.Code);
    }

    [Fact]
    public void MakeCode_TiengViet_LoaBoDauInHoaDauTu()
    {
        Assert.Equal("YTe", DomainCodeGenerator.MakeCode("Y tế"));
        Assert.Equal("CongNgheThongTin", DomainCodeGenerator.MakeCode("Công nghệ thông tin"));
    }

    [Fact]
    public void MakeCode_KyTuDacBiet_LoaBoKyTuKhongChuVaSo()
    {
        // C++ và C# cùng rụt xuống "C" -> tiềm năng va chạm, UniqueCode sẽ xử lý.
        Assert.Equal("C", DomainCodeGenerator.MakeCode("C++"));
        Assert.Equal("C", DomainCodeGenerator.MakeCode("C#"));
        Assert.Equal("ItTechnology", DomainCodeGenerator.MakeCode("IT/Technology"));
        Assert.Equal("ItTechnology", DomainCodeGenerator.MakeCode("IT Technology"));
    }

    [Fact]
    public void MakeCode_ChuoiToanKyTuKhongTraVeDomain()
    {
        Assert.Equal("Domain", DomainCodeGenerator.MakeCode(""));
        Assert.Equal("Domain", DomainCodeGenerator.MakeCode("///"));
        Assert.Equal("Domain", DomainCodeGenerator.MakeCode("   "));
    }

    [Fact]
    public void MatchKey_TenVaCodeKhacNhauNhungKhoaCungNhau()
    {
        Assert.Equal(
            DomainCodeGenerator.MatchKey("IT/Technology"),
            DomainCodeGenerator.MatchKey("IT Technology"));
        Assert.Equal("yte", DomainCodeGenerator.MatchKey("Y tế"));
        Assert.Equal("doithuong", DomainCodeGenerator.MatchKey("Đời thường!"));
    }

    [Fact]
    public void UniqueCode_MaChuaTonTai_TraVeLaiMaGoc()
    {
        Assert.Equal("Foo", DomainCodeGenerator.UniqueCode("Foo", []));
        Assert.Equal("Foo", DomainCodeGenerator.UniqueCode("Foo", ["Bar", "Baz"]));
    }

    [Fact]
    public void UniqueCode_MaDaTonTai_ThemSoHau()
    {
        Assert.Equal("Foo2", DomainCodeGenerator.UniqueCode("Foo", ["Foo"]));
        Assert.Equal("Foo2", DomainCodeGenerator.UniqueCode("Foo", ["foo"])); // không phân biệt hoa thường
        Assert.Equal("Foo3", DomainCodeGenerator.UniqueCode("Foo", ["Foo", "Foo2"]));
        Assert.Equal("ItTechnology2", DomainCodeGenerator.UniqueCode("ItTechnology", ["ItTechnology"]));
    }

    [Fact]
    public void Unaccent_BoDauTiengViet()
    {
        Assert.Equal("d", DomainCodeGenerator.Unaccent("đ"));
        Assert.Equal("d", DomainCodeGenerator.Unaccent("Đ")); // Unaccent viết thường toàn bộ
        Assert.Equal("y te", DomainCodeGenerator.Unaccent("Y tế"));
    }
}
