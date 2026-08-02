# ---------------------------------------------------
# Etapa 1: Build de .NET 10
# ---------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["DiscordBot/DiscordBot.csproj", "DiscordBot/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Infrestructure/Infrestructure.csproj", "Infrestructure/"]
RUN apt-get update && apt-get install -y && rm -rf /var/lib/apt/lists/*
COPY . .
WORKDIR "/src/DiscordBot"
RUN dotnet publish "DiscordBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---------------------------------------------------
# Etapa 2: Runtime .NET 10
# ---------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DiscordBot.dll"]
