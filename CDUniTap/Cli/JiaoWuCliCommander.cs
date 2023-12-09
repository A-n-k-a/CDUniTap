using System.Text.RegularExpressions;
using CDUniTap.Interfaces.Markers;
using CDUniTap.Services.Api;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Spectre.Console;
using Calendar = Ical.Net.Calendar;
using CalendarEvent = Ical.Net.CalendarComponents.CalendarEvent;

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
        selection.AddChoice("全部");
        foreach (var (weekName, _) in info?.AvalableWeeks ?? throw new Exception("获取课表架构失败"))
        {
            selection.AddChoice(weekName);
        }

        var selected = AnsiConsole.Prompt(selection);
        var classes = new List<ClassInfo>();
        var weeks = new List<string>();
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("正在获取", async context =>
            {
                if (selected == "全部")
                {
                    weeks.AddRange(info.AvalableWeeks.Keys);
                }
                else
                {
                    weeks.Add(selected);
                }

                foreach (var week in weeks)
                {
                    var rawResult =
                        await _jiaoWuServiceApi.GetNewWeekScheduleRaw(info.SjmsValue, info.Xqids[0],
                            info.AvalableWeeks[week]);
                    classes.AddRange(ParseClassInfo(rawResult, DateOnly.Parse(info.AvalableWeeks[week])));
                }

            });
        
        var actionSelection = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("请选择操作")
            .AddChoices("显示课表")
            .AddChoices("导出为 ics"));
        switch (actionSelection)
        {
            case "显示课表":
                DisplayClassInfos(classes);
                break;
            case "导出为 ics":
                ExportToICS(classes);
                break;
        }
    }

    public static Dictionary<int, (TimeOnly, TimeOnly)> Timetable =
        new()
        {
            { 0, (TimeOnly.Parse("08:10"), TimeOnly.Parse("09:45")) },
            { 1, (TimeOnly.Parse("10:15"), TimeOnly.Parse("11:50")) },
            { 2, (TimeOnly.Parse("13:00"), TimeOnly.Parse("14:00")) },
            { 3, (TimeOnly.Parse("14:30"), TimeOnly.Parse("16:05")) },
            { 4, (TimeOnly.Parse("16:25"), TimeOnly.Parse("18:00")) },
            { 5, (TimeOnly.Parse("19:10"), TimeOnly.Parse("20:45")) },
        };

    private void ExportToICS(List<ClassInfo> classInfos)
    {
        var calendar = new Calendar();
        foreach (var classInfo in classInfos)
        {
            var calEvent = new CalendarEvent
            {
                Summary = classInfo.ClassName,

                Description =
                    $"{classInfo.Teacher}\n{classInfo.Score}\n{classInfo.ClassWeek}\n{classInfo.ClassSchedule}",

                Start = new CalDateTime(classInfo.Date.ToDateTime(Timetable[classInfo.IndexInDay].Item1)),

                End = new CalDateTime(classInfo.Date.ToDateTime(Timetable[classInfo.IndexInDay].Item2)),

                Location = classInfo.Location,
            };
            calendar.Events.Add(calEvent);
        }

        var converter = new CalendarSerializer();
        var result = converter.SerializeToString(calendar);
        var fileName = DateTime.Now.ToString("yyyyMMddhhmmss") + ".ics";
        File.WriteAllText(fileName, result);
        AnsiConsole.WriteLine($"[green]成功导出到 [/]{Path.GetFullPath(fileName.EscapeMarkup())}");
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