using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace ShiftHandOver.Client.Services
{
    public static class ApiService
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_config.json");

        public static string BaseUrl { get; } = LoadServerUrl();

        public static readonly HttpClient Client = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static string LoadServerUrl()
        {
            const string defaultUrl = "http://localhost:5000/";
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("ServerUrl", out var prop))
                    {
                        string? url = prop.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(url))
                        {
                            if (!url.EndsWith("/")) url += "/";
                            return url;
                        }
                    }
                }
                else
                {
                    var configObj = new { ServerUrl = defaultUrl };
                    File.WriteAllText(ConfigPath, JsonSerializer.Serialize(configObj, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch
            {
                // Fallback to default
            }

            return defaultUrl;
        }
    }
}
