namespace Provider.Model
{
    /// <summary>
    /// Represents the configuration for an Azure Search connection.
    /// </summary>
    public class AzureSearchConnectionConfig
    {
        /// <summary>
        /// Gets or sets the Azure Search service endpoint.
        /// </summary>
        public string SearchEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the Managed Identity Client ID (optional).
        /// </summary>
        public string? MuiClientId { get; set; }

        /// <summary>
        /// Gets or sets the Azure Search index name.
        /// </summary>
        public string SearchIndex { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSearchConnectionConfig"/> class.
        /// </summary>
        

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureSearchConnectionConfig"/> class with specified values.
        /// </summary>
        /// <param name="searchEndpoint">The Azure Search service endpoint.</param>
        /// <param name="searchIndex">The Azure Search index name.</param>
        /// <param name="muiClientId">The Managed Identity Client ID (optional).</param>
        public AzureSearchConnectionConfig(string searchEndpoint, string searchIndex, string? muiClientId = null)
        {
            SearchEndpoint = searchEndpoint;
            SearchIndex = searchIndex;
            MuiClientId = muiClientId;
        }
    }
}
