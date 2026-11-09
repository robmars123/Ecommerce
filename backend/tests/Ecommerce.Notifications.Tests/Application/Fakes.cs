using Ecommerce.Identity.Contracts;
using Ecommerce.Notifications.Api;
using Ecommerce.Notifications.Contracts;
using Ecommerce.Orders.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace Ecommerce.Notifications.Tests.Application;

internal sealed class FakeUsers : IUserReader
{
    public Task<IReadOnlyDictionary<Guid, UserContact>> GetContactsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) => throw new NotSupportedException();
    public Task<Guid?> FindIdByEmailAsync(string email, CancellationToken ct) => throw new NotSupportedException();


    public Dictionary<Guid, UserContact> Contacts { get; } = [];

    public Task<UserContact?> GetContactAsync(Guid userId, CancellationToken ct) => Task.FromResult(Contacts.GetValueOrDefault(userId));
}

internal sealed class FakeOrders : IOrderReader
{
    public Task<RecentOrderPage> ListRecentAsync(int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();

    public Task<int> CountPendingReturnsAsync(CancellationToken ct) => throw new NotSupportedException();

    public Dictionary<Guid, OrderNotificationView> Views { get; } = [];

    public Task<OrderNotificationView?> GetNotificationViewAsync(Guid orderId, CancellationToken ct) => Task.FromResult(Views.GetValueOrDefault(orderId));
}

internal sealed class RecordingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];
    public Exception? Throw { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (Throw is not null)
        {
            throw Throw;
        }
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed record HubSend(string Target, string Method, object? Argument);

/// <summary>A hub context that records where each message went: only the owner's group may ever be a target.</summary>
internal sealed class FakeHubContext : IHubContext<OrderStatusHub>
{
    public FakeHubContext() => Clients = new FakeHubClients(Sent);

    public List<HubSend> Sent { get; } = [];
    public IHubClients Clients { get; }
    public IGroupManager Groups { get; } = new FakeGroupManager();
}

internal sealed class FakeHubClients(List<HubSend> sent) : IHubClients
{
    public IClientProxy All => Proxy("all");

    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy("all-except");

    public IClientProxy Client(string connectionId) => Proxy("client:" + connectionId);

    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy("clients");

    public IClientProxy Group(string groupName) => Proxy("group:" + groupName);

    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy("group-except:" + groupName);

    public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy("groups");

    public IClientProxy User(string userId) => Proxy("user:" + userId);

    public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy("users");

    private RecordingProxy Proxy(string target) => new(target, sent);
}

internal sealed class RecordingProxy(string target, List<HubSend> sent) : IClientProxy
{
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        sent.Add(new HubSend(target, method, args.SingleOrDefault()));
        return Task.CompletedTask;
    }
}

internal sealed class FakeGroupManager : IGroupManager
{
    public List<(string ConnectionId, string Group)> Added { get; } = [];

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Added.Add((connectionId, groupName));
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeCallerContext(System.Security.Claims.ClaimsPrincipal? user) : HubCallerContext
{
    public bool Aborted { get; private set; }

    public override string ConnectionId => "conn-1";
    public override string? UserIdentifier => null;
    public override System.Security.Claims.ClaimsPrincipal? User => user;
    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
    public override Microsoft.AspNetCore.Http.Features.IFeatureCollection Features { get; } = new Microsoft.AspNetCore.Http.Features.FeatureCollection();
    public override CancellationToken ConnectionAborted => CancellationToken.None;

    public override void Abort() => Aborted = true;
}
