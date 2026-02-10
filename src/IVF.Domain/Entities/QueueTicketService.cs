using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Join entity between QueueTicket and ServiceCatalog — normalizes ServiceIndications JSON column.
/// </summary>
public class QueueTicketService : BaseEntity
{
    public Guid QueueTicketId { get; private set; }
    public Guid ServiceCatalogId { get; private set; }

    // Navigation
    public virtual QueueTicket QueueTicket { get; private set; } = null!;
    public virtual ServiceCatalog ServiceCatalog { get; private set; } = null!;

    private QueueTicketService() { }

    public static QueueTicketService Create(Guid queueTicketId, Guid serviceCatalogId)
    {
        return new QueueTicketService
        {
            QueueTicketId = queueTicketId,
            ServiceCatalogId = serviceCatalogId
        };
    }
}
