#!/bin/sh

echo "=========================================="
echo "   INICIANDO LAVALINK + BOT .NET"
echo "=========================================="

cd /app/Infra/Lavalink

echo "🚀 Arrancando Lavalink en segundo plano..."
java -Xms128m -Xmx200m -XX:+UseSerialGC -jar Lavalink.jar --server.port=2333 &

echo "⏳ Esperando a que Lavalink esté listo en puerto 2333..."
until (echo > /dev/tcp/localhost/2333) 2>/dev/null; do
  sleep 2
done

echo "✅ Lavalink listo, iniciando Bot de .NET..."
cd /app
export ASPNETCORE_HTTP_PORTS=8080
exec dotnet DiscordBot.dll
