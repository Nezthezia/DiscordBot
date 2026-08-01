#!/bin/bash

echo "=========================================="
echo "   INICIANDO LAVALINK + BOT .NET"
echo "=========================================="

# 1. Posicionarse en el directorio de Lavalink
cd /app/Infra/Lavalink || exit 1

# 2. Verificar que Lavalink.jar exista
if [ ! -f "Lavalink.jar" ]; then
    echo "❌ ERROR FATAL: No se encuentra Lavalink.jar en /app/Infra/Lavalink!"
    ls -la
    exit 1
fi

echo "🚀 Arrancando proceso Java (Lavalink v4)..."
java -Xms128m -Xmx200m -XX:+UseSerialGC -jar Lavalink.jar &

echo "⏳ Esperando a que Lavalink abra el puerto 2333..."

# 3. Comprobación usando sockets nativos de bash o wget (sin necesitar nc)
for i in $(seq 1 45); do
    if (echo > /dev/tcp/localhost/2333) 2>/dev/null || wget -q --spider http://localhost:2333 2>/dev/null; then
        echo "✅ ¡Lavalink respondió en el puerto 2333 con éxito!"
        break
    fi
    echo "   Cargando plugins y servidor WebSocket... ($i/45)"
    sleep 2
done

# 4. Volver al directorio raíz e iniciar .NET
cd /app || exit 1
echo "🤖 Iniciando Bot de .NET..."
exec dotnet DiscordBot.dll
