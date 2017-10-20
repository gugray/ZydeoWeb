using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ZDO.ConcAlign
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();
            app.UseStaticFiles();
            app.UseMvc(routes =>
            {
                routes.MapRoute("api", "api/{controller}/{action}/{*paras}", new { paras = "" });
                routes.MapRoute("search", "search/{*paras}", new { controller = "Index", action = "Index", paras = "" });
                routes.MapRoute("default", "{*paras}", new { controller = "Index", action = "Index", paras = "" });
            });
        }
    }
}
