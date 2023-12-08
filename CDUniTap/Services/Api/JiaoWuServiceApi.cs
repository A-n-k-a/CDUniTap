using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CDUniTap.Interfaces;
using Spectre.Console;

namespace CDUniTap.Services.Api;

public class JiaoWuServiceApi : IHttpApiServiceBase, ICasAuthenticatedApi
{
    private readonly HttpClient _httpClient;
    private readonly CasServiceApi _casServiceApi;

    public JiaoWuServiceApi(HttpClient httpClient, CasServiceApi casServiceApi)
    {
        _httpClient = httpClient;
        _casServiceApi = casServiceApi;
    }

    public async Task<bool> AuthenticateByCas()
    {
        var toCasAuth = await _httpClient.GetAsync("https://jw.cdut.edu.cn/sso/login.jsp");
        if (toCasAuth.StatusCode != HttpStatusCode.Found) return false;
        var curJumpLink = toCasAuth.Headers.Location;
        int deffer = 10;
        while (deffer-- > 0)
        {
            var res = await _httpClient.GetAsync(curJumpLink ?? throw new Exception("返回未在预期"));
            if (res.Headers.Location is not null)
                curJumpLink = res.Headers.Location;
            else
                break;
        }

        if (deffer <= 0) return false;
        return true;
    }

    public async Task<string> GetNewWeekScheduleRaw(string sjms,string xqid, string weekId)
    {
        return await _httpClient.GetStringAsync(
            $"https://jw.cdut.edu.cn/jsxsd/framework/mainV_index_loadkb.htmlx?rq={weekId}&sjmsValue={sjms}&xnxqid={xqid}&xswk=true");
    }
    
    public async Task<CurriculumPreRequestInfo> GetMyCurriculumPreRequestInfo()
    {
        // Get sjmsValue
        var ans = new CurriculumPreRequestInfo();
        var sjmsResult = await _httpClient.GetStringAsync("https://jw.cdut.edu.cn/jsxsd/framework/xsMainV_new.htmlx?t1=1");
        ans.SjmsValue = Regex.Match(sjmsResult, @"data-value\=""(.*)"" name=""kbjcmsid""").Groups[1].Value;
        ans.Xqids
            = Regex.Matches(sjmsResult, @"<option value="""">([\d-]*)</option>").ToList().Select(t=>t.Groups[1].Value).ToList();
        var matchedWeeks = Regex.Matches(sjmsResult, @"<option value=""([\d-]+)""\s*\S*>(.*)</option>").ToList();
        foreach (var matchedWeek in matchedWeeks)
        {
            ans.AvalableWeeks[matchedWeek.Groups[2].Value] = matchedWeek.Groups[1].Value;
        }

        return ans;
    }

    public class CurriculumPreRequestInfo
    {
        public string SjmsValue { get; set; }
        public List<string> Xqids { get; set; }
        public Dictionary<string, string> AvalableWeeks = new Dictionary<string, string>();
    }
    
    public async Task<List<StudentInfo>> SearchStudent(string student)
    {
        var result = await (await _httpClient.PostAsync("https://jw.cdut.edu.cn/jsxsd/xskb/cxxs",
            new FormUrlEncodedContent(new Dictionary<string, string>()
            {
                { "xsmc", student },
                { "maxRow", "100" }
            }))).Content.ReadFromJsonAsync<StudentSearchResponse>();
        var res = new List<StudentInfo>();
        if (result?.Result is true)
        {
            foreach (var infoList in result.List)
            {
                res.Add(new StudentInfo
                {
                    Id = infoList.Xh,
                    Name = infoList.Xsmc
                });
            }
        }

        return res;
    }

    
    
    public partial class StudentSearchResponse
    {
        [JsonPropertyName("result")] public bool Result { get; set; }

        [JsonPropertyName("list")] public InfoList[] List { get; set; }

        public partial class InfoList
        {
            [JsonPropertyName("xh")] public string Xh { get; set; }


            [JsonPropertyName("xsmc")] public string Xsmc { get; set; }
        }
    }


    public class StudentInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}