using CodeSwitchLabel.Repositories.Persistence;

namespace CodeSwitchLabel.Tests;

public class NormalizeTests
{
    [Theory]
    [InlineData("Em nhớ upload tài liệu.", "em nhớ upload tài liệu")]
    [InlineData("EM NHỚ UPLOAD", "em nhớ upload")]
    [InlineData("  nhiều   khoảng   trắng  ", "nhiều khoảng trắng")]
    [InlineData("Có dấu chấm, phẩy; và hỏi?", "có dấu chấm phẩy và hỏi")]
    public void Normalize_ChuanHoaVeDangSoSanhDuoc(string input, string expected)
    {
        Assert.Equal(expected, ScriptTextNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_GiuNguyenDauTiengViet()
    {
        // Bỏ dấu sẽ gộp nhầm "má" với "ma" — hai từ khác nghĩa hẳn.
        Assert.NotEqual(
            ScriptTextNormalizer.Normalize("má"),
            ScriptTextNormalizer.Normalize("ma"));
    }

    [Theory]
    [InlineData("Em nhớ upload tài liệu trước deadline nhé", 8)]
    [InlineData("Một hai ba", 3)]
    [InlineData("", 0)]
    public void CountWords_DemDungSoTu(string content, int expected)
    {
        Assert.Equal(expected, ScriptTextNormalizer.CountWords(content));
    }
}

public class SnakeCaseNamingTests
{
    [Theory]
    [InlineData("ScriptVersionId", "script_version_id")]
    [InlineData("AppUser", "app_user")]
    [InlineData("QaCheck", "qa_check")]
    [InlineData("Id", "id")]
    [InlineData("EnWordCount", "en_word_count")]
    [InlineData("S3Key", "s3_key")]
    [InlineData("already_snake", "already_snake")]
    public void ToSnakeCase_DoiDungTenCot(string input, string expected)
    {
        Assert.Equal(expected, SnakeCaseNaming.ToSnakeCase(input));
    }
}

/// <summary>
/// Bộ test cho phép ƯỚC LƯỢNG số từ tiếng Anh.
///
/// Test này quan trọng hơn vẻ ngoài của nó: lược đồ chỉ có cột đếm chứ không lưu
/// vị trí từng từ, nên con số này là thứ duy nhất hệ thống biết về mức độ trộn
/// ngôn ngữ của một script. Nếu nó sai thì thống kê sai theo.
/// </summary>
public class EnglishWordCountTests
{
    [Theory]
    [InlineData("Em nhớ upload tài liệu trước deadline nhé", 2)]          // upload, deadline
    [InlineData("Chiều nay team mình có meeting với khách hàng", 2)]       // team, meeting
    [InlineData("Bạn gửi cho mình cái file Excel đó được không", 2)]       // file, Excel
    [InlineData("Mình cần review lại cái pull request này", 3)]            // review, pull, request
    [InlineData("Cái app này bị crash khi mình mở lên", 2)]                // app, crash
    [InlineData("Team mình đang chạy sprint hai tuần một lần", 2)]         // Team, sprint
    [InlineData("Nhóm mình phải nộp assignment trước thứ sáu", 1)]         // assignment
    [InlineData("Chị gửi em cái link Google Drive của dự án nhé", 3)]      // link, Google, Drive
    [InlineData("Bạn nhớ check email trước khi tan làm nha", 2)]           // check, email
    public void CountEnglishWords_DemDungTrenCacCauMau(string content, int expected)
    {
        Assert.Equal(expected, ScriptTextNormalizer.CountEnglishWords(content));
    }

    [Fact]
    public void CountEnglishWords_CauThuanViet_TraVeKhong()
    {
        Assert.Equal(0, ScriptTextNormalizer.CountEnglishWords("Hôm nay trời đẹp quá"));
    }

    /// <summary>
    /// GHI NHẬN GIỚI HẠN ĐÃ BIẾT, không phải lỗi cần sửa.
    ///
    /// Phép ước lượng dựa vào việc từ có mang dấu tiếng Việt hay không, nên mọi từ
    /// tiếng Việt viết không dấu mà chưa nằm trong danh sách loại trừ đều bị đếm nhầm
    /// thành tiếng Anh. Danh sách đó cố ý ngắn — nó chặn các ca phổ biến nhất chứ
    /// không giả vờ là từ điển đầy đủ.
    ///
    /// Muốn hết hẳn kiểu sai này thì phải lưu vị trí từng đoạn tiếng Anh, tức là
    /// thêm bảng vào lược đồ. Nhóm đã quyết hoãn việc đó.
    /// </summary>
    [Fact]
    public void CountEnglishWords_NhanNhamTuTiengVietVietKhongDau()
    {
        // "com" là cơm viết không dấu, nhưng ước lượng không phân biệt được.
        var count = ScriptTextNormalizer.CountEnglishWords("Toi an com");

        Assert.Equal(1, count);
    }

    [Fact]
    public void CountEnglishWords_KhongBaoGioVuotQuaTongSoTu()
    {
        // Ràng buộc ck_script_en_not_exceed_total dưới database dựa vào tính chất này.
        string[] samples =
        [
            "Em nhớ upload tài liệu trước deadline nhé",
            "Hôm nay trời đẹp quá",
            "Team mình đang chạy sprint hai tuần một lần",
            "abc def ghi"
        ];

        foreach (var sample in samples)
        {
            Assert.True(
                ScriptTextNormalizer.CountEnglishWords(sample) <= ScriptTextNormalizer.CountWords(sample),
                $"Số từ tiếng Anh vượt quá tổng số từ ở câu: {sample}");
        }
    }
}
