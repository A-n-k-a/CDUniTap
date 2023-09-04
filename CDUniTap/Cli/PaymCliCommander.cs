using CDUniTap.Interfaces.Markers;
using CDUniTap.Services.Api;
using Spectre.Console;

namespace CDUniTap.Cli;

public class PaymCliCommander : ICliCommander
{
    private readonly PaymServiceApi _paymServiceApi;
    private bool authenticated = false;

    public PaymCliCommander(PaymServiceApi paymServiceApi)
    {
        _paymServiceApi = paymServiceApi;
    }

    public async Task EnterCommander()
    {
        if (!authenticated) await Authenticate();
    }

    public async Task Authenticate()
    {
        await AnsiConsole.Progress()
                         .AutoRefresh(true)
                         .AutoClear(true)
                         .StartAsync(async context =>
                         {
                             var task = context.AddTask("正在尝试使用 Cas 系统认证 统一支付平台");
                             task.IsIndeterminate = true;
                             authenticated = await _paymServiceApi.AuthenticateByCas();
                             task.StopTask();
                             if (!authenticated)
                             {
                                 AnsiConsole.MarkupLine("[red]认证失败, 请尝试重新认证[/]");
                                 return;
                             }
                             AnsiConsole.MarkupLine("[green]认证成功[/]");
                         });
    }
}