using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Middleware.Auth.Model
{
    using System.Diagnostics.CodeAnalysis;
    using System.Net;

    /// <summary>
    /// API Response Class
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ApiResponse<T>
    {
        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or Sets the return object.
        /// </summary>
        public T? Result { get; set; }

        public List<ApiError>? Errors { get; set; } = new List<ApiError>();

        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        public HttpStatusCode HttpStatusCode { get; set; }
    }
}
