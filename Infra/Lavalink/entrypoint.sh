#!/bin/sh
# Forzamos el límite estricto de heap para Java a 350MB
java -Xmx350m -Xms128m -XX:+UseG1GC -jar /app/Infra/Lavalink.jar &

# Iniciar el bot de .NET
exec dotnet DiscordBot.dll
