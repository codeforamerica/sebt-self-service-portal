using System.Xml.XPath;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.Core.Services
{
    public interface IOtpSenderService
    {
        Task<Result> SendOtpAsync(string to, string otp);
    }
}
