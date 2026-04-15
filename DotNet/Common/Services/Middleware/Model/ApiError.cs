namespace Middleware.Auth.Model
{
    public class ApiError
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiError"/> class.
        /// </summary>
        public ApiError()
        {
            Field = "System";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiError"/> class.
        /// </summary>
        /// <param name="message">String error message.</param>
        public ApiError(string message)
        {
            Message = message;
            Field = "System";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiError"/> class.
        /// </summary>
        /// <param name="message">String error message.</param>
        /// <param name="field">String field name.</param>
        public ApiError(string message, string field)
        {
            Message = message;
            Field = field;
        }

        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string? Code { get; set; }

        /// <summary>
        /// Gets or sets the the field/area associated with the error.
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// Gets or sets the the error message.
        /// </summary>
        public string? Message { get; set; }
    }
}
