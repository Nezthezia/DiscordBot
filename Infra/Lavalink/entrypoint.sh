#!/bin/sh

echo "=== Iniciando Lavalink ==="
# Ejecutar Lavalink e imprimir su log
java -Xmx300m -jar /app/Infra/Lavalink/Lavalink.jar > /app/lavalink.log 2>&1 &

# Esperar 15 segundos
sleep 15

# Si Lavalink se murió en esos 15s, mostrar el log de Java
if ! kill -0 $! 2>/dev/null; then
    echo "❌ CRASH DE LAVALINK DETECTADO. Mostrando logs de Java:"
    cat /app/lavalink.log
    exit 1
fi

echo "=== Lavalink está vivo. Iniciando Bot de .NET ==="
exec dotnet DiscordBot.dll
