using System.Reflection;
using System.Text.Json;
using CDUniTap.Cli;
using CDUniTap.Extensions;
using CDUniTap.Interfaces;
using CDUniTap.Interfaces.Markers;
using CDUniTap.Models.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;


string logo =
    $"""
       ____   ____    _   _       _____
      / ___| |  _ \  | | | |     |_   _|
     | |     | | | | | | | |       | |
     | |___  | |_| | | |_| |       | |
      \____| |____/   \___/   ni   |_|   ap
      
      ===  Version: {Assembly.GetExecutingAssembly().GetName().Version?.ToString()}  ===
      ===  By     : Kengwang ===

     """;
Console.WriteLine(logo);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddServicesAsSelfImplements<ICliCommander>();
builder.Services.AddServicesAsSelfImplements<IHttpApiServiceBase>();
builder.Services.AddSingleton<HttpClient>(new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false }));

// 读取本地的存储的用户信息
if (File.Exists("config.json"))
{
    builder.Services.AddSingleton(JsonSerializer.Deserialize<CasServiceApiOptions>(await File.ReadAllTextAsync("config.json")!)!);
}
else
{
    builder.Services.AddSingleton(new CasServiceApiOptions());
}

var app = builder.Build();


var cliCommander = app.Services.GetRequiredService<CasCliCommander>();
await cliCommander.EnterCommander();


var menuCommander = app.Services.GetRequiredService<MenuCliCommander>();
await menuCommander.EnterCommander();