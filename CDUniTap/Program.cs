using System.Reflection;
using CDUniTap.Cli;
using CDUniTap.Extensions;
using CDUniTap.Interfaces;
using CDUniTap.Interfaces.Markers;
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
var progress = AnsiConsole.Progress().HideCompleted(true).AutoRefresh(true).Start(ctx => ctx.AddTask("正在初始化程序"));

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddServicesAsSelfImplements<ICliCommander>();
builder.Services.AddServicesAsSelfImplements<IHttpApiServiceBase>();
builder.Services.AddSingleton<HttpClient>(new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false }));


var app = builder.Build();

progress.StopTask();

// 读取本地的存储的用户信息
if (!File.Exists("config.json"))
{
    var cliCommander = app.Services.GetRequiredService<CasCliCommander>();
    await cliCommander.EnterCommander();
}

var menuCommander = app.Services.GetRequiredService<MenuCliCommander>();
await menuCommander.EnterCommander();