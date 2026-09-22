using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using CodeSwitchLabel.Api.Infrastructure;
using CodeSwitchLabel.Repositories.Persistence;
using CodeSwitchLabel.Repositories.Storage;
using CodeSwitchLabel.Services;
using CodeSwitchLabel.Services.Audio;
using CodeSwitchLabel.Services.Options;
using CodeSwitchLabel.Services.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

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

        // Đối chiếu token với database ở mỗi request: khoá tài khoản hay đổi vai có hiệu lực NGAY,
        // không phải đợi token hết hạn.
        options.Events = new JwtBearerEvents { OnTokenValidated = AccountStateValidator.ValidateAsync };
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

    // Từ Swashbuckle 10 (thư viện OpenAPI 2.x), yêu cầu bảo mật trỏ tới định nghĩa ở trên
    // bằng một đối tượng tham chiếu gắn với tài liệu, nên phải truyền vào dưới dạng hàm.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
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

// Dự án KHÔNG dùng migration: lược đồ do PostgreSQL chạy từ docs/codeswitchlabel.sql lúc tạo
// database rỗng. Kiểm ngay lúc khởi động để báo rõ việc phải làm, thay vì để EF ném lỗi khó hiểu
// ở request đầu tiên.
using (var scope = app.Services.CreateScope())
{
    await DatabaseSeeder.EnsureSchemaAsync(
        scope.ServiceProvider.GetRequiredService<CodeSwitchLabelDbContext>());
}

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

// Sau xác thực: từ đây mọi thay đổi dữ liệu đều được nhật ký ghi kèm người thực hiện.
app.UseMiddleware<AuditUserMiddleware>();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }))
   .WithTags("0 · Hệ thống")
   .WithSummary("Kiểm tra API còn sống");

app.Run();
