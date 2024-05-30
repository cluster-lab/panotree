
namespace ClusterLab.Infrastructure
{

    /**
     * Copied from ClusterVR.CreatorKit.Constants.LayerName (git hash bdba9abce44a7590b873372a9efe4496f5d691ee)
     */
    public static class CCKLayerName
    {
        public const int Default = 0;
        public const int TransparentFX = 1;
        public const int IgnoreRaycast = 2;
        public const int Water = 4;
        public const int UI = 5;
        public const int Accessory = 6;
        public const int AccessoryPreview = 7;
        public const int RidingItem = 11;
        public const int InteractableItem = 14;
        public const int GrabbingItem = 18;
        public const int VenueLayer0 = 19;
        public const int VenueLayer1 = 20;
        public const int PostProcessing = 21;
        public const int PerformerOnly = 22;
        public const int Performer = 23;
        public const int Audience = 24;
        public const int VenueLayer2 = 29;

        public const int InteractableExhibit = 13;
        public const int InteractableExhibitMask = 1 << InteractableExhibit;

        public const int InteractableItemMask = 1 << InteractableItem;
        public const int PostProcessingMask = 1 << PostProcessing;
    }

    /// <summary>
    /// レイヤー名を定数で管理するクラス
    /// </summary>
    public static class LayerName
    {
        public const int Default = CCKLayerName.Default;
        public const int TransparentFX = CCKLayerName.TransparentFX;
        public const int IgnoreRaycast = CCKLayerName.IgnoreRaycast;
        public const int ItemPreview = 3;
        public const int Water = CCKLayerName.Water;
        public const int UI = CCKLayerName.UI;
        public const int Accessory = CCKLayerName.Accessory;
        public const int AccessoryPreview = CCKLayerName.AccessoryPreview;
        public const int FIRSTPERSON_ONLY_LAYER = 9;
        public const int THIRDPERSON_ONLY_LAYER = 10;
        public const int RidingItem = CCKLayerName.RidingItem;
        public const int CraftItem = 12;
        public const int InteractableExhibit = CCKLayerName.InteractableExhibit;
        public const int InteractableItem = CCKLayerName.InteractableItem;
        public const int OtherAvatar = 15;
        public const int OwnAvatar = 16;
        public const int GrabbableUI = 17;
        public const int GrabbingItem = CCKLayerName.GrabbingItem;
        public const int VenueLayer0 = CCKLayerName.VenueLayer0;
        public const int VenueLayer1 = CCKLayerName.VenueLayer1;
        public const int PostProcessing = CCKLayerName.PostProcessing;
        public const int PerformerOnly = CCKLayerName.PerformerOnly;
        public const int Performer = CCKLayerName.Performer;
        public const int Audience = CCKLayerName.Audience;
        public const int PostProcessingCameraOnly = 25;
        public const int HandOrPointer = 26;
        public const int GrabbableObject = 27;
        public const int Nameplate = 28;
        public const int VenueLayer2 = CCKLayerName.VenueLayer2;
        public const int OwnNameplate = 30;

        public const int DefaultMask = 1 << Default;
        public const int TransparentFXMask = 1 << TransparentFX;
        public const int IgnoreRaycastMask = 1 << IgnoreRaycast;
        public const int WaterMask = 1 << Water;
        public const int UIMask = 1 << UI;
        public const int AccessoryMask = 1 << Accessory;
        public const int AccessoryPreviewMask = 1 << AccessoryPreview;
        public const int FIRSTPERSON_ONLY_LAYERMask = 1 << FIRSTPERSON_ONLY_LAYER;
        public const int THIRDPERSON_ONLY_LAYERMask = 1 << THIRDPERSON_ONLY_LAYER;
        public const int RidingItemMask = 1 << RidingItem;
        public const int CraftItemMask = 1 << CraftItem;
        public const int OwnAvatarMask = 1 << OwnAvatar;
        public const int InteractableExhibitMask = 1 << InteractableExhibit;
        public const int InteractableItemMask = 1 << InteractableItem;
        public const int OtherAvatarMask = 1 << OtherAvatar;
        public const int GrabbingItemMask = 1 << GrabbingItem;
        public const int GrabbableUIMask = 1 << GrabbableUI;
        public const int VenueLayer0Mask = 1 << VenueLayer0;
        public const int VenueLayer1Mask = 1 << VenueLayer1;
        public const int PostProcessingMask = 1 << PostProcessing;
        public const int PerformerOnlyMask = 1 << PerformerOnly;
        public const int PerformerMask = 1 << Performer;
        public const int AudienceMask = 1 << Audience;
        public const int HandOrPointerMask = 1 << HandOrPointer;
        public const int GrabbableObjectMask = 1 << GrabbableObject;
        public const int NameplateMask = 1 << Nameplate;
        public const int VenueLayer2Mask = 1 << VenueLayer2;
        public const int OwnNameplateMask = 1 << OwnNameplate;
    }
}
