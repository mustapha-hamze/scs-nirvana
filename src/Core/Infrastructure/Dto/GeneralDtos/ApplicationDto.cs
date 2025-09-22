using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Dto.GeneralDtos
{
    public class ApplicationDto : BaseEntity
    {
        [Required]
        [MaxLength(64)]
        [MinLength(4)]
        public string Title { get; set; }

        [Required]
        [MaxLength(512)]
        [MinLength(32)]
        public string Description { get; set; }

        public string LogoFileName { get; set; }

        public string ApplicationKey { get; set; }
    }
}