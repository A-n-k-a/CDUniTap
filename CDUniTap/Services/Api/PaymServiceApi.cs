using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using CDUniTap.Interfaces;
using Spectre.Console;

namespace CDUniTap.Services.Api;

public partial class PaymServiceApi : IHttpApiServiceBase
{
    private readonly HttpClient _httpClient;
    private readonly CasServiceApi _casService;
    private const string CasServiceUrl = "http://paym.cdut.edu.cn/casLogin/";

    private bool _authenticated = false;
    private string? _xToken;

    public PaymServiceApi(HttpClient httpClient, CasServiceApi casService)
    {
        _httpClient = httpClient;
        _casService = casService;
    }

    public async Task<bool> AuthenticateByCas()
    {
        var callbackLink = await _casService.AuthenticateService(CasServiceUrl);
        if (callbackLink?.Contains("ticket") is not true)
            return false;
        var ticketResponse = await _httpClient.GetAsync(callbackLink);
        var secondRet = ticketResponse.Headers.Location?.ToString();
        if (ticketResponse.StatusCode != HttpStatusCode.Found || secondRet is null) return false;
        var actualLoginResponse = await _httpClient.GetAsync(secondRet);
        if (actualLoginResponse.IsSuccessStatusCode == false) return false;
        var actualLoginContent = await actualLoginResponse.Content.ReadAsStringAsync();
        var resultLink = ActualLoginRegex().Match(actualLoginContent).Groups[1].Value;
        var tokenResponse = await _httpClient.GetAsync(resultLink);
        var tokenLink = tokenResponse.Headers.Location?.ToString();
        if (tokenResponse.StatusCode != HttpStatusCode.Found || tokenLink is null) return false;
        var queries = HttpUtility.ParseQueryString(tokenLink.Substring(tokenLink.IndexOf("?", StringComparison.Ordinal)+1));
        _authenticated = true;
        _xToken = queries["token"];
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Token", _xToken);
        return true;
    }

    public async Task<UserInfoDto?> GetUserInfo()
    {
        var result = await _httpClient.GetFromJsonAsync<DataResponse<UserInfoDto>>($"http://paym.cdut.edu.cn/api/pay/queryUserInfo/{_xToken}");
        return result?.Data;
        
    }
    
    public async Task<List<ProjectDto>?> GetAllProjects()
    {
        var result = await _httpClient.GetFromJsonAsync<DataResponse<List<ProjectDto>>>("http://paym.cdut.edu.cn/api/pay/project/getAllProjectList");
        return result?.Data;
        
    }
    
    [GeneratedRegex("window.location.href = \\\"(.*)\\\";")]
    private static partial Regex ActualLoginRegex();
}

public class DataResponse<T>
{
    [JsonPropertyName("data")] public required T Data { get; set; }
}

public class UserInfoDto
{
    [JsonPropertyName("id")] public required string Id { get; set; }
    [JsonPropertyName("idserial")] public required string StudentId { get; set; }
    [JsonPropertyName("name")] public required string Name { get; set; }
    [JsonPropertyName("sex")] public required string Sex { get; set; }
}


public class ProjectDto
{
    [JsonPropertyName("id")] public required string Id { get; set; }
    [JsonPropertyName("projectName")] public required string Name { get; set; }
}