public interface IMasterService
{
    Task EnsureMasterAsync(Guid userId, CancellationToken ct = default);
}
