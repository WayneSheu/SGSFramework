namespace PhysLIMS.API.Extensions
{
    public static class WebApplicationExtensions
    {
        public static void MapMapFallbackToPathForDirectory(this WebApplication app, string pathPrefix, string filePath)
        {
            ArgumentNullException.ThrowIfNull(app);

            app.MapWhen(ctx => ctx.Request.Path.StartsWithSegments(pathPrefix, StringComparison.OrdinalIgnoreCase), builder =>
            {
                builder.UseRouting();
                builder.UseEndpoints(endpoints =>
                {
                    endpoints.MapFallbackToFile(filePath);
                });
            });
        }
    }
}
