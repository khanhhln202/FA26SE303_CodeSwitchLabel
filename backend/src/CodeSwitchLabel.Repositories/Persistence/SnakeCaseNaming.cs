using System.Text;
using Microsoft.EntityFrameworkCore;

namespace CodeSwitchLabel.Repositories.Persistence;

/// <summary>
/// Đổi mọi tên bảng, cột, khoá và index sang snake_case.
/// PostgreSQL tự hạ chữ thường mọi định danh không đặt trong dấu nháy kép,
/// nên để EF sinh tên PascalCase sẽ dẫn tới phải nháy kép ở mọi câu SQL viết tay —
/// mà chúng ta có viết SQL tay ở truy vấn cấp phát script.
///
/// Viết tay thay vì thêm package EFCore.NamingConventions: khoảng 30 dòng,
/// không thêm phụ thuộc, và đọc là hiểu.
/// </summary>
public static class SnakeCaseNaming
{
    public static void ApplySnakeCaseNames(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName is not null)
            {
                entity.SetTableName(ToSnakeCase(tableName));
            }

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName()!));
            }

            foreach (var fk in entity.GetForeignKeys())
            {
                fk.SetConstraintName(ToSnakeCase(fk.GetConstraintName()!));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
            }
        }
    }

    /// <summary>ScriptVersionId → script_version_id · AppUser → app_user · QaCheck → qa_check</summary>
    public static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];

            if (char.IsUpper(current))
            {
                var isStart = i == 0;
                var previousIsLower = !isStart && char.IsLower(name[i - 1]);
                var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);

                // Chèn gạch dưới khi chuyển từ thường sang hoa (scriptId → script_id),
                // hoặc khi kết thúc một cụm viết hoa liền (QACheck → qa_check).
                if (!isStart && (previousIsLower || nextIsLower))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }
}
