using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Utilities.ApplicationConst
{
    public class ConnectionString
    {
        //public string Get = "Server=tcp:entralon.database.windows.net,1433;Initial Catalog=ENTRALON_COREDB_TEST;Persist Security Info=False;User ID=entralon-dbaccess;Password=Ent@3311!$;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
        public string Get = "Server=tcp:entralon.database.windows.net,1433;Initial Catalog=ENTRALON_COREDB;Persist Security Info=False;User ID=entralon-dbaccess;Password=Ent@3311!$;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
    }
}
