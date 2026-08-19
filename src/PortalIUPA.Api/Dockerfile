# API del Portal del Empleado (.NET 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/PortalIUPA.Api/PortalIUPA.Api.csproj", "PortalIUPA.Api/"]
COPY ["src/PortalIUPA.Application/PortalIUPA.Application.csproj", "PortalIUPA.Application/"]
COPY ["src/PortalIUPA.Domain/PortalIUPA.Domain.csproj", "PortalIUPA.Domain/"]
COPY ["src/PortalIUPA.Infrastructure/PortalIUPA.Infrastructure.csproj", "PortalIUPA.Infrastructure/"]
RUN dotnet restore "PortalIUPA.Api/PortalIUPA.Api.csproj"
COPY src/ .
WORKDIR /src/PortalIUPA.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PortalIUPA.Api.dll"]