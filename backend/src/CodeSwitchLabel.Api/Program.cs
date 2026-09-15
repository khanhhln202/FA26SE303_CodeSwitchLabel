using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Options;
using CodeSwitchLabel.Services.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- cấu hình

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "Thiếu ConnectionStrings:Postgres. Xem appsettings.Development.json.");

// Mọi mục cấu hình đều kiểm ngay lúc khởi động thay vì đợi request đầu tiên mới nổ:
// thiếu khoá ký hay thiếu địa chỉ kho lưu trữ thì phải chết lúc chạy `dotnet run`,
// không phải lúc có người đăng nhập hay nộp bản ghi.
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<ObjectStorageOptions>()
    .Bind(builder.Configuration.GetSection(ObjectStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<AudioOptions>()
    .Bind(builder.Configuration.GetSection(AudioOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Thiếu cấu hình mục Jwt.");

// ---------------------------------------------------------------- dịch vụ

builder.Services.AddCodeSwitchLabel(connectionString);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Giữ nguyên tên claim như trong token. Mặc định ASP.NET đổi "sub" thành
        // một URI dài của schema SOAP cũ, khiến code đọc claim "sub" lặng lẽ trả null.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),

            // Mặc định .NET cho lệch 5 phút. Đặt về 0 để token hết hạn đúng lúc hết hạn.
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Trả enum dưới dạng chuỗi: "Validated" dễ hiểu hơn 1 với người đọc API,
        // và frontend không phải giữ một bảng tra số sang tên.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CodeSwitchLabel API",
        Version = "v1",
        Description = """
            Hệ thống thu thập và kiểm soát chất lượng dữ liệu tiếng nói Việt–Anh (code-switching).
            Đồ án tốt nghiệp FA26SE303.

            **Cách dùng trang này**

            1. Gọi `POST /api/auth/login` với một tài khoản demo.
            2. Chép giá trị `accessToken` trong phản hồi.
            3. Bấm **Authorize** ở góc trên bên phải, dán token vào.
            4. Gọi thử các endpoint khác.

            Mọi lỗi trả về theo chuẩn ProblemDetails (RFC 7807), kèm trường `code`
            bất biến để frontend phân biệt các ca lỗi mà không phụ thuộc câu chữ tiếng Việt.
            """
    });

    // Nút Authorize. Không có nó thì mọi endpoint đều trả 401 và không thử được gì.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Dán thẳng access token vào ô dưới, KHÔNG cần gõ chữ Bearer ở đầu."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Kéo chú thích /// trong controller lên trang Swagger.
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
});

// Frontend nằm ở repo khác và chạy cổng khác, nên phải mở CORS.
// Danh sách origin đọc từ cấu hình — không mở toàn bộ bằng AllowAnyOrigin.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// ---------------------------------------------------------------- pipeline

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CodeSwitchLabel API v1");
        options.DocumentTitle = "CodeSwitchLabel API";

        // Mở localhost là vào thẳng Swagger, khỏi phải gõ thêm /swagger.
        options.RoutePrefix = string.Empty;

        options.DefaultModelsExpandDepth(-1);
    });

    var seedPassword = builder.Configuration["Seed:DefaultPassword"];

    if (!string.IsNullOrWhiteSpace(seedPassword))
    {
        await DatabaseSeeder.SeedAsync(app.Services, seedPassword);
    }

    try
    {
        await app.Services.GetRequiredService<IObjectStorage>().EnsureBucketAsync();
    }
    catch (Exception ex)
    {
        // Kho lưu trữ chưa bật thì API vẫn chạy, chỉ riêng phần bản ghi âm không dùng được.
        // Nhờ vậy ai đang làm module khác không bị buộc phải bật đủ mọi dịch vụ.
        // Phân biệt với THIẾU CẤU HÌNH ở trên: thiếu cấu hình là lỗi cài đặt, phải dừng ngay.
        app.Logger.LogWarning(ex, "Không kết nối được kho lưu trữ file — các endpoint bản ghi âm sẽ lỗi.");
    }
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }))
   .WithTags("0 · Hệ thống")
   .WithSummary("Kiểm tra API còn sống");

app.Run();
