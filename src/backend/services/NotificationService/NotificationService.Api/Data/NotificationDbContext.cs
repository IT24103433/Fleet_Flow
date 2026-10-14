using Microsoft.EntityFrameworkCore;
namespace NotificationService.Api.Data;
public sealed class InboxNotification
{
    public Guid Id {get;set;}=Guid.NewGuid(); public Guid EventId {get;set;} public Guid TargetUserId {get;set;}
    public string Category {get;set;}="System";public string Title {get;set;}="";public string Message {get;set;}="";
    public Guid? RelatedEntityId {get;set;} public Guid? VehicleId {get;set;}
    public DateTime CreatedAt {get;set;}=DateTime.UtcNow;public bool IsRead {get;set;}
}
public sealed class NotificationReceipt
{
    public Guid EventId {get;set;} public string PayloadHash {get;set;}="";public DateTime ProcessedAt {get;set;}
}
public sealed class RejectedNotificationEvent
{
    public Guid Id {get;set;}=Guid.NewGuid();public string Topic {get;set;}="";public int Partition {get;set;}
    public long Offset {get;set;}public string PayloadHash {get;set;}="";public string Payload {get;set;}="";
    public string ErrorCode {get;set;}="InvalidEvent";public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
}
public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options):DbContext(options)
{
    public DbSet<InboxNotification> Notifications=>Set<InboxNotification>();
    public DbSet<NotificationReceipt> Receipts=>Set<NotificationReceipt>();
    public DbSet<RejectedNotificationEvent> RejectedEvents=>Set<RejectedNotificationEvent>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<InboxNotification>(e=>
        {
            e.ToTable("Notifications");e.HasKey(n=>n.Id);
            e.Property(n=>n.Category).HasMaxLength(50).IsRequired();e.Property(n=>n.Title).HasMaxLength(200).IsRequired();
            e.Property(n=>n.Message).HasMaxLength(2000).IsRequired();
            e.HasIndex(n=>new{n.EventId,n.TargetUserId}).IsUnique();e.HasIndex(n=>new{n.TargetUserId,n.IsRead,n.CreatedAt});
        });
        model.Entity<NotificationReceipt>(e=>{e.ToTable("NotificationReceipts");e.HasKey(n=>n.EventId);e.Property(n=>n.PayloadHash).HasMaxLength(64).IsRequired();});
        model.Entity<RejectedNotificationEvent>(e=>
        {e.ToTable("RejectedNotificationEvents");e.HasKey(n=>n.Id);e.HasIndex(n=>new{n.Topic,n.Partition,n.Offset}).IsUnique();
         e.Property(n=>n.Topic).HasMaxLength(200).IsRequired();e.Property(n=>n.PayloadHash).HasMaxLength(64).IsRequired();
         e.Property(n=>n.ErrorCode).HasMaxLength(50).IsRequired();e.Property(n=>n.Payload).IsRequired();});
    }
}
