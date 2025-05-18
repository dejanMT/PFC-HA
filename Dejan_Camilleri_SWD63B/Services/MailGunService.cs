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
        private readonly HttpClient _http;
        private readonly string _from;
        public MailGunService(IConfiguration cfg)
        {
            var apiKey = cfg["Mailgun:ApiKey"];
            var domain = cfg["Mailgun:Domain"];
            _from = $"postmaster@{domain}";
            _http = new HttpClient
            {
                BaseAddress = new Uri($"https://api.mailgun.net/v3/{domain}/")
            };
            var auth = Convert.ToBase64String(
              Encoding.ASCII.GetBytes($"api:{apiKey}")
            );
            _http.DefaultRequestHeaders.Authorization =
              new AuthenticationHeaderValue("Basic", auth);
        }

        public async Task SendEmailAsync(string to, string subject, string text)
        {
            var form = new Dictionary<string, string>
            {
                ["from"] = _from,
                ["to"] = to,
                ["subject"] = subject,
                ["text"] = text
            };
            var res = await _http.PostAsync(
              "messages",
              new FormUrlEncodedContent(form)
            );
            res.EnsureSuccessStatusCode();
        }
    }
}
