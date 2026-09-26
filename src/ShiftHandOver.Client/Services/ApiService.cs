using System;
using System.Net.Http;

namespace ShiftHandOver.Client.Services
{
    public static class ApiService
    {
        public const string BaseUrl = "http://localhost:5000/";

        public static readonly HttpClient Client = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}
