using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using StrongGrid;
using StrongGrid.Models;
using StrongGrid.Models.Webhooks;

namespace BrnBrns.Function
{
    public class EmailForward
    {
        private readonly ILogger<EmailForward> _logger;

        public EmailForward(ILogger<EmailForward> logger)
        {
            _logger = logger;
        }

        [Function("EmailForward")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req)
        {
            var parser = new WebhookParser();
            var inboundEmail = await parser.ParseInboundEmailWebhookAsync(req.Body);

            _logger.LogInformation($"EmailForward received new email from {inboundEmail.From.Email}");

            double score = 0.0;
            if (!string.IsNullOrEmpty(inboundEmail.SpamScore))
            {
                score = double.Parse(inboundEmail.SpamScore);
                if (score >= 5.0)
                {
                    _logger.LogInformation($"Discarding email due to spam score of {score}");
                    return new OkObjectResult("EmailForward discarded email due to spam!");    
                }
            }

            string response = await ForwardEmail(inboundEmail, score);
            if (string.IsNullOrEmpty(response))
            {
                _logger.LogError($"EmailForward FAILED");
                return new OkObjectResult("EmailForward failed to send email!");
            }

            _logger.LogInformation($"EmailForward successfully forwarded email {response}");
            return new OkObjectResult($"EmailForward successfully forwarded email {response}");
        }

        private async Task<string> ForwardEmail(InboundEmail mail, double spamScore)
        {
            // To, from emails
            string toAddress = GetEnvironmentVariable("SmtpToEmail");
            string toName = GetEnvironmentVariable("SmtpToName");
            MailAddress toEmail = new MailAddress(toAddress, toName);

            string fromAddress = GetEnvironmentVariable("SmtpFromEmail");
            string fromName = GetEnvironmentVariable("SmtpFromName");
            MailAddress fromEmail = new MailAddress(fromAddress, fromName);

            // Create StrongGrid mail client
            string apiKey = GetEnvironmentVariable("SENDGRID_API");
            var client = new Client(apiKey);

            // Add spam report
            if (!string.IsNullOrEmpty(mail.SpamReport) && spamScore >= 2.5)
            {
                if (!string.IsNullOrEmpty(mail.Html))
                {
                    mail.Html = mail.SpamReport + "<br>---<br>" + mail.Html;
                }
                mail.Text = mail.SpamReport + "\n---\n" + mail.Text;
            }

            // Add message details
            if (!string.IsNullOrEmpty(mail.Html))
            {
                mail.Html += "<br>---<br>";
                mail.Html += $"Original From: \"{mail.From.Name}\" &lt;{mail.From.Email}&gt;";
                mail.Html += "<br>";
                mail.Html += $"Original To: \"{mail.To.FirstOrDefault()?.Email}\" &lt;{mail.To.FirstOrDefault()?.Email}&gt;";
            }
            
            mail.Text += "\n---\n";
            mail.Text += $"Original From: \"{mail.From.Name}\" <{mail.From.Email}>\n";
            mail.Text += $"Original To: \"{mail.To.FirstOrDefault()?.Email}\" <{mail.To.FirstOrDefault()?.Email}>";

            // Add attachments
            List<Attachment> attachmentList = new List<Attachment>();
            foreach (var attach in mail.Attachments)
            {
                _logger.LogInformation($"{attach.FileName}\n{attach.Data}");
                attachmentList.Add(Attachment.FromStream(attach.Data, attach.FileName, attach.ContentType, attach.ContentId));
            }

            // Send mail
            if (attachmentList.Count > 0)
            {
                var attachments = attachmentList.ToArray();
                return await client.Mail.SendToSingleRecipientAsync(
                    toEmail,
                    fromEmail,
                    mail.Subject,
                    mail.Html,
                    mail.Text,
                    replyTo: mail.From,
                    attachments: attachments
                ).ConfigureAwait(false);
            }
            else
            {
                return await client.Mail.SendToSingleRecipientAsync(
                    toEmail,
                    fromEmail,
                    mail.Subject,
                    mail.Html,
                    mail.Text,
                    replyTo: mail.From
                ).ConfigureAwait(false);
            }
        }

        public string GetEnvironmentVariable(string name)
        {
            return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }
    }
}
