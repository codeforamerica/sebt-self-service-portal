namespace SEBT.Portal.Core.AppSettings
{
    public class SmtpClientSettings
    {
        public static readonly string SectionName = "SmtpClientSettings";

        public required string SmtpServer { get; set; }
        public required int SmtpPort { get; set; }
        public required bool EnableSsl { get; set; }

        /// <summary>
        /// Optional SMTP username (e.g. SES SMTP credentials access key).
        /// When set with Password, used for authenticated SMTP (e.g. Amazon SES).
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// Optional SMTP password (e.g. SES SMTP credentials secret key).
        /// When set with UserName, used for authenticated SMTP.
        /// </summary>
        public string? Password { get; set; }
    }
}
