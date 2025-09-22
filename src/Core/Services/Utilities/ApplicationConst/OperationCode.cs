using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Utilities.ApplicationConst
{
    public static class OperationCode
    {
        // General
        public const int ChangeActivity = 4000;

        // CMS
        public const int CreateContent = 10001;
        public const int DeleteContent = 10002;
        public const int UpdateContent = 10003;

        public const int CreateCategory = 10004;
        public const int DeleteCategory = 10005;
        public const int UpdateCategory = 10006;

    }
}
