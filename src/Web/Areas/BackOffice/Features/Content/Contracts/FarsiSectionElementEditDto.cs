using System.ComponentModel.DataAnnotations;

namespace Web.Areas.BackOffice.Features.Content.Contracts
{
    // Only the text field the master element's type edits is posted; the other stays null.
    public class FarsiSectionElementEditDto
    {
        public int Id { get; set; }

        [StringLength(256)]
        public string TinyText { get; set; }

        public string EditorText { get; set; }
    }
}
