namespace SEBT.Portal.Core.AppSettings
{
    public class SmtpClientSettings
    {
        public static readonly string SectionName = "SmtpClientSettings";

        public required string SmtpServer { get; set; }
        public required int SmtpPort { get; set; }
        public required bool EnableSsl { get; set; }
    }
}
