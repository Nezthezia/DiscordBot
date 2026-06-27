using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Background;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds |
                     GatewayIntents.GuildMessages |
                     GatewayIntents.MessageContent
}));

// 2. Registramos el servicio de interacciones en el contenedor de .NET
builder.Services.AddSingleton(sp =>
    new InteractionService(sp.GetRequiredService<DiscordSocketClient>().Rest));

builder.Services.AddHostedService<DiscordBotWorker>();

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
