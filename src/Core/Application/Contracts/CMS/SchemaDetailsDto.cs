using System.ComponentModel.DataAnnotations;
using Domains.Entities;

namespace Application.Contracts.CMS
{
    public class SchemaDetailsDto : BaseEntity
    {
        public int SchemaId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }

        public int TypeId { get; set; }

        public int Size { get; set; }
    }
}
