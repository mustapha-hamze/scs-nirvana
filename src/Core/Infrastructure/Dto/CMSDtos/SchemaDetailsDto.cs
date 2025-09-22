using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.CMSDtos
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