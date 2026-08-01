#!/bin/bash

echo "=========================================="
echo "   INICIANDO LAVALINK + BOT .NET"
echo "=========================================="

cd /app/Infra/Lavalink || exit 1

if [ ! -f "Lavalink.jar" ]; then
    echo "❌ ERROR FATAL: No se encuentra Lavalink.jar en /app/Infra/Lavalink!"
    exit 1
fi

echo "🚀 Arrancando proceso Java..."
java -Xms128m -Xmx200m -XX:+UseSerialGC -jar Lavalink.jar &

echo "⏳ Esperando a que Lavalink abra el puerto 2333 y responda HTTP..."

# Bucle de 60 segundos usando Python (incluido por defecto o nativo en muchas imágenes) o socket directo
for i in $(seq 1 30); do
    # Intentamos abrir socket TCP en 127.0.0.1 2333
    exec 3<>/dev/tcp/127.0.0.1/2333 2>/dev/null
    if [ $? -eq 0 ]; then
        echo "✅ ¡Lavalink abrió el puerto 2333 con éxito!"
        exec 3<&-
        exec 3>&-
        break
    fi
    echo "   Lavalink aún está cargando plugins... ($i/30)"
    sleep 2
done

# Damos 3 segundos extra para que Spring Boot levante el WebSocket tras abrir el puerto
sleep 3

cd /app || exit 1
echo "🤖 Iniciando Bot de .NET..."
exec dotnet DiscordBot.dll
