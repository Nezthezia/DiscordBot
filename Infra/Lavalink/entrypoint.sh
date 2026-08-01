#!/bin/sh

echo "=========================================="
echo "   INICIANDO LAVALINK + BOT .NET"
echo "=========================================="

# 1. Movernos al directorio donde está el .jar y application.yml
cd /app/Infra/Lavalink

# 2. Comprobar que el archivo .jar existe
if [ ! -f "Lavalink.jar" ]; then
    echo "❌ ERROR FATAL: No se encuentra Lavalink.jar en /app/Infra/Lavalink!"
    ls -la
    exit 1
fi

echo "🚀 Arrancando proceso Java..."
# Iniciar Java imprimiendo los logs en la consola principal
java -Xms128m -Xmx200m -XX:+UseSerialGC -jar Lavalink.jar &

echo "⏳ Esperando a que el puerto 2333 responda..."
# Esperar activamente a que el servidor WebSocket de Lavalink abra el puerto 2333
for i in $(seq 1 30); do
    if nc -z localhost 2333; then
        echo "✅ ¡Lavalink abrió el puerto 2333 con éxito!"
        break
    fi
    echo "   Esperando a Lavalink... ($i/30)"
    sleep 2
done

# Regresar a la carpeta principal e iniciar .NET
cd /app
echo "🤖 Iniciando Bot de .NET..."
exec dotnet DiscordBot.dll
