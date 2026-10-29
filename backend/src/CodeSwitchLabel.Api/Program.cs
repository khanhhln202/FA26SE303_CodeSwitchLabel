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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- c蘯･u hﾃｬnh

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "Thi蘯ｿu ConnectionStrings:Postgres. Xem appsettings.Development.json.");

// M盻絞 m盻･c c蘯･u hﾃｬnh ﾄ黛ｻ「 ki盻ノ ngay lﾃｺc kh盻殃 ﾄ黛ｻ冢g thay vﾃｬ ﾄ黛ｻ｣i request ﾄ黛ｺｧu tiﾃｪn m盻嬖 n盻・
// thi蘯ｿu khoﾃ｡ kﾃｽ hay thi蘯ｿu ﾄ黛ｻ蟻 ch盻・kho lﾆｰu tr盻ｯ thﾃｬ ph蘯｣i ch蘯ｿt lﾃｺc ch蘯｡y `dotnet run`,
// khﾃｴng ph蘯｣i lﾃｺc cﾃｳ ngﾆｰ盻拱 ﾄ惰ハg nh蘯ｭp hay n盻冪 b蘯｣n ghi.
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

// ---------------------------------------------------------------- d盻議h v盻･

builder.Services.AddCodeSwitchLabel(connectionString);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Khoﾃ｡ kﾃｽ l蘯･y t盻ｫ IOptions<JwtOptions>, KHﾃ年G ch盻･p c蘯･u hﾃｬnh ngay t蘯｡i ﾄ妥｢y. Ch盻･p s盻嬶 thﾃｬ c蘯･u hﾃｬnh ghi ﾄ妥ｨ
// sau ﾄ妥ｳ 窶・bi蘯ｿn mﾃｴi trﾆｰ盻拵g lﾃｺc deploy, hay WebApplicationFactory trong test 窶・ch盻・ﾃ｡p cho bﾃｪn c蘯･p token
// mﾃ khﾃｴng ﾃ｡p cho bﾃｪn ki盻ノ token, vﾃ l盻擁 hi盻㌻ ra lﾃ 401 "signature key was not found" r蘯･t khﾃｳ l蘯ｧn.
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;

        // Gi盻ｯ nguyﾃｪn tﾃｪn claim nhﾆｰ trong token. M蘯ｷc ﾄ黛ｻ杵h ASP.NET ﾄ黛ｻ品 "sub" thﾃnh
        // m盻冲 URI dﾃi c盻ｧa schema SOAP cﾅｩ, khi蘯ｿn code ﾄ黛ｻ皇 claim "sub" l蘯ｷng l蘯ｽ tr蘯｣ null.
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

            // M蘯ｷc ﾄ黛ｻ杵h .NET cho l盻㌘h 5 phﾃｺt. ﾄ雪ｺｷt v盻・0 ﾄ黛ｻ・token h蘯ｿt h蘯｡n ﾄ妥ｺng lﾃｺc h蘯ｿt h蘯｡n.
            ClockSkew = TimeSpan.Zero
        };

        // ﾄ雪ｻ訴 chi蘯ｿu token v盻嬖 database 盻・m盻擁 request: khoﾃ｡ tﾃi kho蘯｣n hay ﾄ黛ｻ品 vai cﾃｳ hi盻㎡ l盻ｱc NGAY,
        // khﾃｴng ph蘯｣i ﾄ黛ｻ｣i token h蘯ｿt h蘯｡n.
        options.Events = new JwtBearerEvents { OnTokenValidated = AccountStateValidator.ValidateAsync };
    });

builder.Services.AddAuthorization();

// C3: sai vai tr蘯｣ 403 JSON cﾃｳ mﾃ｣ thay vﾃｬ body r盻溶g.
builder.Services.AddSingleton<
    Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler,
    JsonAuthorizationHandler>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Tr蘯｣ enum dﾆｰ盻嬖 d蘯｡ng chu盻擁: "Validated" d盻・hi盻ブ hﾆ｡n 1 v盻嬖 ngﾆｰ盻拱 ﾄ黛ｻ皇 API,
        // vﾃ frontend khﾃｴng ph蘯｣i gi盻ｯ m盻冲 b蘯｣ng tra s盻・sang tﾃｪn.
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
            H盻・th盻創g thu th蘯ｭp vﾃ ki盻ノ soﾃ｡t ch蘯･t lﾆｰ盻｣ng d盻ｯ li盻㎡ ti蘯ｿng nﾃｳi Vi盻㏄窶鄭nh (code-switching).
            ﾄ雪ｻ・ﾃ｡n t盻奏 nghi盻㎝ FA26SE303.
            """
    });

    // Nﾃｺt Authorize. Khﾃｴng cﾃｳ nﾃｳ thﾃｬ m盻絞 endpoint ﾄ黛ｻ「 tr蘯｣ 401 vﾃ khﾃｴng th盻ｭ ﾄ柁ｰ盻｣c gﾃｬ.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Dﾃ｡n th蘯ｳng access token vﾃo ﾃｴ dﾆｰ盻嬖, KHﾃ年G c蘯ｧn gﾃｵ ch盻ｯ Bearer 盻・ﾄ黛ｺｧu."
    });

    // T盻ｫ Swashbuckle 10 (thﾆｰ vi盻㌻ OpenAPI 2.x), yﾃｪu c蘯ｧu b蘯｣o m蘯ｭt tr盻・t盻嬖 ﾄ黛ｻ杵h nghﾄｩa 盻・trﾃｪn
    // b蘯ｱng m盻冲 ﾄ黛ｻ訴 tﾆｰ盻｣ng tham chi蘯ｿu g蘯ｯn v盻嬖 tﾃi li盻㎡, nﾃｪn ph蘯｣i truy盻］ vﾃo dﾆｰ盻嬖 d蘯｡ng hﾃm.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });

    // Nhﾃｳm endpoint x蘯ｿp theo th盻ｩ t盻ｱ khai bﾃ｡o 盻・ApiTags, khﾃｴng theo th盻ｩ t盻ｱ ch盻ｯ cﾃ｡i.
    options.DocumentFilter<ApiTagsDocumentFilter>();

    // Kﾃｩo chﾃｺ thﾃｭch /// trong controller lﾃｪn trang Swagger.
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
});

// Frontend n蘯ｱm 盻・repo khﾃ｡c vﾃ ch蘯｡y c盻貧g khﾃ｡c, nﾃｪn ph蘯｣i m盻・CORS.
// Danh sﾃ｡ch origin ﾄ黛ｻ皇 t盻ｫ c蘯･u hﾃｬnh 窶・khﾃｴng m盻・toﾃn b盻・b蘯ｱng AllowAnyOrigin.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Gi盻嬖 h蘯｡n t盻祖 ﾄ黛ｻ・ﾄ惰ハg nh蘯ｭp / ﾄ惰ハg kﾃｽ: 10 l蘯ｧn m盻擁 phﾃｺt trﾃｪn m盻擁 IP, ngﾄハ brute-force
// vﾃ dﾃｲ email trﾃｪn endpoint cﾃｴng khai.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsDevelopment() ? 100000 : 10,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ---------------------------------------------------------------- pipeline

var app = builder.Build();

app.UseRateLimiter();

// Lﾆｰ盻｣c ﾄ黛ｻ・ database r盻溶g nh蘯ｭn docs/codeswitchlabel.sql; database ﾄ疎ng ch蘯｡y nh蘯ｭn
// migrations/V*.sql qua deploy/deploy.sh. API ch盻・ki盻ノ lﾆｰ盻｣c ﾄ黛ｻ・ khﾃｴng t盻ｱ s盻ｭa.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CodeSwitchLabelDbContext>();
    await DatabaseSeeder.EnsureSchemaAsync(db);
}

app.UseExceptionHandler();

// Ba th盻ｩ dﾆｰ盻嬖 ﾄ妥｢y trﾆｰ盻嫩 kia ch盻・b蘯ｭt 盻・mﾃｴi trﾆｰ盻拵g dev. Nay m盻擁 th盻ｩ cﾃｳ cﾃｴng t蘯ｯc riﾃｪng, vﾃｬ mﾃ｡y ch盻ｧ
// th盻ｭ nghi盻㍊ cho ﾄ黛ｻ冓 frontend c蘯ｧn Swagger vﾃ d盻ｯ li盻㎡ m蘯ｫu, nhﾆｰng KHﾃ年G ﾄ柁ｰ盻｣c ch蘯｡y 盻・ch蘯ｿ ﾄ黛ｻ・Development:
// ch蘯ｿ ﾄ黛ｻ・ﾄ妥ｳ tr蘯｣ nguyﾃｪn n盻冓 dung l盻擁 ra ngoﾃi.
if (builder.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CodeSwitchLabel API v1");
        options.DocumentTitle = "CodeSwitchLabel API";

        // M盻・localhost lﾃ vﾃo th蘯ｳng Swagger, kh盻淑 ph蘯｣i gﾃｵ thﾃｪm /swagger.
        options.RoutePrefix = string.Empty;

        options.DefaultModelsExpandDepth(-1);
    });
}

// Cﾃｳ khoﾃ｡ Seed:DefaultPassword thﾃｬ t蘯｡o tﾃi kho蘯｣n vﾃ cﾃ｢u m蘯ｫu, b蘯･t k盻・mﾃｴi trﾆｰ盻拵g nﾃo.
// B蘯｣n ch蘯｡y th蘯ｭt b盻・tr盻創g khoﾃ｡ nﾃy lﾃ khﾃｴng seed gﾃｬ c蘯｣.
var seedPassword = builder.Configuration["Seed:DefaultPassword"];

if (!string.IsNullOrWhiteSpace(seedPassword))
{
    await DatabaseSeeder.SeedAsync(app.Services, seedPassword);
}

CodeSwitchLabel.Services.Implementations.RecordingService.CleanupStaleTempFiles(app.Logger);

try
{
    // Luﾃｴn th盻ｭ t蘯｡o bucket: mﾃ｡y ch盻ｧ m盻嬖 d盻ｱng thﾃｬ chﾆｰa cﾃｳ, thi蘯ｿu nﾃｳ lﾃ m盻絞 endpoint b蘯｣n ghi ﾃ｢m h盻熟g.
    await app.Services.GetRequiredService<IObjectStorage>().EnsureBucketAsync();
}
catch (Exception ex)
{
    // Kho lﾆｰu tr盻ｯ chﾆｰa b蘯ｭt thﾃｬ API v蘯ｫn ch蘯｡y, ch盻・riﾃｪng ph蘯ｧn b蘯｣n ghi ﾃ｢m khﾃｴng dﾃｹng ﾄ柁ｰ盻｣c.
    // Nh盻・v蘯ｭy ai ﾄ疎ng lﾃm module khﾃ｡c khﾃｴng b盻・bu盻冂 ph蘯｣i b蘯ｭt ﾄ黛ｻｧ m盻絞 d盻議h v盻･.
    // Phﾃ｢n bi盻㏄ v盻嬖 THI蘯ｾU C蘯､U Hﾃ君H 盻・trﾃｪn: thi蘯ｿu c蘯･u hﾃｬnh lﾃ l盻擁 cﾃi ﾄ黛ｺｷt, ph蘯｣i d盻ｫng ngay.
    app.Logger.LogWarning(ex, "Khﾃｴng k蘯ｿt n盻訴 ﾄ柁ｰ盻｣c kho lﾆｰu tr盻ｯ file 窶・cﾃ｡c endpoint b蘯｣n ghi ﾃ｢m s蘯ｽ l盻擁.");
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Sau xﾃ｡c th盻ｱc: t盻ｫ ﾄ妥｢y m盻絞 thay ﾄ黛ｻ品 d盻ｯ li盻㎡ ﾄ黛ｻ「 ﾄ柁ｰ盻｣c nh蘯ｭt kﾃｽ ghi kﾃｨm ngﾆｰ盻拱 th盻ｱc hi盻㌻.
app.UseMiddleware<AuditUserMiddleware>();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }))
   .WithTags(ApiTags.Health)
   .WithSummary("Ki盻ノ tra API cﾃｲn s盻創g");

app.Run();
