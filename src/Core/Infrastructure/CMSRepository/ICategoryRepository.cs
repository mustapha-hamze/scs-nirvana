using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domains.Entities.ContentManagement;
using Infrastructure.Repository;

namespace Infrastructure.CMSRepository
{
    public interface ICategoryRepository : IRepository<Category>
    {
        List<Category> List(int applicationId);
        List<Category> GetAllFullPath(int applicationId);
    }
}
