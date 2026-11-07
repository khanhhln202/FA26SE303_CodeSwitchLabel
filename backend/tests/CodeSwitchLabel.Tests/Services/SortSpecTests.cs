using CodeSwitchLabel.Repositories.Enums;
using CodeSwitchLabel.Services.Dtos;
using Xunit;

namespace CodeSwitchLabel.Tests.Services;

/// <summary>
/// đọc `?sort=` và `?dir=` thành yêu cầu sắp xếp.
///
/// Đây là hợp đồng mà MỌI endpoint danh sách đều dựa vào, nên nó được ghim ở mức unit:
/// đặc biệt là nguyên tắc "sai tên cột thì rơi về thứ tự mặc định, không phải lỗi" —
/// client cũ không gửi gì và client gõ sai phải ra cùng một kết quả.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SortSpecTests
{
    [Fact]
    public void TenCotHopLe_DocDungCotVaChieuTangDan()
    {
        var spec = SortSpec<ScriptSortField>.From("createdAt", "asc");

        Assert.Equal(ScriptSortField.CreatedAt, spec.Field);
        Assert.False(spec.Descending);
    }

    [Fact]
    public void DirDesc_LatChieu()
    {
        var spec = SortSpec<ScriptSortField>.From("createdAt", "desc");

        Assert.Equal(ScriptSortField.CreatedAt, spec.Field);
        Assert.True(spec.Descending);
    }

    [Theory]
    [InlineData("CREATEDAT", "DESC")]
    [InlineData("createdat", "Desc")]
    [InlineData("  createdAt  ", "  desc  ")]
    public void TenCotVaChieuKhongPhanBietHoaThuong(string sort, string dir)
    {
        var spec = SortSpec<ScriptSortField>.From(sort, dir);

        Assert.Equal(ScriptSortField.CreatedAt, spec.Field);
        Assert.True(spec.Descending);
    }

    [Theory]
    [InlineData("khongCoCotNay")]
    [InlineData("created_at")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TenCotSaiHoacRong_RoVeThuTuMacDinh(string? sort)
    {
        var spec = SortSpec<ScriptSortField>.From(sort, "desc");

        Assert.Equal(SortSpec<ScriptSortField>.Default, spec);
    }

    [Fact]
    public void ChiCoDirKhongCoCot_RoVeThuTuMacDinh()
    {
        // `dir` mà không có `sort` là vô nghĩa — không được đổi thứ tự mặc định.
        var spec = SortSpec<ScriptSortField>.From(null, "desc");

        Assert.Equal(SortSpec<ScriptSortField>.Default, spec);
    }

    [Theory]
    [InlineData("desc", true)]
    [InlineData("DESC", true)]
    [InlineData("asc", false)]
    [InlineData("ascending", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void DirChiLapChiềuKhiLaChuDesc(string? dir, bool expected)
    {
        var spec = SortSpec<ScriptSortField>.From("createdAt", dir);

        Assert.Equal(expected, spec.Descending);
    }

    [Fact]
    public void CotCuaBangKhac_KhongDungNhamVaBangNay()
    {
        // ReviewSortField có ReviewedAt nhưng ScriptSortField không — enum tách theo bảng
        // là để client không gửi nhầm cột của danh sách khác.
        var spec = SortSpec<ScriptSortField>.From("reviewedAt", null);

        Assert.Equal(SortSpec<ScriptSortField>.Default, spec);
    }

    [Fact]
    public void PageRequest_KhongCoSortVaDir_TraVeThuTuMacDinh()
    {
        var spec = new PageRequest().ToSort<ScriptSortField>();

        Assert.Equal(SortSpec<ScriptSortField>.Default, spec);
    }

    [Fact]
    public void PageRequest_TruyenSortVaDir_TraVeYeuCauSauKhiDoc()
    {
        var spec = new PageRequest { Sort = "status", Dir = "desc" }.ToSort<ScriptSortField>();

        Assert.Equal(ScriptSortField.Status, spec.Field);
        Assert.True(spec.Descending);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(100_000, 100_000)]
    [InlineData(100_001, 100_000)]
    [InlineData(int.MaxValue, 100_000)]
    public void PageRequest_Page_ClampedTo1ThroughMaxPage(int input, int expected)
    {
        var req = new PageRequest { Page = input };

        Assert.Equal(expected, req.Page);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-5, 20)]
    [InlineData(1, 1)]
    [InlineData(20, 20)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(int.MaxValue, 100)]
    public void PageRequest_PageSize_ClampedTo1ThroughMaxPageSize(int input, int expected)
    {
        var req = new PageRequest { PageSize = input };

        Assert.Equal(expected, req.PageSize);
    }
}
