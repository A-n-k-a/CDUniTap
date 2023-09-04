using CDUniTap.Interfaces.Markers;
using CDUniTap.Models.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace CDUniTap.Cli;

public class MenuCliCommander : ICliCommander
{
    private readonly CasServiceApiOptions _options;
    private readonly IServiceProvider _service;

    public MenuCliCommander(CasServiceApiOptions options, IServiceProvider service)
    {
        _options = options;
        _service = service;
    }
    
    public async Task EnterCommander()
    {
        AnsiConsole.MarkupLine($"欢迎 [green]{_options.StudentId}[/] 使用客户端");
        var requestedService = AnsiConsole.Prompt(new SelectionPrompt<string>()
                               .Title("请选择你要登录的系统")
                               .AddChoices("统一支付平台 (电费查缴)"));
        switch (requestedService)
        {
            case "统一支付平台 (电费查缴)":
                var paym = _service.GetRequiredService<PaymCliCommander>();
                await paym.EnterCommander();
                break;
        }
    }
}