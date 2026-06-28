using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Background;
using Infrestructure.Services;
using Lavalink4NET.Extensions;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Clients;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton(provider =>
{
    var config = new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildVoiceStates,
        LogGatewayIntentWarnings = false // Opcional: evita alertas molestas en consola
    };

    return new DiscordSocketClient(config);
});

builder.Services.AddSingleton<Application.Interfaces.IAudioService, LavalinkAudioService>();

// 2. Registramos el servicio de interacciones en el contenedor de .NET
builder.Services.AddSingleton(sp =>
    new InteractionService(sp.GetRequiredService<DiscordSocketClient>().Rest));

builder.Services.AddHostedService<DiscordBotWorker>();

builder.Services.AddLavalink();
builder.Services.AddSingleton<IDiscordClientWrapper, DiscordClientWrapper>();

builder.Services.ConfigureLavalink(options =>
{
    options.BaseAddress = new Uri(builder.Configuration["LavalinkConfig:BaseAddress"]!);
    options.Passphrase = builder.Configuration["LavalinkConfig:Password"]!;
});

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
