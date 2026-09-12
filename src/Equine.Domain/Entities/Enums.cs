namespace Equine.Domain.Entities;

public enum Djurslag
{
    Hast,
    Ko,
    Sva,
    Katt,
    Hund,
    other
}

public enum HingstSex
{
    Sto,
    Valack,
    Hingst,
    Okant
}

public enum JournalStatus
{
    Draft,
    Signed
}

public enum JournalSource
{
    Practice,
    Import
}

public enum TreatmentTypeStatus
{
    Active,
    Inactive
}

public enum LocationType
{
    ClientStable,
    FixedSite
}

public enum BookingStatus
{
    Requested,
    Confirmed,
    Completed,
    Cancelled,
    NoShow
}

public enum BookingSource
{
    Widget,
    Manual,
    Phone
}

public enum NotificationType
{
    BookingVerification,
    BookingRequestedPractitioner,
    BookingConfirmed,
    BookingRescheduled,
    BookingCancelled,
    Reminder24h,
    DailySummary,
    FollowUpDue,
    JournalUnsigned,
    NotificationFailed,
    MagicLink,
    MarketingBroadcast,
    WeeklyDigest
}

public enum NotificationChannel
{
    Email,
    Sms,
    InApp
}

public enum NotificationDeliveryStatus
{
    Pending,
    Sending,
    Sent,
    Failed,
    Cancelled
}

public enum TenantStatus
{
    Active,
    Suspended
}

public enum TenantPlan
{
    Free,
    Paid
}

public enum TenantSubscriptionStatus
{
    Free,
    Active,
    PastDue,
    Canceled
}
