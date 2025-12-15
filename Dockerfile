# Use the official .NET 10.0 runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the .NET 10.0 SDK as build image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["Wex.CorporatePayments.Api/Wex.CorporatePayments.Api.csproj", "Wex.CorporatePayments.Api/"]
COPY ["Wex.CorporatePayments.Application/Wex.CorporatePayments.Application.csproj", "Wex.CorporatePayments.Application/"]
COPY ["Wex.CorporatePayments.Domain/Wex.CorporatePayments.Domain.csproj", "Wex.CorporatePayments.Domain/"]
COPY ["Wex.CorporatePayments.Infrastructure/Wex.CorporatePayments.Infrastructure.csproj", "Wex.CorporatePayments.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "Wex.CorporatePayments.Api/Wex.CorporatePayments.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/Wex.CorporatePayments.Api"
RUN dotnet build "Wex.CorporatePayments.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Wex.CorporatePayments.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:80/health || exit 1

ENTRYPOINT ["dotnet", "Wex.CorporatePayments.Api.dll"]
