# ---------------------------------------------------
# Etapa 1: Build de .NET 10
# ---------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar .csproj para restaurar dependencias
COPY ["DiscordBot/DiscordBot.csproj", "DiscordBot/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Infrestructure/Infrestructure.csproj", "Infrestructure/"]

RUN dotnet restore "DiscordBot/DiscordBot.csproj"

# Copiar código fuente y compilar
COPY . .
WORKDIR "/src/DiscordBot"
RUN dotnet publish "DiscordBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---------------------------------------------------
# Etapa 2: Runtime (.NET 10 + Java Runtime)
# ---------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Instalar Java JRE para ejecutar Lavalink.jar
RUN apt-get update && apt-get install -y openjdk-21-jre-headless && rm -rf /var/lib/apt/lists/*

# Copiar la app compilada y la carpeta Infra con el JAR
COPY --from=build /app/publish .
COPY Infra /app/Infra

# Copiar y dar permisos al script de entrada
RUN chmod +x /app/Infra/Lavalink/entrypoint.sh

ENV ASPNETCORE_URLS=http://+:${PORT}

ENTRYPOINT ["/app/Infra/Lavalink/entrypoint.sh"]
