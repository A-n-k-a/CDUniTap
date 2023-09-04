using CDUniTap.Interfaces.Markers;
using CDUniTap.Services.Api;
using Spectre.Console;

namespace CDUniTap.Cli;

public class PaymCliCommander : ICliCommander
{
    private readonly PaymServiceApi _paymServiceApi;
    private bool authenticated = false;
    private List<ProjectDto>? _projects;

    public PaymCliCommander(PaymServiceApi paymServiceApi)
    {
        _paymServiceApi = paymServiceApi;
    }

    public async Task EnterCommander()
    {
        if (!authenticated) await Authenticate();
        if (!authenticated) return;
        await GetUserInfo();
        await GetAllProjects();
        await AskProjectChoice();
    }

    private async Task AskProjectChoice()
    {
        var choice = new SelectionPrompt<string>();
        choice.Converter = s => { return _projects?.First(t => t.Id == s).Name ?? "未知"; };
        var projectId = AnsiConsole.Prompt(choice.AddChoices(_projects?.Select(t => t.Id) ?? new List<string>()));
        switch (projectId)
        {
            case "7a99ede5475b55a03adb936454463994": // 新开普电费
                break;
        }
    }

    public async Task GetUserInfo()
    {
        await AnsiConsole.Status()
                         .Spinner(Spinner.Known.Dots)
                         .StartAsync("正在获取用户信息", async _ =>
                         {
                             var userInfo = await _paymServiceApi.GetUserInfo();
                             AnsiConsole.MarkupLine(
                                 $"欢迎 [green]{userInfo.Name}[/] ({userInfo.StudentId} [[{userInfo.Sex}]])");
                         });
    }

    public async Task GetAllProjects()
    {
        await AnsiConsole.Status()
                         .Spinner(Spinner.Known.Dots)
                         .StartAsync("正在获取可选项目信息", async _ =>
                         {
                             _projects = await _paymServiceApi.GetAllProjects();
                             AnsiConsole.MarkupLine($"获取成功, 共 [green]{_projects?.Count}[/] 个");
                         });
    }

    public async Task Authenticate()
    {
        await AnsiConsole.Status()
                         .AutoRefresh(true)
                         .StartAsync("正在尝试通过 Cas 系统认证 统一支付平台", async statusContext =>
                         {
                             statusContext.Spinner(Spinner.Known.Dots);
                             authenticated = await _paymServiceApi.AuthenticateByCas();
                             if (!authenticated)
                             {
                                 AnsiConsole.MarkupLine("[red]认证统一支付平台失败, 请尝试重新认证[/]");
                                 return;
                             }

                             AnsiConsole.MarkupLine("[green]认证统一支付平台成功[/]");
                         });
    }
}