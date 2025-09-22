using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.PropTechDtos
{
    public class CompanyDto : BaseEntity
    {
        // relation
        public int CityId { get; set; }
        public int ApplicationId { get; set; }

        // main fields
        public int Type { get; set; }

        [StringLength(64)]
        public string Title { get; set; }
        [StringLength(1024)]
        public string Description { get; set; }
        [StringLength(16)]
        public string PhoneOne { get; set; }
        [StringLength(16)]
        public string PhoneTwo { get; set; }
        [StringLength(128)]
        [EmailAddress]
        public string Email { get; set; }
        [StringLength(128)]
        public string Website { get; set; }
        [StringLength(256)]
        public string Address { get; set; }
        [StringLength(64)]
        public string POIFullName { get; set; }
        [StringLength(16)]
        public string POIMobile { get; set; }
        [StringLength(16)]
        public string POIPhone { get; set; }
        [StringLength(128)]
        [EmailAddress]
        public string POIEmail { get; set; }
        [StringLength(256)]
        public string POIDescription { get; set; }
        [StringLength(64)]
        public string POIPosition { get; set; }
    }
}