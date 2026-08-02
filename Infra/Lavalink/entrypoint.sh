#!/bin/sh

echo "=========================================="
echo "   INICIANDO LAVALINK + BOT .NET"
echo "=========================================="

cd /app/Infra/Lavalink

echo "🚀 Arrancando Lavalink en segundo plano..."
java -Xms128m -Xmx200m -XX:+UseSerialGC -jar Lavalink.jar &

echo "⏳ Esperando a que Lavalink esté listo en puerto 2333..."
while ! nc -z localhost 2333; do
  sleep 10
done

echo "✅ Lavalink listo, iniciando Bot de .NET..."
cd /app
exec dotnet DiscordBot.dll
