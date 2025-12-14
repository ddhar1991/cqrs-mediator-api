using MediatR;

namespace cqrs_mediatr.Notifications
{
    public record ProductCreatedNotification(Guid Id) : INotification;
}
