using Stripe;

namespace AuthService.Api.Startup;

public static class PaymentProcessorSettings
{
    public static void GetPaymentProcessorSettings(this IServiceCollection services, IConfiguration configuration)
    {
        StripeConfiguration.ApiKey = configuration.GetSection("Stripe:ApiKey").Value;
    }
}