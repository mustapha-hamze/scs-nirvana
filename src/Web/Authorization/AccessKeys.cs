namespace Web.Authorization;

// Exact, token-comparable BackOffice access-key strings. These mirror the persisted
// comma-delimited per-user, per-application access value (IUserManagementServices.GetUserAccesses)
// and the client-side accesses.Contains("KEY") gates already in the corresponding Razor views -
// see e.g. Views/Content/_ContentListActionsButton.cshtml, Views/Slider/GetSliderItemList.cshtml,
// Views/Shared/_SideBarCMS.cshtml/_SideBarSCM.cshtml. RequireAccessAttribute/AccessKeyAuthorizer
// compare these with exact-token equality (never Contains/StartsWith), so a similar/prefix key can
// never grant a permission it wasn't issued. SuperAdmin always bypasses these checks.
public static class AccessKeys
{
    public static class Content
    {
        // Base module key: gates the Content list/type-navigation itself (sidebar "Content" link).
        public const string Module = "CMS1000_1001";

        public const string Add = "CMS1000_1001_ADD_1000";
        public const string Save = "CMS1000_1001_SAVE_1003";
        public const string Update = "CMS1000_1001_UPDATE_1001";
        public const string Edit = "CMS1000_1001_EDIT_1005";
        public const string Delete = "CMS1000_1001_DELETE_1002";
        public const string ChangeActivity = "CMS1000_1001_CHANGE_ACTIVITY_1004";

        // Farsi localization form. The UI's own access check for this
        // (accesses.Contains("CMS1000_1001_EDIT_FARSI_1014")) is currently commented out in
        // _ContentListActionsButton.cshtml, but the key itself is established there - enforced
        // here regardless of the client-side gate being disabled.
        public const string EditFarsi = "CMS1000_1001_EDIT_FARSI_1014";

        // Sections/"body" editor tab: preview (read) vs. save (create/update/reorder/delete
        // sections, and the body/file/gallery uploads used from within it) are distinct keys.
        public const string PreviewBody = "CMS1000_1001_PREVIEW_BODY_1007";
        public const string SaveBody = "CMS1000_1001_SAVE_BODY_1007";

        public const string PreviewImages = "CMS1000_1001_PREVIEW_IMAGES_1008";
        public const string UploadImages = "CMS1000_1001_UPLOAD_IMAGES_1009";

        public const string PreviewRelations = "CMS1000_1001_PREVIEW_RELATIONS_1010";
        public const string SaveRelations = "CMS1000_1001_SAVE_RELATIONS_1011";

        public const string PreviewMetadata = "CMS1000_1001_PREVIEW_META_1012";
        public const string SaveMetadata = "CMS1000_1001_SAVE_META_1013";
    }

    public static class Category
    {
        // No finer-grained key exists in the UI for Category (Views/Shared/_SideBarCMS.cshtml
        // only gates the sidebar link) - every CategoryController action shares this one key.
        public const string Module = "CMS1000_1002";
    }

    public static class Schema
    {
        // Same as Category: no finer-grained key exists, only the sidebar module gate.
        public const string Module = "CMS1000_1003";
    }

    public static class Slider
    {
        // Base module key: sidebar "Sliders" link (Views/Shared/_SideBarSCM.cshtml).
        public const string Module = "SCM3000_1001";

        // Views/Slider/_CreateSliderButton.cshtml gates navigation to the Create form with this
        // CMS-prefixed key verbatim (not a typo we introduced) - preserved exactly as the
        // established UI contract, not the SCM3000_1001_SAVE_1000 key used by the form's own
        // submit button below.
        public const string Add = "CMS1000_1001_ADD_1000";
        public const string Save = "SCM3000_1001_SAVE_1000";

        public const string AccessItems = "SCM3000_1001_ACCESSITEMS_1001";
        public const string SaveItem = "SCM3000_1001_SAVEITEM_1002";
        public const string Activity = "SCM3000_1001_ACTIVITY_1003";
        public const string DeleteItem = "SCM3000_1001_DELETE_1004";
        public const string UpdateItem = "SCM3000_1001_UPDATE_1005";
    }
}
