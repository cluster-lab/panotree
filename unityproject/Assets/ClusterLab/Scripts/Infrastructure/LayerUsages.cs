namespace ClusterLab.Infrastructure
{
    public static class LayerUsages
    {
        public const int OwnAvatarPhysicsMask =
            ~(LayerName.WaterMask | LayerName.UIMask |
                LayerName.FIRSTPERSON_ONLY_LAYERMask | LayerName.THIRDPERSON_ONLY_LAYERMask |
                LayerName.RidingItemMask |
                LayerName.InteractableExhibitMask | LayerName.OtherAvatarMask |
                LayerName.OwnAvatarMask | LayerName.GrabbableUIMask | LayerName.GrabbingItemMask |
                LayerName.PostProcessingMask | LayerName.PerformerMask | LayerName.AudienceMask |
                LayerName.HandOrPointerMask | LayerName.GrabbableObjectMask |
                LayerName.NameplateMask | LayerName.VenueLayer2Mask);
    }
}
