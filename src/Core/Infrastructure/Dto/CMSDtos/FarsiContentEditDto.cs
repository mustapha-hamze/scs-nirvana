using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.CMSDtos
{
    public class FarsiContentEditDto
    {
        public int Id { get; set; }
        public int ApplicationId { get; set; }
        public int TypeId { get; set; }

        [StringLength(256)]
        public string Title { get; set; }

        [StringLength(2048)]
        public string HeadLine { get; set; }

        [StringLength(2048)]
        public string Abstract { get; set; }

        public string Description { get; set; }

        public DateTime PublishDt { get; set; }

        public FarsiContentMetadataEditDto Metadata { get; set; }
        public List<FarsiSectionEditDto> Sections { get; set; }
    }
}
