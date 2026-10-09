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
            // Cấu hình kết nối server cục bộ (Local):
            // const string defaultUrl = "http://localhost:5000/";

            // Địa chỉ server đang kết nối từ xa (Remote Server):
            const string defaultUrl = "http://100.119.81.91:5000/";
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

        public static System.Windows.Media.ImageSource? GetImageSource(string? pathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl)) return null;
            try
            {
                string fullUrl = pathOrUrl;
                if (!pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                    !pathOrUrl.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
                {
                    if (pathOrUrl.StartsWith("/")) pathOrUrl = pathOrUrl.Substring(1);
                    fullUrl = BaseUrl + pathOrUrl;
                }

                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(fullUrl, UriKind.RelativeOrAbsolute);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public static async System.Threading.Tasks.Task<string?> UploadImageAsync(string localFilePath)
        {
            if (!File.Exists(localFilePath)) return null;
            try
            {
                using var form = new MultipartFormDataContent();
                using var fileStream = File.OpenRead(localFilePath);
                using var fileContent = new StreamContent(fileStream);
                var ext = Path.GetExtension(localFilePath).ToLowerInvariant();
                var mediaType = ext switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".webp" => "image/webp",
                    ".gif" => "image/gif",
                    _ => "application/octet-stream"
                };
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
                form.Add(fileContent, "file", Path.GetFileName(localFilePath));

                var res = await Client.PostAsync("api/Branch/upload-image", form);
                if (res.IsSuccessStatusCode)
                {
                    var content = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("url", out var urlProp))
                    {
                        return urlProp.GetString();
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
