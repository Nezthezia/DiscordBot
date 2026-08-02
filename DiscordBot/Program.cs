using Application.Interfaces;
using Application.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Background;
using Infrestructure.Services;
using Lavalink4NET.Clients;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton(provider =>
{
    var config = new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildVoiceStates | GatewayIntents.MessageContent,
        MessageCacheSize = 10,
        LogGatewayIntentWarnings = false // Opcional: evita alertas molestas en consola
    };

    return new DiscordSocketClient(config);
});

builder.Services.AddSingleton<Application.Interfaces.IAudioService, LavalinkAudioService>();

// 2. Registramos el servicio de interacciones en el contenedor de .NET
builder.Services.AddSingleton(sp =>
    new InteractionService(sp.GetRequiredService<DiscordSocketClient>().Rest));

builder.Services.AddHostedService<DiscordBotWorker>();

builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

builder.Services.AddLavalink();
builder.Services.AddSingleton<IDiscordClientWrapper, DiscordClientWrapper>();

builder.Services.ConfigureLavalink(options =>
{
    var baseUrl = builder.Configuration["LavalinkConfig:BaseAddress"]!;
    options.BaseAddress = new Uri(baseUrl);
    options.Passphrase = builder.Configuration["LavalinkConfig:Password"] ?? "youshallnotpass";
    options.ReadyTimeout = TimeSpan.FromSeconds(120);
    options.WebSocketUri = new Uri(baseUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "/v4/websocket");
});

builder.Services.AddTransient<IBotCommandService, BotCommandService>();
builder.Services.AddSingleton<DiscordBot.Handler.DiscordMessageListener>();

builder.Services.AddHostedService<WebPollerWorker>();

var app = builder.Build();

app.MapGet("/", () => "Servidor de control de DiscordAudio operando con éxito.");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
