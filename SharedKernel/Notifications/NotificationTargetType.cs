namespace SharedKernel.Notifications
{
    // Mirrors NotificationService.Enums.NotificationTargetType by numeric value.
    // Kept as a separate definition on purpose — SharedKernel doesn't reference
    // NotificationService, the two only agree on this wire contract (JSON int).
    internal enum NotificationTargetType
    {
        User = 0,
        Users = 1,
        Role = 2,
        Broadcast = 3
    }
}
