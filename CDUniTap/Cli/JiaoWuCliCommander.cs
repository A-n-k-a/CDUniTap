using System.Text.RegularExpressions;
using CDUniTap.Interfaces.Markers;
using CDUniTap.Services.Api;
using Spectre.Console;

namespace CDUniTap.Cli;

public partial class JiaoWuCliCommander : ICliCommander
{
    private bool _authenticated = false;
    private readonly JiaoWuServiceApi _jiaoWuServiceApi;

    public JiaoWuCliCommander(JiaoWuServiceApi jiaoWuServiceApi)
    {
        _jiaoWuServiceApi = jiaoWuServiceApi;
    }

    public async Task EnterCommander()
    {
        if (!_authenticated) await Authenticate();
        if (!_authenticated) return;
        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("请选择要使用的功能")
            .AddChoices("查询在校学生")
            .AddChoices("查询本人课表")
        );
        switch (choice)
        {
            case "查询在校学生":
                await SearchStudent();
                break;
            case "查询本人课表":
                await GetNewCurriculumTable();
                break;
        }
    }

    private async Task GetNewCurriculumTable()
    {
        JiaoWuServiceApi.CurriculumPreRequestInfo? info = null;
        await AnsiConsole.Status()
            .StartAsync("正在获取课表架构",
                async context => { info = await _jiaoWuServiceApi.GetMyCurriculumPreRequestInfo(); });
        var selection = new SelectionPrompt<string>()
            .Title("请选择周次");
        foreach (var (weekName, _) in info?.AvalableWeeks ?? throw new Exception("获取课表架构失败"))
        {
            selection.AddChoice(weekName);
        }

        var selected = AnsiConsole.Prompt(selection);
        var rawResult =
            await _jiaoWuServiceApi.GetNewWeekScheduleRaw(info.SjmsValue, info.Xqids[0], info.AvalableWeeks[selected]);
        var classes = ParseClassInfo(rawResult, DateOnly.Parse(info.AvalableWeeks[selected]));
        var actionSelection = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("请选择操作")
            .AddChoices("显示课表")
            .AddChoices("导出为 ics"));
        switch (actionSelection)
        {
            case "显示课表":
                DisplayClassInfos(classes);
                break;
        }

    }

    
    
    private void DisplayClassInfos(List<ClassInfo> classInfos)
    {
        var datedClass = classInfos.GroupBy(t => t.Date).ToList();
        var indexedClass = classInfos.GroupBy(t => t.IndexInDay).ToList();
        var table = new Table();
        table.Border = TableBorder.AsciiDoubleHead;
        foreach (var grouping in datedClass)
        {
            table.AddColumn(grouping.Key.ToString("O"));
        }

        foreach (var grouping in indexedClass)
        {
            var panels = new List<Panel>();
            var lastDate = classInfos.FirstOrDefault()?.Date.DayNumber ?? 0;
            foreach (var classInfo in grouping)
            {   
                for (int i = lastDate; i < classInfo.Date.DayNumber - 1; i++)
                {
                    panels.Add(new Panel("无课程"));
                }
                panels.Add(new Panel(new Rows(
                    new Markup($"[green]{classInfo.ClassName.EscapeMarkup()}[/]"),
                    new Text(classInfo.Location),
                    new Text(classInfo.Teacher)
                )));
                lastDate = classInfo.Date.DayNumber;

            }

            table.AddRow(panels);
        }
        AnsiConsole.Clear();
        AnsiConsole.Write(table);
    }
    
    private List<ClassInfo> ParseClassInfo(string rawResult, DateOnly startDate)
    {
        // 先获取出 6*7 = 42 个课程
        var classRawInfos = Regex
            .Matches(rawResult, @"<td align=""left"">\r\n\s*\r\n(.*)\r\n\r\n\s*</td>", RegexOptions.Multiline).ToList()
            .Select(t => t.Groups[1].Value).ToList();
        var ret = new List<ClassInfo>();
        for (var index = 0; index < classRawInfos.Count; index++)
        {
            var classRawInfo = classRawInfos[index];
            if (string.IsNullOrWhiteSpace(classRawInfo)) continue;
            // 使用正则匹配每个课程的信息
            const string pattern =
                @"<span onmouseover='kbtc\(this\)' onmouseout='kbot\(this\)' class='box' style='[^']*'><p>[^<]*</p><p>([^<]*)</p><span class='text'>([^<]*)</span></span><div class='item-box' ><p>(\S*)</p><div class='tch-name'><span>(\S*)</span><span>([^<]*)</span></div><div><span><img src='/jsxsd/assets_v1/images/item1.png'>([^<]*)</span>";
            var match = ClassRawInfoRegex().Match(classRawInfo);
            var classInfo = new ClassInfo
            {
                Date = startDate.AddDays(index % 7),
                IndexInDay = index / 7,
                ClassName = match.Groups[3].Value,
                Teacher = match.Groups[1].Value,
                Score = match.Groups[4].Value,
                Location = match.Groups[6].Value,
                ClassSchedule = match.Groups[5].Value,
                ClassWeek = match.Groups[2].Value
            };
            ret.Add(classInfo);
        }

        return ret;
    }

    class ClassInfo
    {
        public DateOnly Date { get; set; }
        public int IndexInDay { get; set; }
        public string ClassName { get; set; }
        public string Teacher { get; set; }
        public string Score { get; set; }
        public string Location { get; set; }
        public string ClassSchedule { get; set; }
        public string ClassWeek { get; set; }
    }

    private async Task SearchStudent()
    {
        while (true)
        {
            var searchItem = AnsiConsole.Ask<string>("请输入学生姓名或学号, 输入 exit 退出: ");
            if (searchItem is "exit")
                break;
            var result = await _jiaoWuServiceApi.SearchStudent(searchItem);
            foreach (var info in result)
            {
                AnsiConsole.WriteLine($"{info.Name}");
            }
        }
    }

    public async Task Authenticate()
    {
        await AnsiConsole.Status()
            .AutoRefresh(true)
            .StartAsync("正在尝试通过 Cas 系统认证 新版教务系统", async statusContext =>
            {
                statusContext.Spinner(Spinner.Known.Dots);
                _authenticated = await _jiaoWuServiceApi.AuthenticateByCas();
                if (!_authenticated)
                {
                    AnsiConsole.MarkupLine("[red]认证新版教务系统失败, 请尝试重新认证[/]");
                    return;
                }

                AnsiConsole.MarkupLine("[green]认证新版教务系统成功[/]");
            });
    }

    [GeneratedRegex(
        @"<span onmouseover='kbtc\(this\)' onmouseout='kbot\(this\)' class='box' style='[^']*'><p>[^<]*</p><p>([^<]*)</p><span class='text'>([^<]*)</span></span><div class='item-box' ><p>(\S*)</p><div class='tch-name'><span>(\S*)</span><span>([^<]*)</span></div><div><span><img src='/jsxsd/assets_v1/images/item1.png'>([^<]*)</span>")]
    private static partial Regex ClassRawInfoRegex();
}