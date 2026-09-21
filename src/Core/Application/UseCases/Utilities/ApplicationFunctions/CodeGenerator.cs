using System;
using System.Security.Cryptography;

namespace Application.UseCases.Utilities.ApplicationFunctions
{
    public class CodeGenerator
    {
        public string GenerateAppKey(int applicationInternalId)
        {
            string strDateTime = DateTime.UtcNow.ToString("yyyy") + DateTime.UtcNow.Month.ToString("mm") + DateTime.UtcNow.Day.ToString("dd");
            strDateTime += DateTime.UtcNow.Hour.ToString("hh") + DateTime.UtcNow.Minute.ToString("mm") + DateTime.UtcNow.Second.ToString("ss");

            // Fixed-seed System.Random previously made this component identical on every call.
            string appKey = "APP-KEY-NIRVANA-CMS-" + strDateTime + "-" + applicationInternalId + "-" + RandomNumberGenerator.GetInt32(int.MaxValue);

            return appKey;
        }
    }
}