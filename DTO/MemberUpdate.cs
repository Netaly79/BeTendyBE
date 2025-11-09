public sealed class MemberUpdateRequest
{
    public UserUpdateDto? User { get; set; }
    public MasterUpdateDto? Master { get; set; }
}

public sealed class UserUpdateDto
{
    public string? FirstName { get; set; }
    public string? LastName  { get; set; }
    public string? Phone     { get; set; }
    public string? AvatarUrl { get; set; }
}

public sealed class MasterUpdateDto
{
    public string? About            { get; set; }
    public List<string>? Skills     { get; set; }
    public int? ExperienceYears     { get; set; }
    public string? Address          { get; set; }
}