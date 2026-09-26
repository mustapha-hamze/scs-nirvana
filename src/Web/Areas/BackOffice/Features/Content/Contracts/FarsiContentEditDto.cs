using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Web.Areas.BackOffice.Features.Content.Contracts
{
    // Text-only manual translation request: translated text keyed by the stable master IDs, plus
    // the source fingerprint the editor was rendered from. Layout, media and element titles are
    // never posted - they stay read-only master data.
    public class FarsiContentEditDto
    {
        public int Id { get; set; }

        // ContentSourceFingerprint of the English source the form was rendered from.
        public string SourceFingerprint { get; set; }

        [StringLength(256)]
        public string Title { get; set; }

        [StringLength(2048)]
        public string HeadLine { get; set; }

        [StringLength(2048)]
        public string Abstract { get; set; }

        public string Description { get; set; }

        // Null when master has no metadata.
        public FarsiContentMetadataEditDto Metadata { get; set; }
        public List<FarsiSectionEditDto> Sections { get; set; }
    }
}
