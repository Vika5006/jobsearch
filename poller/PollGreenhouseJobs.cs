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
    }

    public class PollerConfiguration
    {
        public required string SmtpHost { get; set; }
        public required string SmtpPort { get; set; }
        public required string SmtpUser { get; set; }
        public required string SmtpPass { get; set; }
        public required string EmailFrom { get; set; }
        public required string EmailTo { get; set; }
        public required string TwilioSid { get; set; }
        public required string TwilioAuth { get; set; }
        public required string TwilioNumber { get; set; }
        public required string MyPhone { get; set; }
        public required string VoiceCallbackUrl { get; set; }
        public required string CompanyTokens { get; set; }
        public required string Keywords { get; set; }
    }

    public class PollGreenhouseJobs
    {
        public PollGreenhouseJobs(IConfiguration config, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PollGreenhouseJobs>();
            IConfiguration _config = config ?? throw new ArgumentNullException(nameof(config));
            _pollerConfig = InitializePollerConfiguration(_config);
        }

        private PollerConfiguration InitializePollerConfiguration(IConfiguration config)
        {
            #pragma warning disable CS8601 // Possible null reference assignment.
            var pollerConfig = new PollerConfiguration
            {
                SmtpHost = config["SMTP_HOST"],
                SmtpPort = config["SMTP_PORT"],
                SmtpUser = config["SMTP_USER"],
                SmtpPass = config["SMTP_PASS"],
                EmailFrom = config["EMAIL_FROM"],
                EmailTo = config["EMAIL_TO"],
                TwilioSid = config["TWILIO_SID"],
                TwilioAuth = config["TWILIO_AUTH"],
                TwilioNumber = config["TWILIO_NUMBER"],
                MyPhone = config["MY_PHONE"],
                VoiceCallbackUrl = config["VOICE_CALLBACK_URL"],
                CompanyTokens = config["COMPANY_TOKENS"],
                Keywords = config["KEYWORDS"]
            };
#pragma warning restore CS8601 // Possible null reference assignment.
            return pollerConfig;
        }
        
        private bool IsJobRecent(DateTime publishedDate)
        {
            // Convert DateTime.UtcNow.AddMinutes(-60) to CDT, MDT, EDT, PDT
            var utcNow = DateTime.UtcNow;
            var adjustedUtcNow = utcNow.AddMinutes(-360);

            var cstZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
            var mstZone = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");
            var estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            var pstZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

            var adjustedCst = TimeZoneInfo.ConvertTimeFromUtc(adjustedUtcNow, cstZone);
            var adjustedMst = TimeZoneInfo.ConvertTimeFromUtc(adjustedUtcNow, mstZone);
            var adjustedEst = TimeZoneInfo.ConvertTimeFromUtc(adjustedUtcNow, estZone);
            var adjustedPst = TimeZoneInfo.ConvertTimeFromUtc(adjustedUtcNow, pstZone);

            return publishedDate >= adjustedCst || publishedDate >= adjustedMst || publishedDate >= adjustedEst || publishedDate >= adjustedPst;
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

                var validDate = DateTime.TryParse(publishedStr, out var publishedDate);
                if (validDate && IsJobRecent(publishedDate)
                    && keywords.Any(k => title.Contains(k, StringComparison.OrdinalIgnoreCase))
                    && IsUnitedStates(location))
                {
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

        private static bool IsUnitedStates(string location)
        {
            return location.Contains("United States", StringComparison.OrdinalIgnoreCase) || location.Contains("US", StringComparison.OrdinalIgnoreCase);
        }

        public async Task SendEmailAsync(string subject, string body)
        {
            using var smtpClient = new SmtpClient(_pollerConfig.SmtpHost)
            {
                Port = int.Parse(_pollerConfig.SmtpPort),
                Credentials = new NetworkCredential(_pollerConfig.SmtpUser, _pollerConfig.SmtpPass),
                EnableSsl = true
            };

            var mail = new MailMessage
            {
                From = new MailAddress(_pollerConfig.EmailFrom),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            mail.To.Add(_pollerConfig.EmailTo);

            await smtpClient.SendMailAsync(mail);
        }

        public async Task PlaceVoiceCallAsync()
        {
            TwilioClient.Init(_pollerConfig.TwilioSid, _pollerConfig.TwilioAuth);
            var callOptions = new CreateCallOptions(
                to: new PhoneNumber(_pollerConfig.MyPhone),
                from: new PhoneNumber(_pollerConfig.TwilioNumber)
            )
            {
                MachineDetection = "Enable",
                Url = new Uri(_pollerConfig.VoiceCallbackUrl),
                Timeout = 30
            };

            await CallResource.CreateAsync(callOptions);
        }

        [Function("PollGreenhouseJobs")]
        public async Task RunPollGreenhouseJobs([TimerTrigger("0 */30 * * * *")] TimerInfo timer)
        {
            var tokens = _pollerConfig.CompanyTokens.Split(",", StringSplitOptions.RemoveEmptyEntries);
            var keywords = _pollerConfig.Keywords.Split(",", StringSplitOptions.RemoveEmptyEntries);

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
                // do not call me at my night
                if (jobs.Any() && utcHour >= 13 && utcHour <= 23)
                {
                    await PlaceVoiceCallAsync();
                }
            }
        }
        private readonly ILogger<PollGreenhouseJobs> _logger;
        private readonly PollerConfiguration _pollerConfig;
    }
}