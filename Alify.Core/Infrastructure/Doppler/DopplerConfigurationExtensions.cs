using Microsoft.Extensions.Configuration;

namespace Alify.Core.Infrastructure.Doppler
{
    public static class DopplerConfigurationExtensions
    {
        public static IConfigurationBuilder AddDoppler(this IConfigurationBuilder builder, string? dopplerToken)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.Add(new DopplerConfigurationSource(dopplerToken));
        }
    }
}
