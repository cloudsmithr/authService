# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first (better layer caching)
COPY AuthService.sln ./
COPY src/AuthService.Api/*.csproj ./src/AuthService.Api/
COPY src/AuthService.Application/*.csproj ./src/AuthService.Application/
COPY src/AuthService.Domain/*.csproj ./src/AuthService.Domain/
COPY src/AuthService.Infrastructure/*.csproj ./src/AuthService.Infrastructure/

RUN dotnet restore

# Copy everything else and build
COPY . .
WORKDIR /src/src/AuthService.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "AuthService.Api.dll"]
