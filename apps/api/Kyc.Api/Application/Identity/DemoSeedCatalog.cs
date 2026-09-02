namespace Kyc.Api.Application.Identity;

/// <summary>Stable demo identities for KYC-101. Password is local-only and matches the README runbook.</summary>
public static class DemoSeedCatalog
{
    public const string Password = "ChangeMe1234";
    public const string DocumentFileName = "seed-id.png";

    public const string CompleteFormData =
        """{"fullName":"Ada Lovelace","dateOfBirth":"1815-12-10","nationality":"British","address":"12 Analytical Engine Rd"}""";

    public const string DraftTitle = "[seed] Draft";
    public const string SubmittedTitle = "[seed] Submitted";
    public const string InReviewTitle = "[seed] In review";
    public const string ApprovedTitle = "[seed] Approved";
    public const string RejectedTitle = "[seed] Rejected";
    public const string RejectComment = "Seed reject: identity document does not match the form.";
    public const string ApproveComment = "Seed approve: documents match.";

    public static readonly DemoTenantSpec[] Tenants =
    [
        new("acme", "Acme", "admin@acme.example", "reviewer@acme.example", "customer@acme.example"),
        new("globex", "Globex", "admin@globex.example", "reviewer@globex.example", "customer@globex.example")
    ];
}

public sealed record DemoTenantSpec(
    string Slug,
    string Name,
    string AdminEmail,
    string ReviewerEmail,
    string CustomerEmail);
