FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ZeroBot.Core/ZeroBot.Core.csproj", "ZeroBot.Core/"]
RUN dotnet restore "ZeroBot.Core/ZeroBot.Core.csproj"
COPY . .
WORKDIR "/src/ZeroBot.Core"
RUN dotnet build "./ZeroBot.Core.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ZeroBot.Core.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ZeroBot.Core.dll"]
