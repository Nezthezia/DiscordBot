# 🎵 Discord Music Bot

Bot de música para Discord desarrollado en **C# .NET 10** con arquitectura limpia. Soporta búsqueda y reproducción desde **Spotify** y **Deezer** mediante el plugin [LavaSrc](https://github.com/topi314/LavaSrc) sobre un servidor **Lavalink** propio.

---

## 🛠️ Stack

| Tecnología | Descripción |
|---|---|
| .NET 10 | Framework principal |
| Discord.Net | Librería cliente de Discord (Slash Commands) |
| Lavalink4NET | Integración .NET con el servidor de audio |
| Lavalink v4 + LavaSrc | Servidor de audio con soporte Spotify y Deezer |
| Docker & Docker Compose | Contenerización del servidor Lavalink y el bot |

---

## 📁 Estructura del Proyecto

```
📦 DiscordMusicBot/
├── src/
│   ├── DiscordBot/               # Punto de entrada, comandos y eventos de Discord
│   │   ├── Background/           # Workers (DiscordBotWorker, WebPollerWorker)
│   │   ├── Builders/             # Constructores de embeds y componentes de UI
│   │   ├── Handler/              # Manejadores de eventos y autocomplete
│   │   ├── Moduls/               # Módulos de Slash Commands (MusicModule, GeneralModule)
│   │   └── Services/             # Servicios de UI del reproductor
│   ├── Application/              # Casos de uso, interfaces y DTOs
│   │   ├── Common/Constants/     # Constantes compartidas (AudioButtonIds, etc.)
│   │   ├── DTOs/                 # Objetos de transferencia (TrackInfoDto)
│   │   ├── Interfaces/           # IAudioService, IBotCommandService, IDeezerModule, ISpotifyModule
│   │   └── Services/             # BotCommandService
│   ├── Domain/                   # Entidades y lógica de dominio pura
│   └── Infrestructure/           # Implementaciones concretas
│       └── Services/             # LavalinkAudioService, DeezerModule, SpotifyModule
└── Infra/
    └── Lavalink/                 # Servidor de audio
        ├── Dockerfile.lavalink   # Imagen basada en eclipse-temurin:21-jre
        ├── Lavalink.jar          # Binario del servidor Lavalink
        └── application.yml       # Configuración del servidor (no subir al repo)
```

> `application.yml` contiene la contraseña del servidor. Usa `application_place_holder.yml` como base y **no lo incluyas en el repositorio**.

---

## 🎮 Comandos disponibles

### 🎵 Música

| Comando | Descripción |
|---|---|
| `/play <búsqueda o URL>` | Busca y reproduce una canción, o la añade a la cola |
| `/skip` | Salta la canción que se está reproduciendo actualmente |
| `/pause` | Pausa la reproducción actual |
| `/resume` | Reanuda la reproducción pausada |
| `/stop` | Detiene la reproducción y desconecta el bot |
| `/previous` | Vuelve a la canción anterior o reinicia la actual |
| `/seek <tiempo>` | Adelanta o atrasa la canción actual a un tiempo específico |
| `/volume <valor>` | Ajusta el volumen del reproductor |

### 📋 Cola

| Comando | Descripción |
|---|---|
| `/list` | Muestra la lista de canciones en cola |
| `/remove <canción>` | Elimina una canción de la lista (con autocompletado) |
| `/clear` | Limpia toda la lista de canciones |
| `/shuffle` | Reordena la música de manera aleatoria |
| `/move <origen> <destino>` | Mueve una canción a otra posición en la cola |
| `/loop` | Activa la repetición en bucle de la canción actual |
| `/unloop` | Desactiva la repetición en bucle |

### ⚙️ General

| Comando | Descripción |
|---|---|
| `/help` | Muestra todos los comandos disponibles del bot |
| `/hola` | Ping de prueba |

---

## ⚙️ Configuración

### `appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "DiscordConfig": {
    "Token": "TOKEN_BOT",
    "TestGuildId": "ID_SERVER_PRUEBAS",
    "AnnouncementChannelId": "ID_CANAL_ANNOUNCEMENTS",
    "LogsChannelId": "ID_CANAL_LOGS"
  },
  "LavalinkConfig": {
    "BaseAddress": "http://lavalink:2333",
    "Password": "UNA_CONTRASEÑA"
  }
}
```

> ⚠️ Crea un `appsettings.Production.json` con los valores reales para producción y **nunca lo subas al repositorio**.

---

### `Infra/Lavalink/application.yml`

Copia `application_place_holder.yml` como `application.yml` y completa los valores:

```yaml
server:
  port: 2333
  address: 0.0.0.0

lavalink:
  plugins:
    - dependency: "com.github.topi314.lavasrc:lavasrc-plugin:4.8.3"

  server:
    password: "UNA_CONTRASEÑA"         # debe coincidir con LavalinkConfig.Password
    sources:
      youtube: false
      local: true
      soundcloud: true
    bufferDurationMs: 400
    playerUpdateInterval: 5

logging:
  level:
    root: INFO
    lavalink: INFO
```

Alternativamente, la contraseña se puede inyectar por variable de entorno:

```bash
LAVALINK_PASSWORD=tu_contraseña_segura
```

---

## 🐳 Docker

El proyecto incluye un `docker-compose.yml` en la raíz que levanta tanto el servidor Lavalink como el bot en contenedores separados.

```yaml
services:
  lavalink:
    build:
      context: .
      dockerfile: Infra/Lavalink/Dockerfile.lavalink   # usa eclipse-temurin:21-jre + Lavalink.jar
    container_name: lavalink
    restart: unless-stopped

  bot:
    build:
      context: .
      dockerfile: Dockerfile                            # build multietapa .NET 10 SDK → ASP.NET Runtime
    container_name: discordbot
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    depends_on:
      - lavalink
    restart: unless-stopped
    ports:
      - "8080:8080"
```

> El bot usa `http://lavalink:2333` como `BaseAddress` en producción ya que ambos servicios comparten la red Docker interna.

---

## 🚀 Cómo ejecutar

### Prerrequisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) y Docker Compose
- Token de bot de Discord ([Discord Developer Portal](https://discord.com/developers/applications))
- Intents habilitados en el portal: **Message Content Intent**, **Server Members Intent**

### Desarrollo local

1. **Clonar el repositorio**
   ```bash
   git clone https://github.com/tu-usuario/tu-repo.git
   cd tu-repo
   ```

2. **Configurar credenciales**

   Crea `src/DiscordBot/appsettings.Development.json`:
   ```json
   {
     "DiscordConfig": {
       "Token": "TU_TOKEN_AQUI"
     },
     "LavalinkConfig": {
       "BaseAddress": "http://localhost:2333",
       "Password": "TU_PASSWORD_LAVALINK"
     }
   }
   ```

   Crea `Infra/Lavalink/application.yml` (copia desde `application_place_holder.yml`) y establece la misma contraseña.

3. **Levantar todo con Docker Compose**
   ```bash
   docker compose up --build
   ```

   O si prefieres correr el bot directamente desde .NET:
   ```bash
   # Terminal 1 — solo Lavalink
   docker compose up lavalink

   # Terminal 2 — bot en local
   dotnet run --project src/DiscordBot
   ```

### Producción (VPS)

```bash
# Clonar y configurar appsettings.Production.json + application.yml

docker compose up -d --build

# Ver logs
docker compose logs -f bot
docker compose logs -f lavalink
```

---

## 📋 Permisos requeridos en Discord

El bot necesita los siguientes permisos en el servidor:

- `Conectar` — para unirse al canal de voz
- `Hablar` — para reproducir audio
- `Leer mensajes / Ver canales` — para recibir comandos
- `Enviar mensajes` — para responder con embeds
- `Usar comandos de aplicación` — para los Slash Commands

---

## 🤝 Contribuciones

¡Las contribuciones son bienvenidas! Abre un issue antes de enviar un Pull Request para discutir los cambios propuestos.

---

## 📄 Licencia

Este proyecto está bajo la licencia [MIT](LICENSE).