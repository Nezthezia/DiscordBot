#!/bin/sh

echo "=========================================="
echo "   INICIANDO LAVALINK + BOT .NET"
echo "=========================================="

cd /app/Infra/Lavalink

echo "🚀 Arrancando Lavalink en segundo plano..."
java -Xms128m -Xmx200m -XX:+UseSerialGC -jar Lavalink.jar &

echo "⏳ Dando 60 segundos a Java para compilar plugins e iniciar WebSocket..."
sleep 60

cd /app
echo "🤖 Iniciando Bot de .NET..."
exec dotnet DiscordBot.dll
