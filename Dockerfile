FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/PettyCash.Domain/PettyCash.Domain.csproj src/PettyCash.Domain/
COPY src/PettyCash.Application/PettyCash.Application.csproj src/PettyCash.Application/
COPY src/PettyCash.Infrastructure/PettyCash.Infrastructure.csproj src/PettyCash.Infrastructure/
COPY src/PettyCash.Api/PettyCash.Api.csproj src/PettyCash.Api/
RUN dotnet restore src/PettyCash.Api/PettyCash.Api.csproj
COPY src/ src/
RUN dotnet publish src/PettyCash.Api/PettyCash.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "PettyCash.Api.dll"]
