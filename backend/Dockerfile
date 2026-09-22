# Image chạy API cho cả nhóm, nhất là đội frontend: có Docker là chạy được,
# không cần cài .NET SDK hay ffmpeg lên máy.
#
# Nền Ubuntu 24.04 (noble) — image chính thức mặc định của .NET 10, và là một nhánh của Debian.
# Microsoft không phát hành bản Debian cho .NET 10 chính thức; ghi rõ tag "noble" để image
# không tự đổi hệ điều hành nếu Microsoft đổi mặc định về sau.

# ---------------------------------------------------------------- build
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

# Chép file project và restore TRƯỚC khi chép code: sửa code mà không đổi gói NuGet
# thì Docker dùng lại lớp đã cache, lần build sau nhanh hơn nhiều.
COPY src/CodeSwitchLabel.Api/CodeSwitchLabel.Api.csproj src/CodeSwitchLabel.Api/
COPY src/CodeSwitchLabel.Services/CodeSwitchLabel.Services.csproj src/CodeSwitchLabel.Services/
COPY src/CodeSwitchLabel.Repositories/CodeSwitchLabel.Repositories.csproj src/CodeSwitchLabel.Repositories/
RUN dotnet restore src/CodeSwitchLabel.Api/CodeSwitchLabel.Api.csproj

COPY src/ src/
RUN dotnet publish src/CodeSwitchLabel.Api/CodeSwitchLabel.Api.csproj -c Release -o /app --no-restore

# ---------------------------------------------------------------- chạy
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime

# ffmpeg và ffprobe: chuyển mọi định dạng trình duyệt ghi ra sang WAV 16 kHz mono, rồi đo thời lượng.
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# Image .NET có sẵn một người dùng không có quyền root. Chạy bằng người này để nếu API bị khai thác
# thì kẻ tấn công cũng không có quyền quản trị bên trong container.
USER $APP_UID

# Image .NET từ bản 8 trở đi mặc định nghe cổng 8080.
EXPOSE 8080
ENTRYPOINT ["dotnet", "CodeSwitchLabel.Api.dll"]
