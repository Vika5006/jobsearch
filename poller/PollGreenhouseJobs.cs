using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace JobSearch
{
    public class JobPosting
    {
        public long Id { get; set; }
        public required string Title { get; set; }
        public required string AbsoluteUrl { get; set; }
        public DateTime? PublishedAt { get; set; }
        public required string Location { get; set; }
    };

    public class PollGreenhouseJobs
    {
        public PollGreenhouseJobs(IConfiguration config, ILogger<PollGreenhouseJobs> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }
        public async Task<List<JobPosting>> PollCompanyJobsAsync(string companyToken, string[] keywords)
        {
            using var client = new HttpClient();
            string url = $"https://boards-api.greenhouse.io/v1/boards/{companyToken}/jobs";

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            var doc = JsonDocument.Parse(content);
            var jobs = new List<JobPosting>();

            foreach (var job in doc.RootElement.GetProperty("jobs").EnumerateArray())
            {
                var title = job.GetProperty("title").GetString();
                var urlPath = job.GetProperty("absolute_url").GetString();
                var publishedStr = job.TryGetProperty("updated_at", out var pubVal) ? pubVal.GetString() : null;
                var location = job.TryGetProperty("location", out var locVal) ? Convert.ToString(locVal) : null;
                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(urlPath) || string.IsNullOrEmpty(publishedStr) || string.IsNullOrEmpty(location))
                    continue;
                // Filter jobs published in the last 30 minutes
                var validDate = DateTime.TryParse(publishedStr, out var publishedDate);
                if (validDate && publishedDate >= DateTime.UtcNow.AddMinutes(-30)
                    && keywords.Any(k => title.Contains(k, StringComparison.OrdinalIgnoreCase))
                    && (location.Contains("United States", StringComparison.OrdinalIgnoreCase) || location.Contains("US", StringComparison.OrdinalIgnoreCase)))                {
                    jobs.Add(new JobPosting
                    {
                        Id = job.GetProperty("id").GetInt64(),
                        Title = title,
                        AbsoluteUrl = urlPath,
                        PublishedAt = publishedDate,
                        Location = location
                    });
                }
            }

            return jobs;
        }

        public async Task SendEmailAsync(string subject, string body)
        {
            using var smtpClient = new SmtpClient(_config["SMTP_HOST"])
            {
                Port = int.Parse(_config["SMTP_PORT"]),
                Credentials = new NetworkCredential(_config["SMTP_USER"], _config["SMTP_PASS"]),
                EnableSsl = true
            };

            var mail = new MailMessage
            {
                From = new MailAddress(_config["EMAIL_FROM"]),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            mail.To.Add(_config["EMAIL_TO"]);

            await smtpClient.SendMailAsync(mail);
        }

        public async Task PlaceVoiceCallAsync()
        {
            TwilioClient.Init(_config["TWILIO_SID"], _config["TWILIO_AUTH"]);
            var voiceCallbackUrl = _config["VOICE_CALLBACK_URL"];

            var callOptions = new CreateCallOptions(
                to: new PhoneNumber(_config["MY_PHONE"]),
                from: new PhoneNumber(_config["TWILIO_NUMBER"])
            )
            {
                MachineDetection = "Enable",
                // TODO: should remove, i don't really need this to clutter my voicemail. Keep the timeout
                Url = new Uri(voiceCallbackUrl),
                Timeout = 10
            };

            await CallResource.CreateAsync(callOptions);
        }

        [Function("PollGreenhouseJobs")]
        public async Task RunPollGreenhouseJobs([TimerTrigger("0 */30 * * * *")] TimerInfo timer)
        {
            var tokens = _config["COMPANY_TOKENS"].Split(",", StringSplitOptions.RemoveEmptyEntries);
            var keywords = _config["KEYWORDS"].Split(",", StringSplitOptions.RemoveEmptyEntries);

            foreach (var token in tokens)
            {
                var jobs = await PollCompanyJobsAsync(token, keywords);
                foreach (var job in jobs)
                {
                    var body = $"New job found at {token}:\n\n{job.Title}\n{job.AbsoluteUrl}";
                    await SendEmailAsync($"📢 New Job Alert: {job.Title}", body);
                }

                if (jobs.Any())
                {
                    _logger.LogInformation($"Found {jobs.Count} new jobs for {token}.");
                }
                else
                {
                    _logger.LogInformation($"No new jobs found for {token}.");
                }

                var utcHour = DateTime.UtcNow.Hour;
                if (jobs.Any() && utcHour >= 13 && utcHour <= 23)
                {
                    await PlaceVoiceCallAsync();
                }
            }
        }
        private readonly IConfiguration _config;
        private readonly ILogger<PollGreenhouseJobs> _logger;
    }
}