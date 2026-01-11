using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace BackendSGH.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var smtpHost = _configuration["Email:Host"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["Email:Port"] ?? "587");
            var smtpUser = _configuration["Email:Username"];
            var smtpPass = _configuration["Email:Password"];
            var fromEmail = _configuration["Email:From"] ?? smtpUser;

            if (string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPass))
            {
                // Fallback for development if no creds: Log instead of crash
                Console.WriteLine($"[EmailService] MOCK SEND to {to}: {subject}");
                return;
            }

            // Clean password (remove spaces if copied from Google UI)
            if (!string.IsNullOrEmpty(smtpPass)) smtpPass = smtpPass.Replace(" ", "");

            using (var client = new SmtpClient(smtpHost, smtpPort))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(smtpUser, smtpPass);
                client.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(to);

                await client.SendMailAsync(mailMessage);
            }
        }
    }
}
