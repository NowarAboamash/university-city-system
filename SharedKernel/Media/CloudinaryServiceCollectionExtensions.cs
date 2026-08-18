using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Media
{
    public static class CloudinaryServiceCollectionExtensions
    {
        public static IServiceCollection AddCloudinaryImageUploader(this IServiceCollection services)
        {
            services.AddSingleton<IImageUploader, CloudinaryImageUploader>();
            return services;
        }
    }
}
