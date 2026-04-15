using Azure.Core;
using Azure.Identity;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Kusto.Ingest;
using Microsoft.Identity.Web;
using Newtonsoft.Json;
using Provider.Interfaces;
using Provider.Model;
using System.Data;
using System.Reflection;

namespace Provider
{


    public class KustoProvider : IKustoProvider
    {
        private readonly string kustoUri;
        private readonly string database;

        private readonly KustoConnectionStringBuilder kustoConnectionStringBuilder;

        public KustoProvider(string kustoUri, string database)
        {
            this.kustoUri = kustoUri;
            this.database = database;
#if DEBUG
                var credential = new DefaultAzureCredential();
#else
                var credential = new ManagedIdentityCredential();
#endif
                kustoConnectionStringBuilder = new KustoConnectionStringBuilder(kustoUri) {InitialCatalog=database }.WithAadAzureTokenCredentialsAuthentication(credential);

        }

        public KustoProvider(KustoDbConfig kustoDbConfig)
        {
            this.kustoUri = kustoDbConfig.KustoClusterUrl;
            this.database = kustoDbConfig.KustoDatabaseName;
            TokenCredential credential = new ManagedIdentityCredential();
#if DEBUG
            credential = new DefaultAzureCredential();
#else
            if (string.IsNullOrEmpty(kustoDbConfig?.CredentialConfig?.MuiClientId))
                 credential = new ManagedIdentityCredential();
            else
            {
                if (!string.IsNullOrEmpty(kustoDbConfig?.CredentialConfig?.AppClientId) && !string.IsNullOrEmpty(kustoDbConfig?.CredentialConfig?.TenantId))
                {
                    credential = new ClientAssertionCredential(
                            kustoDbConfig?.CredentialConfig?.TenantId,
                            kustoDbConfig?.CredentialConfig?.AppClientId,
                            async cancellationToken =>
                            {
                                var assertion = new ManagedIdentityClientAssertion(kustoDbConfig?.CredentialConfig?.MuiClientId);
                                return await assertion.GetSignedAssertionAsync(default);
                            });
                }
                else
                    credential = new ManagedIdentityCredential(kustoDbConfig?.CredentialConfig?.MuiClientId);
                    //kustoConnectionStringBuilder = new KustoConnectionStringBuilder(kustoUri)
                    //{
                    //    InitialCatalog = database
                    //}.WithAadUserManagedIdentity(kustoDbConfig?.CredentialConfig?.MuiClientId);

            }
#endif
            kustoConnectionStringBuilder = new KustoConnectionStringBuilder(kustoUri){ InitialCatalog=database }.WithAadAzureTokenCredentialsAuthentication(credential);

        }


        public async Task<List<T>> ReadAsync<T>(string query) where T : new()
        {
            TimeSpan timeout = TimeSpan.FromMinutes(10);
            using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kustoConnectionStringBuilder);
            var clientRequestProperties = new ClientRequestProperties();
            clientRequestProperties.SetOption(ClientRequestProperties.OptionServerTimeout, timeout);
            var reader = await queryProvider.ExecuteQueryAsync(database, query, clientRequestProperties);
            var kustoColumns = new HashSet<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                kustoColumns.Add(reader.GetName(i));
            }

            var results = new List<T>();
            while (reader.Read())
            {
                var instance = new T();
                foreach (PropertyInfo prop in typeof(T).GetProperties())
                {
                    var attr = prop.GetCustomAttribute<KustoColumnAttribute>();
                    if (attr != null)
                    {
                        var columnName = attr.ColumnName;
                        if ((kustoColumns.Contains(columnName)))
                        {
                            var value = reader[columnName];
                            if (value != DBNull.Value)
                            {
                                prop.SetValue(instance, value);
                            }
                        }

                    }
                }
                results.Add(instance);
            }
            return results;
        }

        public async Task<List<T>> ReadAsync<T>(string functionName, IDictionary<string, object> parameters) where T : new()
        {
            var query = $"{functionName}({string.Join(", ", parameters.Select(p => $"{p.Key}='{JsonConvert.SerializeObject(p.Value)}'"))})";
            return await ReadAsync<T>(query);
        }

        public async Task WriteAsync<T>(string tableName, IEnumerable<T> data)
        {
            using var ingestClient = KustoIngestFactory.CreateManagedStreamingIngestClient(kustoConnectionStringBuilder);
            var mappings = GetColumnMappings<T>();
            var tableData = data.Select(item => string.Join(",", mappings.Select(m => typeof(T).GetProperty(m.Key)!.GetValue(item)?.ToString() ?? "null")));

            var ingestProps = new KustoIngestionProperties(database, tableName)
            {
                Format = DataSourceFormat.csv,
                IngestionMapping = new IngestionMapping
                {
                    IngestionMappings = mappings.Select(m => new ColumnMapping { ColumnName = m.Value, ColumnType = "string" }).ToList()
                }
            };

            var dataString = string.Join("\n", tableData);
            await ingestClient.IngestFromStreamAsync(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(dataString)), ingestProps);
        }

        public async Task WriteAsync<T>(string functionName, IDictionary<string, object> parameters, IEnumerable<T> data)
        {
            var query = $"{functionName}({string.Join(", ", parameters.Select(p => $"{p.Key}='{p.Value}'"))})";
            await WriteAsync(query, data);
        }

        /// <inheritdoc />
        public async Task<DataTable> ExecuteQueryAsync(string query)
        {
            TimeSpan timeout = TimeSpan.FromMinutes(10);
            using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kustoConnectionStringBuilder);
            var clientRequestProperties = new ClientRequestProperties();
            clientRequestProperties.SetOption(ClientRequestProperties.OptionServerTimeout, timeout);

            var reader = await queryProvider.ExecuteQueryAsync(database, query, clientRequestProperties);

            var dataTable = new DataTable();
            dataTable.Load(reader);

            return dataTable;
        }

        private static Dictionary<string, string> GetColumnMappings<T>()
        {
            return typeof(T).GetProperties()
                .Where(p => p.GetCustomAttribute<KustoColumnAttribute>() != null)
                .ToDictionary(
                    p => p.Name,
                    p => p.GetCustomAttribute<KustoColumnAttribute>()!.ColumnName);
        }
    }

}
