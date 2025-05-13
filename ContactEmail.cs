using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker; 
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace BrnBrns.Function
{
    public class ContactEmail
    {
        private readonly ILogger<ContactEmail> _logger;

        public ContactEmail(ILogger<ContactEmail> logger)
        {
            _logger = logger;
        }

        [Function("ContactEmail")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req)
        {
            _logger.LogInformation("ContactEmail received new request");

            // Get posted req object
            string content = await new StreamReader(req.Body).ReadToEndAsync();
            PostData data = JsonConvert.DeserializeObject<PostData>(content);

            // Check for required input
            if (data == null || string.IsNullOrEmpty(data.name) || string.IsNullOrEmpty(data.email) || string.IsNullOrEmpty(data.message)) {
                return new BadRequestObjectResult("Please pass name, email, and message in the JSON request body");
            }

            // To, from emails
            string toEmail = GetEnvironmentVariable("SmtpToEmail");
            string toName = GetEnvironmentVariable("SmtpToName");
            string fromEmail = GetEnvironmentVariable("SmtpFromEmail");
            string fromName = GetEnvironmentVariable("SmtpFromName");
            
            // Create email subject and body
            string subject = $"New Message from {data.name}";
            string body = $"<p>Hello,</p><p><b>{data.name}</b> has sent you a message:</p><p>{data.message}</p><p>--</p><p>Beep boop,</p><p>Azure Bot</p>";

            // Create SendGrid mail client
            string apiKey = GetEnvironmentVariable("SENDGRID_API");
            SendGridClient client = new SendGridClient(apiKey);

            // Create mail message
            SendGridMessage message = new SendGridMessage();
            message.SetSubject(subject);
            message.AddContent(MimeType.Html, body);

            message.SetFrom(new EmailAddress(fromEmail, fromName));
            message.SetReplyTo(new EmailAddress(data.email, data.name));

            List<EmailAddress> toEmails = new List<EmailAddress>
            {
                new EmailAddress(toEmail, toName)
            };
            message.AddTos(toEmails);

            // Send message
            try
            {
                Response response = await client.SendEmailAsync(message);
                if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    _logger.LogInformation($"ContactEmail successfully sent email");
                }
                else
                {
                    _logger.LogError($"ContactEmail FAILED: {response.StatusCode}: {response.Body}");
                    return new OkObjectResult("ContactEmail failed to send email!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"ContactEmail ERROR: {ex.ToString()}");
                return new OkObjectResult("ContactEmail failed to send email!");
            }

            return new OkObjectResult("ContactEmail completed successfully");
        }

        public static string GetEnvironmentVariable(string name)
        {
            return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }
    }

    public class PostData
    {
        public string name { get; set; }
        public string email { get; set; }
        public string message { get; set; }
    }
}
