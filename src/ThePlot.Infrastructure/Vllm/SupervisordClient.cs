using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ThePlot.Infrastructure.Vllm;

public interface ISupervisordClient
{
    Task StartProcessAsync(string name, CancellationToken ct = default);
    Task StopProcessAsync(string name, CancellationToken ct = default);
    Task<string> GetProcessStateAsync(string name, CancellationToken ct = default);
}

/// <summary>
/// Minimal XML-RPC client for supervisord's <c>inet_http_server</c>.
/// </summary>
public sealed class SupervisordClient(
    HttpClient httpClient,
    IOptions<VllmOptions> options,
    ILogger<SupervisordClient> logger) : ISupervisordClient
{
    private const int FaultAlreadyStarted = 60;
    private const int FaultNotRunning = 70;

    public async Task StartProcessAsync(string name, CancellationToken ct = default)
    {
        try
        {
            await CallAsync("supervisor.startProcess", [name, true], ct);
            logger.LogInformation("Supervisord: started process '{Name}'.", name);
        }
        catch (SupervisordFaultException ex) when (ex.FaultCode == FaultAlreadyStarted)
        {
            logger.LogDebug("Supervisord: process '{Name}' already running.", name);
        }
    }

    public async Task StopProcessAsync(string name, CancellationToken ct = default)
    {
        try
        {
            await CallAsync("supervisor.stopProcess", [name, true], ct);
            logger.LogInformation("Supervisord: stopped process '{Name}'.", name);
        }
        catch (SupervisordFaultException ex) when (ex.FaultCode == FaultNotRunning)
        {
            logger.LogDebug("Supervisord: process '{Name}' already stopped.", name);
        }
    }

    public async Task<string> GetProcessStateAsync(string name, CancellationToken ct = default)
    {
        var xml = await CallAsync("supervisor.getProcessInfo", [name], ct);
        var doc = XDocument.Parse(xml);
        var members = doc.Descendants("member");
        foreach (var member in members)
        {
            if (member.Element("name")?.Value == "statename")
            {
                return member.Element("value")?.Value
                       ?? member.Descendants("string").FirstOrDefault()?.Value
                       ?? "UNKNOWN";
            }
        }

        return "UNKNOWN";
    }

    private async Task<string> CallAsync(string method, object[] parameters, CancellationToken ct)
    {
        var url = options.Value.SupervisordUrl.TrimEnd('/');
        var body = BuildRequestXml(method, parameters);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/RPC2")
        {
            Content = new StringContent(body, Encoding.UTF8, "text/xml")
        };

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        var doc = XDocument.Parse(responseBody);
        var fault = doc.Descendants("fault").FirstOrDefault();
        if (fault is not null)
        {
            var faultCode = 0;
            var faultString = "";
            foreach (var member in fault.Descendants("member"))
            {
                var name = member.Element("name")?.Value;
                if (name == "faultCode")
                    faultCode = int.Parse(
                        member.Descendants("int").FirstOrDefault()?.Value
                        ?? member.Descendants("i4").FirstOrDefault()?.Value
                        ?? "0");
                else if (name == "faultString")
                    faultString = member.Descendants("string").FirstOrDefault()?.Value ?? "";
            }

            throw new SupervisordFaultException(faultCode, faultString);
        }

        return responseBody;
    }

    private static string BuildRequestXml(string method, object[] parameters)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\"?><methodCall><methodName>")
            .Append(method)
            .Append("</methodName><params>");

        foreach (var p in parameters)
        {
            sb.Append("<param><value>");
            switch (p)
            {
                case string s:
                    sb.Append("<string>").Append(s).Append("</string>");
                    break;
                case bool b:
                    sb.Append("<boolean>").Append(b ? "1" : "0").Append("</boolean>");
                    break;
                case int i:
                    sb.Append("<int>").Append(i).Append("</int>");
                    break;
                default:
                    sb.Append("<string>").Append(p).Append("</string>");
                    break;
            }

            sb.Append("</value></param>");
        }

        sb.Append("</params></methodCall>");
        return sb.ToString();
    }
}

public sealed class SupervisordFaultException(int faultCode, string faultString)
    : Exception($"Supervisord fault {faultCode}: {faultString}")
{
    public int FaultCode { get; } = faultCode;
    public string FaultString { get; } = faultString;
}
