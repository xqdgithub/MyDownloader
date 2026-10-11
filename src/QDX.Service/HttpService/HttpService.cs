using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using System.Text;
using System.Net.Http.Json;
using System.Reflection.PortableExecutable;
using System;
using System.Net.Http;

namespace QDX.Serivices.HttpService
{
    public class HttpService
    {
        public Dictionary<string, string> DefaultHeaders { get; set; } = new Dictionary<string, string>()
        {
            {"user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0"}
        };
        public async Task<T?> GetAsync<T>(string url, Dictionary<string, string>? parameters = null, Dictionary<string, string>? headers = null)where T : class
        {
            headers = headers ?? new Dictionary<string, string>();
            url = GetUrlWithParameters(url, parameters);
            foreach (var h in DefaultHeaders)
            {
                if (!headers.ContainsKey(h.Key))
                {
                    headers.Add(h.Key, h.Value);
                }
            }
            using HttpClient httpClient = GetHttpClient(headers);
            var req = await httpClient.GetStringAsync(url);
            if (typeof(T) == typeof(string))
            {
                return req as T;
            }
            var res = JsonSerializer.Deserialize<T>(req);
            return res;
        }

        private HttpClient GetHttpClient(Dictionary<string, string>? headers = null)
        {
            headers = headers ?? new Dictionary<string, string>();
            var httpClient = new HttpClient();
            httpClient.Timeout = Timeout.InfiniteTimeSpan;
            foreach (var header in headers)
            {
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
            return httpClient;
        }
        private string GetUrlWithParameters(string url, Dictionary<string, string>? parameters)
        {
            parameters = parameters ?? new();
            StringBuilder stringBuilder = new StringBuilder();
            if (parameters.Count > 0)
            {
                foreach (var param in parameters)
                {
                    stringBuilder.Append(param.Key);
                    stringBuilder.Append("=");
                    stringBuilder.Append(param.Value);
                    stringBuilder.Append("&");
                }
                url = $"{url}?{stringBuilder.ToString().TrimEnd('&')}";
            }
            return url;
        }
        public async Task<T?> PostAsync<T>(string url, HttpContent content, Dictionary<string, string>? headers = null, Dictionary<string, string>? parameters = null) where T:class
        {
            var rst = await StringPostAsync(url, content, parameters, headers);
            if(typeof(T)==typeof(string))
            {
                return rst as T;
            }
            else
            {
                var res = JsonSerializer.Deserialize<T>(rst);
                return res;
            }
            
            
        }

        public async Task<string> StringPostAsync(string url, HttpContent content, Dictionary<string, string>? headers = null, Dictionary<string, string>? parameters = null)
        {
            parameters = parameters ?? new();
            headers = headers ?? new Dictionary<string, string>();
            url = GetUrlWithParameters(url, parameters);
            foreach (var h in DefaultHeaders)
            {
                if (!headers.ContainsKey(h.Key))
                {
                    headers.Add(h.Key, h.Value);
                }
            }
            using HttpClient httpClient = GetHttpClient(headers);
            var httpResponseMessageTask = await httpClient.PostAsync(url, content);
            var rst = await httpResponseMessageTask.Content.ReadAsStringAsync();
            return rst;
        }

        public async Task<Tout?> PostJsonAsync<Tin, Tout>(string url, Tin content, Dictionary<string, string>? headers = null, Dictionary<string, string>? parameters = null) where Tout : class
        {
            
            var jsonContent = JsonContent.Create(content, new MediaTypeHeaderValue("application/json"));
            return await PostAsync<Tout>(url, jsonContent, parameters, headers);
        }
        public async Task<Tout?> PostStringAsync<Tout>(string url, string strContent, Dictionary<string, string>? headers = null, Dictionary<string, string>? parameters = null) where Tout : class
        {
            var jsonContent = new StringContent(strContent,encoding:Encoding.UTF8, "application/json");
            return await PostAsync<Tout>(url, jsonContent, parameters, headers);
        }
    }
}
