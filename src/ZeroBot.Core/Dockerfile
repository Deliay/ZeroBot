FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/ZeroBot.Core/ZeroBot.Core.csproj", "src/ZeroBot.Core/"]
COPY ["src/plugins/ZeroBot.AI/ZeroBot.AI.csproj", "src/plugins/ZeroBot.AI/"]
COPY ["src/ZeroBot.Utility/ZeroBot.Utility.csproj", "src/ZeroBot.Utility/"]
COPY ["src/ZeroBot.Abstraction/ZeroBot.Abstraction.csproj", "src/ZeroBot.Abstraction/"]
COPY ["src/plugins/ZeroBot.Bilibili/ZeroBot.Bilibili.csproj", "src/plugins/ZeroBot.Bilibili/"]
COPY ["src/plugins/ZeroBot.ComfyUI/ZeroBot.ComfyUI.csproj", "src/plugins/ZeroBot.ComfyUI/"]
COPY ["src/plugins/ZeroBot.Endfield.Api/ZeroBot.Endfield.Api.csproj", "src/plugins/ZeroBot.Endfield.Api/"]
COPY ["src/plugins/ZeroBot.Endfield/ZeroBot.Endfield.csproj", "src/plugins/ZeroBot.Endfield/"]
COPY ["src/plugins/ZeroBot.Endfield.Credential.Json/ZeroBot.Endfield.Credential.Json.csproj", "src/plugins/ZeroBot.Endfield.Credential.Json/"]
COPY ["src/plugins/ZeroBot.PermissionCommandPlugin/ZeroBot.PermissionCommandPlugin.csproj", "src/plugins/ZeroBot.PermissionCommandPlugin/"]
COPY ["src/plugins/ZeroBot.TestPlugin/ZeroBot.TestPlugin.csproj", "src/plugins/ZeroBot.TestPlugin/"]
COPY ["src/plugins/ZeroBot.Milky/ZeroBot.Milky.csproj", "src/plugins/ZeroBot.Milky/"]
COPY ["src/plugins/ZeroBot.Repository.Mongo/ZeroBot.Repository.Mongo.csproj", "src/plugins/ZeroBot.Repository.Mongo/"]
COPY ["Directory.Packages.props", "Directory.Packages.props"]
RUN dotnet restore "src/ZeroBot.Core/ZeroBot.Core.csproj"
COPY . .
WORKDIR "/src/src/ZeroBot.Core"
RUN dotnet build "./ZeroBot.Core.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ZeroBot.Core.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
WORKDIR /data
ENTRYPOINT ["dotnet", "/app/ZeroBot.Core.dll"]
