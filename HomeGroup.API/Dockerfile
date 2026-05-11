FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["HomeGroup.API/HomeGroup.API.csproj", "HomeGroup.API/"]
RUN dotnet restore "HomeGroup.API/HomeGroup.API.csproj"
COPY . .
WORKDIR "/src/HomeGroup.API"
RUN dotnet publish "HomeGroup.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HomeGroup.API.dll"]
