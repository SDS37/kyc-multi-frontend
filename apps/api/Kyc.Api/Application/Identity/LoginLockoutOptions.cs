namespace Kyc.Api.Application.Identity;

/// <summary>In-memory failed-login lockout (KYC-093). Multi-instance hosts need a shared store later.</summary>
public sealed class LoginLockoutOptions
{
    public const string SectionName = "Lockout";

    public int MaxFailedAttempts { get; set; } = 5;
    public int DurationMinutes { get; set; } = 15;

    public void Validate()
    {
        if (MaxFailedAttempts <= 0)
        {
            throw new InvalidOperationException("Lockout:MaxFailedAttempts must be greater than 0.");
        }

        if (DurationMinutes <= 0)
        {
            throw new InvalidOperationException("Lockout:DurationMinutes must be greater than 0.");
        }
    }
}
