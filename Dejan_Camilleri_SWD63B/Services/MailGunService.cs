using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Dejan_Camilleri_SWD63B.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Dejan_Camilleri_SWD63B.Services
{
    public class MailGunService : IMailService
    {
        private readonly HttpClient _client;
        private readonly string _domain;

        public MailGunService(IConfiguration config)
        {
            var apiKey = config["MailGun:ApiKey"];
            _domain = config["MailGun:Domain"];
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(_domain))
                throw new ArgumentException("MailGun credentials not configured");

            _client = new HttpClient { BaseAddress = new Uri("https://api.mailgun.net/v3/") };
            var byteArray = Encoding.ASCII.GetBytes($"api:{apiKey}");
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var form = new Dictionary<string, string>
            {
                ["from"] = $"no-reply@{_domain}",
                ["to"] = to,
                ["subject"] = subject,
                ["text"] = body
            };
            using var content = new FormUrlEncodedContent(form);
            using var response = await _client.PostAsync($"{_domain}/messages", content);
            response.EnsureSuccessStatusCode();
        }
    }
}
