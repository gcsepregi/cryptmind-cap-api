# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Copy solution and restore dependencies
COPY ["CryptMindCapAPI.sln", "./"]
COPY ["CryptMindCapAPI/CryptMindCapAPI.csproj", "CryptMindCapAPI/"]
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish "CryptMindCapAPI/CryptMindCapAPI.csproj" -c Release -o /app/publish

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
# ASP.NET Core usually listens on port 8080 in .NET 8+ containers
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CryptMindCapAPI.dll"]