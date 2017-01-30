using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Server.HttpListener;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Hosting
{
    public static class WebHostBuilderExtensions
    {
        public static IWebHostBuilder UseHttpListener(this IWebHostBuilder hostBuilder)
        {
            return hostBuilder.ConfigureServices(services =>
            {
                ServiceCollectionServiceExtensions.AddSingleton<IServer, HttpListenerServer>(services);
            });
        }
    }
}