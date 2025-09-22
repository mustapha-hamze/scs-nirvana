using System;

namespace Services.Utilities.ApplicationFunctions
{
    public class CodeGenerator
    {
        public string GenerateAppKey(int applicationInternalId)
        {
            string strDateTime = DateTime.Now.ToString("yyyy") + DateTime.Now.Month.ToString("mm") + DateTime.Now.Day.ToString("dd");
            strDateTime += DateTime.Now.Hour.ToString("hh") + DateTime.Now.Minute.ToString("mm") + DateTime.Now.Second.ToString("ss");

            Random random = new Random(123456789);
            string appKey = "APP-KEY-NIRVANA-CMS-" + strDateTime + "-" + applicationInternalId + "-" + random.Next().ToString();

            return appKey;
        }
    }
}