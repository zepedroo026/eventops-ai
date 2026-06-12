FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .

RUN dotnet publish src/EventOps.API/EventOps.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Render injeta PORT em runtime; o Program.cs lê-a e define UseUrls.
# Fallback para 10000 caso o contentor seja corrido localmente sem PORT.
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "EventOps.API.dll"]
