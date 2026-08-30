using Checkers.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

var app = builder.Build();

app.MapGet("/", () => "Online Checkers server is running.");

app.MapHub<GameHub>("/gamehub");

app.Run();