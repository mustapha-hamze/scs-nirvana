using System;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.GeneralDtos
{
    public class CultureDto : BaseEntity
    {
        public CultureDto()
        {
        }
        // public int ApplicationId { get; set; }
        [StringLength(64)]
        public string Title { get; set; }

        [StringLength(8)]
        public string Key { get; set; }
    }
}
