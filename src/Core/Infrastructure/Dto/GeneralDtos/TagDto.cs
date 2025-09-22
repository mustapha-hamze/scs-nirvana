using System;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.GeneralDtos
{
    public class TagDto : BaseEntity
    {
        public int ApplicationId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }
        public int TypeId { get; set; }
    }
}