using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Dto.CMSDtos
{
    public class ContentImageDto : BaseEntity
    {
        public int ContentId { get; set; }

        [StringLength(128)]
        public string ImageFileName { get; set; }

        public int Size { get; set; }
    }
}
