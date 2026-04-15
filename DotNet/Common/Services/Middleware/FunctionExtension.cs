using Microsoft.Azure.Functions.Worker;
using InfraService.Utilities;

namespace Middleware.Extension
{
    public static class FunctionExtension
    {
        /// <summary>
        /// Defines the HealthCheckApiHttpTriggerName.
        /// </summary>
        //public static readonly string[] HealthCheckApiHttpTriggerNames = { "HealthCheckHttpTrigger", "Function1" };
        /// <summary>
        /// The IsHttpTrigger.
        /// </summary>
        /// <param name="context">The context<see cref="FunctionContext"/>.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        /// 
        public static bool IsHttpTrigger(this FunctionContext context)
        {
            Requires.IsNotNull(context, nameof(context));
            return context.FunctionDefinition.InputBindings.Values.FirstOrDefault(a => a.Type.EndsWith("Trigger", StringComparison.CurrentCultureIgnoreCase))!.Type.Equals("httpTrigger", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsHealthCheckHttpTrigger(this FunctionContext context, string[] HealthCheckApiHttpTriggerNames)
        {
            Requires.IsNotNull(context, nameof(context));
            var funcName = context.FunctionDefinition.Name;
            return HealthCheckApiHttpTriggerNames.Any(name => funcName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
