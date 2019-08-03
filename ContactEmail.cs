using System;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BrnBrns.Function
{
    public static class ContactEmail
    {
        [FunctionName("ContactEmail")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("ContactEmail received new request");

            // Get posted req object
            string content = await new StreamReader(req.Body).ReadToEndAsync();
            PostData data = JsonConvert.DeserializeObject<PostData>(content);

            // Check for required input
            if (data == null || string.IsNullOrEmpty(data.name) || string.IsNullOrEmpty(data.email) || string.IsNullOrEmpty(data.message)) {
                return new BadRequestObjectResult("Please pass name, email, and message in the JSON request body");
            }

            // To, from emails
            string toEmail = GetEnvironmentVariable("SmtpTo");
            string fromEmail = GetEnvironmentVariable("SmtpUser");
            
            // SMTP mail settings
            string smtpHost = GetEnvironmentVariable("SmtpHost");
            string smtpUser = fromEmail;
            string smtpPass = GetEnvironmentVariable("SmtpPassword");
            int smtpPort = int.Parse(GetEnvironmentVariable("SmtpPort"));

            // Create email subject and body
            string subject = $"New Message from {data.name}";
            string body = $"Hello,\n\n {data.name} has sent you a message:\n\n{data.message}";

            // Create SMTP mail client
            SmtpClient client = new SmtpClient();
            client.Port = smtpPort;
            client.EnableSsl = true;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            client.Host = smtpHost;
            client.Credentials = new System.Net.NetworkCredential(smtpUser, smtpPass);

            // Create mail message
            MailMessage message = new MailMessage();
            message.Subject = subject;
            message.From = new MailAddress(fromEmail);
            message.ReplyToList.Add(new MailAddress(data.email));
            message.Body = body;
            message.To.Add(new MailAddress(toEmail));

            // Send message
            try
            {
                client.Send(message);
                log.LogInformation("ContactEmail successfully sent email");
            }
            catch (Exception ex)
            {
                log.LogError($"ContactEmail ERROR: {ex.ToString()}");
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
