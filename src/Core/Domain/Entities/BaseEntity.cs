using System;
using System.ComponentModel.DataAnnotations;

namespace Domains.Entities
{
    public class BaseEntity
    {
        [Key]
        public int Id { get; set; }
        public int Status { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsActive { get; set; }
        public DateTime UpdatedDT { get; set; }
        public DateTime CreatedDT { get; set; }
    }
}
