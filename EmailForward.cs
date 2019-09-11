using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace BrnBrns.Function
{
    public static class EmailForward
    {
        [FunctionName("EmailForward")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("EmailForward received new email");

            var data = await req.ReadFormAsync();
            foreach (KeyValuePair<string, StringValues> datum in data)
            {
                log.LogInformation($"{datum.Key}:\n{data[datum.Key]}");
            }

            // To, from emails
            string toEmail = GetEnvironmentVariable("SmtpToEmail");
            string toName = GetEnvironmentVariable("SmtpToName");
            string fromEmail = GetEnvironmentVariable("SmtpFromEmail");
            string fromName = GetEnvironmentVariable("SmtpFromName");

            // Create SendGrid mail client
            string apiKey = GetEnvironmentVariable("SENDGRID_API");
            SendGridClient client = new SendGridClient(apiKey);

            // Create mail message
            string subject = data["subject"];
            string body = data["html"];

            SendGridMessage message = new SendGridMessage();
            message.SetFrom(new EmailAddress(fromEmail, fromName));
            Envelope envelope = JsonConvert.DeserializeObject<Envelope>(data["envelope"]);
            string[] fromTitle = data["from"].ToString().Split(" ");
            string replyName = fromTitle.Length > 2 ? fromTitle[0] + " " + fromTitle[1] : fromTitle[0];
            message.SetReplyTo(new EmailAddress(envelope.From, replyName));

            body += "<br><br>(Message originally sent to: ";
            for (int i=0; i<envelope.To.Length; i++)
            {
                body += i == envelope.To.Length-1 ? $"{envelope.To[i]})" : $"{envelope.To[i]}, ";
            }
            
            message.SetSubject(subject);
            message.AddContent(MimeType.Html, body);

            List<EmailAddress> toEmails = new List<EmailAddress>
            {
                new EmailAddress(toEmail, toName)
            };
            message.AddTos(toEmails);

            // Send message
            try
            {
                Response response = await client.SendEmailAsync(message);

                if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
                {
                    log.LogError($"EmailForward FAILED: {response.StatusCode}: {response.Body}");
                    return new OkObjectResult("EmailForward failed to send email!");
                }
            }
            catch (Exception ex)
            {
                log.LogError($"FowardEmail ERROR: {ex.ToString()}");
                return new OkObjectResult("EmailForward failed to send email!");
            }

            return new OkObjectResult("EmailForward successfully forwarded email");
        }

        public static string GetEnvironmentVariable(string name)
        {
            return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }
    }

    public class Envelope
    {
        public string[] To { get; set; }
        public string From { get; set; }
    }
}
