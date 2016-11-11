using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

using ZDO.Console.Logic;

namespace ZDO.CHSite
{
    public class Startup
    {
        private readonly IHostingEnvironment env;
        private readonly ILoggerFactory loggerFactory;
        private readonly IConfigurationRoot config;

        public Startup(IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            this.env = env;
            this.loggerFactory = loggerFactory;

            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables();
            string cfgFileName = "/etc/zdo/console-stgs.json";
            if (File.Exists(cfgFileName)) builder.AddJsonFile(cfgFileName, optional: true);
            config = builder.Build();

            // If running in production or staging, will log to file. Initialize Serilog here.
            if (!env.IsDevelopment())
            {
                var seriConf = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.File(config["logFileName"]);
                Log.Logger = seriConf.CreateLogger();
            }
            // Log to console (debug) or file (otherwise).
            if (env.IsDevelopment()) loggerFactory.AddConsole(LogLevel.Information);
            else loggerFactory.AddSerilog();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // MVC for serving pages and REST
            services.AddMvc();
            // Configuration singleton
            services.Configure<Options>(opt => config.Bind(opt));
        }

        public void Configure(IApplicationBuilder app, IApplicationLifetime appLife)
        {
            // Static file options: inject caching info for all static files.
            StaticFileOptions sfo = new StaticFileOptions
            {
                OnPrepareResponse = (context) =>
                {
                    // Cache indefinitely.
                    if (context.Context.Request.Path.Value.StartsWith("/static/"))
                    {
                        context.Context.Response.Headers["Cache-Control"] = "private, max-age=31536000";
                        context.Context.Response.Headers["Expires"] = DateTime.UtcNow.AddYears(1).ToString("R");
                    }
                }
            };
            // Static files (JS, CSS etc.) served directly.
            app.UseStaticFiles(sfo);
            // Serve our (single) .cshtml file, and serve API requests.
            app.UseMvc(routes =>
            {
                routes.MapRoute("api", "api/{action}/{*paras}", new { controller="Api", paras = "" });
                routes.MapRoute("default", "{*paras}", new { controller = "Index", action = "Index", paras = "" });
            });
        }
    }
}
