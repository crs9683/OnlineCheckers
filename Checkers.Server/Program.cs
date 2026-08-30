using Checkers.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval =
        TimeSpan.FromSeconds(5);

    options.ClientTimeoutInterval =
        TimeSpan.FromSeconds(15);
});

var app = builder.Build();

app.MapGet("/", () => "Online Checkers server is running.");

app.MapHub<GameHub>("/gamehub");

app.Run();