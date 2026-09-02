using Kyc.Api.Application.Identity;

namespace Kyc.Api.Tests;

public sealed class AuthLimitsOptionsTests
{
    [Fact]
    public void Non_development_limits_are_stricter_and_register_is_stricter_than_login()
    {
        var development = AuthLimitsOptions.DevelopmentDefaults();
        var production = AuthLimitsOptions.ProductionDefaults();

        Assert.True(production.LoginPermitPerMinute < development.LoginPermitPerMinute);
        Assert.True(production.RegisterPermitPerMinute < development.RegisterPermitPerMinute);
        Assert.True(production.GraphqlPermitPerMinute < development.GraphqlPermitPerMinute);
        Assert.True(production.RegisterPermitPerMinute < production.LoginPermitPerMinute);
        Assert.True(development.RegisterPermitPerMinute < development.LoginPermitPerMinute);
    }
}
