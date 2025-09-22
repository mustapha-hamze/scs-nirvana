namespace Infrastructure.Dto.UserManagementDtos;
public class UserAttachmentDto : BaseEntity
{
    [StringLength(450)]
    [Required]
    public string UserId { get; set; }
    [StringLength(256)]
    [Required]
    public string Title { get; set; }
    public string Description { get; set; }
    [Required]
    public int AttachmentType { get; set; }
}