namespace InfraService.OpenTelemetryProvider
{
    public static class LoggingUtility
    {
        /// <summary>
        /// Get Function Logging Properties.
        /// </summary>
        /// <param name="functionName">function Name.</param>
        /// <returns>The KeyValuePair"/>.</returns>
        public static List<KeyValuePair<string, object>> GetFunctionScopeProperties(string functionName)
        {
            var list = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("FunctionName", functionName),
            };

            return list;
        }
    }
}
