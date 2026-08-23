FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "HousingService/HousingService/HousingService.csproj"
RUN dotnet build "HousingService/HousingService/HousingService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HousingService/HousingService/HousingService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HousingService.dll"]
