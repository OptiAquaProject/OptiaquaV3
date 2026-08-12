using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace DatosOptiaqua {
    public class Email {
        public static async Task SendMail(string email, string subject, string bodyHtml, List<string> attachListFiles = null) {
            var emailMessage = new MimeMessage();

            emailMessage.From.Add(new MailboxAddress("Optiaqua", "optiaqua@gmail.com"));
            var lEmails = email.Split(',', ';').ToList();
            var lTo = new List<MailboxAddress>();
            lEmails.ForEach(x => lTo.Add(new MailboxAddress("", x)));
            //emailMessage.To.Add(new MailboxAddress("", email));
            emailMessage.To.AddRange(lTo);
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart("html") { Text = bodyHtml };
            try {
                var client = new SmtpClient();
                await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.Auto);
                var emailSender = DB.ConfigLoad("EmailSender");
                var emailSenderPasswordCrypt = DB.ConfigLoad("EmailSenderPasswordCrypt");
                var emailSenderPassword = webapi.Encriptacion.Desencripta(emailSenderPasswordCrypt);
                await client.AuthenticateAsync(emailSender, emailSenderPassword);
                await client.SendAsync(emailMessage);
                await client.DisconnectAsync(true);
            } catch (Exception ex) {
                var e = ex;
                throw;
            }
        }
    }
}