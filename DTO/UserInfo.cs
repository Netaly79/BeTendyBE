public sealed class UserInfoResponse
{
    public Guid Id { get; set; }
    //public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public bool IsMaster { get; set; }
}