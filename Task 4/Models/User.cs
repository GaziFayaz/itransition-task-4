using System.Text.Json.Serialization;

namespace Task_4.Models;

public enum Status
{
    Verified = 1,
    Unverified = 2,
}

public class User
{
    public int Id { get; set; }

    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Status Status { get; set; } = Status.Unverified;

    public bool IsBlocked { get; set; } = false;

    public DateTime? LastLoggedInAt { get; set; } = null;

    public DateTime? LastActivityAt { get; set; } = null;
}