# =============================================================================
# DOCKERFILE FOR SHIFTHANDOVER BACKEND WEB API (.NET 8)
# =============================================================================

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for better layer caching
COPY ["src/ShiftHandOver.Share/ShiftHandOver.Share.csproj", "src/ShiftHandOver.Share/"]
COPY ["src/ShiftHandOver.Server/ShiftHandOver.Server/ShiftHandOver.Server.csproj", "src/ShiftHandOver.Server/ShiftHandOver.Server/"]
RUN dotnet restore "src/ShiftHandOver.Server/ShiftHandOver.Server/ShiftHandOver.Server.csproj"

# Copy full source code
COPY ["src/ShiftHandOver.Share/", "src/ShiftHandOver.Share/"]
COPY ["src/ShiftHandOver.Server/ShiftHandOver.Server/", "src/ShiftHandOver.Server/ShiftHandOver.Server/"]

WORKDIR "/src/src/ShiftHandOver.Server/ShiftHandOver.Server"
RUN dotnet publish "ShiftHandOver.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Expose HTTP port 5000
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "ShiftHandOver.Server.dll"]
