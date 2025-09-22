using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.PropTechDtos
{
    public class DevelopmentDto : BaseEntity
    {
        public int ApplicationId { get; set; }
        public int CountryId { get; set; }
        public int DealType { get; set; } // rent or sell
        public int DevelopmentType { get; set; } // house or building
        public int UsageType { get; set; } // new or used
        public int ConstructionType { get; set; } // under construction or ready to move or planed construction
        // relation
        public int DeveloperId { get; set; } // company with developer type
        public int SalesOfficeId { get; set; } // company with sales office type
        public int DistrictId { get; set; } // get it from zone entity
        // extera fields
        public int ReferenceId { get; set; } // for now is equal with main Id[key]

        [StringLength(128)]
        public string ReferenceId2 { get; set; }

        // main fields
        [StringLength(128)]
        public string Title { get; set; }

        [StringLength(256)]
        public string FakeTitle { get; set; }

        [StringLength(256)]
        public string FA_FakeTitle { get; set; }

        public string Description { get; set; }
        public string FA_Description { get; set; }
        public int CompletionYearInt { get; set; }
        [StringLength(64)]
        public string CompletionYearStr { get; set; }

        public int Blocks { get; set; }
        public int MinFloor { get; set; }
        public int MaxFloor { get; set; }
        public int Units { get; set; }
        public bool Residential { get; set; }
        public int ResidentialUnits { get; set; }
        public bool Office { get; set; }
        public int OfficeUnits { get; set; }
        public bool Commercial { get; set; }
        public int CommercialUnits { get; set; }
        public bool Storage { get; set; }
        public int StorageUnits { get; set; }
        [StringLength(128)]
        public string UnitTips { get; set; }

        public int MinBedRoom { get; set; }
        public int MaxBedRoom { get; set; }
        public bool ComplexDevelopment { get; set; }
        public bool FinalEdit { get; set; }

        [StringLength(1024)]
        public string Migrations { get; set; }

        [StringLength(1024)]
        public string Ownerships { get; set; }

        [StringLength(1024)]
        public string Payments { get; set; }

        [StringLength(256)]
        public string GoogleMapLocation { get; set; }

        [StringLength(256)]
        public string ReferenceSite { get; set; }

        [StringLength(256)]
        public string ReferenceMeta { get; set; }

        [StringLength(256)]
        public string Abstract { get; set; }

        [StringLength(256)]
        public string RefEx1 { get; set; }

        [StringLength(256)]
        public string RefEx2 { get; set; }

        [StringLength(256)]
        public string RefEx3 { get; set; }

        [StringLength(256)]
        public string RefProps { get; set; }

        [StringLength(256)]
        public string RefTitle1 { get; set; }

        [StringLength(256)]
        public string UITitle { get; set; }

        public string Text { get; set; }
        public bool Featured { get; set; }

        [StringLength(128)]
        public string LiveSync { get; set; }

        [StringLength(128)]
        public string LiveSyncModule { get; set; }

        [StringLength(256)]
        public string LiveSyncUrl { get; set; }

        [StringLength(512)]
        public string LocationString { get; set; }

        public string DeveloperTitle { get; set; }
        public string SalesOfficeTitle { get; set; }
        public string ZoneTitle { get; set; }
        public string DevelopmentTypeTitle { get; set; }
        public double StartingPrice { get; set; }
        public string CurrrencyInit { get; set; }
        public int MinPrice { get; set; }
        public int MaxPrice { get; set; }
        public int MinArea { get; set; }
        public int MaxArea { get; set; }
        public int CityId { get; set; }
        public int AreaId { get; set; }

        [StringLength(256)]
        public string MainImageFileName { get; set; }

        [StringLength(1024)]
        public string WhatsNearby_Education { get; set; }
        [StringLength(1024)]
        public string WhatsNearby_HealthMedical { get; set; }
        [StringLength(1024)]
        public string WhatsNearby_Transportation { get; set; }
        [StringLength(1024)]
        public string WhatsNearby_ShoppingCenter { get; set; }

        public int ViewedTimes { get; set; }

        public DateTime RecentViewedDT { get; set; }

        public virtual ICollection<PropertyDto> Properties { get; set; }
        public virtual ICollection<DevelopmentAttachmentDto> Attachments { get; set; }
        public virtual ICollection<DevelopmentImageDto> Images { get; set; }
    }
}