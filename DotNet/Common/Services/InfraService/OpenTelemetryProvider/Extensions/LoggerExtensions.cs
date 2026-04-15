namespace InfraService.OpenTelemetryProvider.Extensions
{
    using Microsoft.Extensions.Logging;
    using System.Runtime.CompilerServices;
    using System.Text;

    public static class LoggerExtensions
    {
        /// <summary>
        /// Formats and writes a debug log message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="exception">The exception to log.</param>
        /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        /// <example>logger.LogDebug(exception, "Error while processing request from {Address}", address)</example>
        public static void AppLogDebug(this ILogger logger, Exception? exception, string? message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", string CaseNumber="",string CustomerResourceId="",string LocationId="",string HttpStatusCode="",string Status="", string IncidentId = "")
        {

            message = message?.Replace("{", "{{").Replace("}", "}}");
            (message, var updatedArgs) = UpdateMessageWithClassNameMemberName(message, memberName, sourceFilePath, CaseNumber, CustomerResourceId, LocationId, HttpStatusCode, Status, IncidentId);
            logger.LogDebug(exception, message, updatedArgs);
        }

        /// <summary>
        /// Formats and writes a debug log message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        /// <example>logger.LogDebug("Processing request from {Address}", address)</example>
        public static void AppLogDebug(this ILogger logger, string? message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", string CaseNumber = "", string CustomerResourceId = "", string LocationId = "", string HttpStatusCode = "", string Status = "", string IncidentId = "")
        {

            message = message?.Replace("{", "{{").Replace("}", "}}");
            (message, var updatedArgs) = UpdateMessageWithClassNameMemberName(message, memberName, sourceFilePath, CaseNumber, CustomerResourceId, LocationId, HttpStatusCode, Status, IncidentId);
            logger.LogDebug(message, updatedArgs);
        }

        /// <summary>
        /// Formats and writes an informational log message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        /// <example>logger.LogInformation("Processing request from {Address}", args: new [ address ])</example>
        public static void AppLogInformation(this ILogger logger, string? message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", string CaseNumber = "", string CustomerResourceId = "", string LocationId = "", string HttpStatusCode = "", string Status = "", string IncidentId = "")
        {

            message = message?.Replace("{", "{{").Replace("}", "}}");
            (message, var updatedArgs) = UpdateMessageWithClassNameMemberName(message, memberName, sourceFilePath, CaseNumber, CustomerResourceId, LocationId, HttpStatusCode, Status, IncidentId);
            logger.LogInformation(message, updatedArgs);
        }

        /// <summary>
        /// Formats and writes an informational log message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="exception">The exception to log.</param>
        /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        /// <example>logger.LogInformation(exception, "Error while processing request from {Address}", args: new [ address ])</example>
        public static void AppLogInformation(this ILogger logger, Exception? exception, string? message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", string CaseNumber = "", string CustomerResourceId = "", string LocationId = "", string HttpStatusCode = "", string Status = "", string IncidentId = "")
        {

            message = message?.Replace("{", "{{").Replace("}", "}}");
            (message, var updatedArgs) = UpdateMessageWithClassNameMemberName(message, memberName, sourceFilePath, CaseNumber, CustomerResourceId, LocationId, HttpStatusCode, Status, IncidentId);
            logger.LogInformation(exception, message, updatedArgs);
        }

        /// <summary>
        /// Formats and writes a warning log message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="exception">The exception to log.</param>
        /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        /// <example>logger.LogWarning(exception, "Error while processing request from {Address}", args: new [ address ])</example>
        public static void AppLogWarning(this ILogger logger, Exception? exception, string? message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", string CaseNumber = "", string CustomerResourceId = "", string LocationId = "", string HttpStatusCode = "", string Status = "", string IncidentId = "")
        {

            message = message?.Replace("{", "{{").Replace("}", "}}");
            (message, var updatedArgs) = UpdateMessageWithClassNameMemberName(message, memberName, sourceFilePath, CaseNumber, CustomerResourceId, LocationId, HttpStatusCode, Status, IncidentId);
            logger.LogWarning(exception, message, updatedArgs);
        }

        /// <summary>
        /// Formats and writes a warning log message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        /// <example>logger.LogWarning("Processing request from {Address}", args: new [ address ])</example>
        public static void AppLogWarning(this ILogger logger, string? message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", string CaseNumber = "", string CustomerResourceId = "", string LocationId = "", string HttpStatusCode = "", string Status = "", string IncidentId = "")
        {

            message = message?.Replace("{", "{{").Replace("}", "}}");
            (message, var updatedArgs) = UpdateMessageWithClassNameMemberName(message, memberName, sourceFilePath, CaseNumber, CustomerResourceId, LocationId, HttpStatusCode, Status, IncidentId);
            logger.LogWarning(message, updatedArgs);
        }

        /// <summary>
        /// Formats and writes an error log message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="exception">The exception to log.</param>
        /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        /// <example>logger.LogError(exception, "Error while processing request from {Address}", args: new [ address ])</example>
        public static void AppLogError(this ILogger logger, Exception? exception, string? message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", string CaseNumber = "", string CustomerResourceId = "", string LocationId = "", string HttpStatusCode = "", string Status = "", string IncidentId = "")
        {

            message = message?.Replace("{", "{{").Replace("}", "}}");
            (message, var updatedArgs) = UpdateMessageWithClassNameMemberName(message, memberName, sourceFilePath, CaseNumber, CustomerResourceId, LocationId, HttpStatusCode, Status, IncidentId);
            logger.LogError(exception, message, updatedArgs);
        }

        /// <summary>
        /// Formats and writes an error log message.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to write to.</param>
        /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        /// <example>logger.LogError("Processing request from {Address}", args: new [ address ])</example>
        public static void AppLogError(this ILogger logger, string? message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", string CaseNumber = "", string CustomerResourceId = "", string LocationId = "", string HttpStatusCode = "", string Status = "", string IncidentId = "")
        {

            message = message?.Replace("{", "{{").Replace("}", "}}");
            (message, var updatedArgs) = UpdateMessageWithClassNameMemberName(message, memberName, sourceFilePath, CaseNumber, CustomerResourceId, LocationId, HttpStatusCode, Status, IncidentId);
            logger.LogError(message, updatedArgs);
        }


        private static (string,object[]) UpdateMessageWithClassNameMemberName(string? message, string OperationName,  string ClassName, string CaseNumber, string CustomerResourceId , string LocationId , string HttpStatusCode , string Status, string IncidentId)
        {
            var args =new List<object>();
            var messageFormat = new StringBuilder();
            if (ClassName != "")
                { messageFormat.Append($"{{{nameof(ClassName)}}}");
                var cn=Path.GetFileNameWithoutExtension(ClassName);
                args.Add(cn);
            }
            if (OperationName!="")
                { messageFormat.Append($".{{{nameof(OperationName)}}}");  args.Add(OperationName);}
            if (CaseNumber != "")
                { messageFormat.Append($".{{{nameof(CaseNumber)}}}");  args.Add(CaseNumber);}
            if (CustomerResourceId != "")
                { messageFormat.Append($".{{{nameof(CustomerResourceId)}}}"); args.Add(CustomerResourceId); } 
            if (LocationId != "")
                { messageFormat.Append($".{{{nameof(LocationId)}}}"); args.Add(LocationId); }
            if (HttpStatusCode != "")
                { messageFormat.Append($".{{{nameof(HttpStatusCode)}}}"); args.Add(HttpStatusCode); }
            if (Status != "")
                { messageFormat.Append($".{{{nameof(Status)}}}"); args.Add(Status); }
            if (IncidentId != "")
                { messageFormat.Append($".{{{nameof(IncidentId)}}}"); args.Add(IncidentId); }
            messageFormat.Append($": {message}");
            return (messageFormat.ToString(),args.ToArray());
        }

        
    }
}
