using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace WEB_Sentro.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred.");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var isAjaxOrApi = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                              context.Request.Path.StartsWithSegments("/api") ||
                              context.Request.Headers["Accept"].ToString().Contains("application/json");

            if (isAjaxOrApi)
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                var response = new { error = "An unexpected error occurred. Please try again later." };
                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
            else
            {
                // For non-AJAX requests, we can redirect to the error page
                // But typically app.UseExceptionHandler("/Home/Error") handles this if we rethrow.
                // However, since we catch it here, we should redirect manually if not in dev.
                context.Response.Redirect("/Home/Error");
            }
        }
    }
}
