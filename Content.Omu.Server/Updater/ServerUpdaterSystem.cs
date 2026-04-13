using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Content.Server._EinsteinEngines.GameTicking;
using Robust.Shared.Configuration;
using Content.Omu.Common.CCVar;
using Microsoft.VisualBasic;

/*
    Realisticly this system shouldn't exist, but its the only way I thought of to ensure the server updates properly.
*/


namespace Content.Omu.Server.ServerUpdaterSystem;

public sealed class ServerUpdaterSystem : EntitySystem
{


    [Dependency] private readonly IConfigurationManager _cfg = default!;
    private ISawmill _sawmill = default!;

    private string? serverId;
    private bool updaterEnabled = false;

    private string pannelurl = _cfg.GetCVar(OmuCVars.ServerUpdaterPanelUrl);
    private string apiKey = _cfg.GetCVar(OmuCVars.ServerUpdaterApiKey);

    private static readonly HttpClient client = CreateHttpClient();

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("updater");

        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnded);

        _cfg.OnValueChanged(OmuCVars.ServerUpdaterServerId, id => serverId = id, true);
        _cfg.OnValueChanged(OmuCVars.ServerUpdaterEnabled, enabled => updaterEnabled = enabled, true);
    }

    private void OnRoundEnded(RoundEndedEvent args)
    {
        if (!updaterEnabled)
            return;
        if (string.IsNullOrEmpty(serverId))
        {
            _sawmill.Warning("Server ID not set, skipping restarting updater.");
            return;
        }
        if (string.IsNullOrEmpty(pannelurl))
        {
            _sawmill.Warning("Panel URL not set, skipping restarting updater.");
            return;
        }
        if (string.IsNullOrEmpty(apiKey))
        {
            _sawmill.Warning("API key not set, skipping restarting updater.");
            return;
        }

        await RestartServerAsync(serverId);
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient()
        {
            BaseAddress = new Uri(pannelurl)
        };

        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        httpClient.DefaultRequestHeaders.Add("Accept", "Application/vnd.pterodactyl.v1+json");

        return httpClient;
    }

    public static async Task RestartServerAsync(string serverId)
    {
        var endpoint = $"api/client/servers/{serverId}/power";

        var json = "{ \"signal\": \"restart\" }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync(endpoint, content);

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                _sawmill.Info("Updater restart initiated");
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                _sawmill.Error($"Request failed: {response.StatusCode}");
                _sawmill.Error($"Response body: {body}");
            }
        }
        catch (HttpRequestException ex)
        {
            _sawmill.Error($"HTTP request error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            _sawmill.Error("Request timed out");
        }
    }
}
