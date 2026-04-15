using System.Data;

namespace Provider.Interfaces
{
    public interface IKustoProvider
    {
        Task<List<T>> ReadAsync<T>(string query) where T : new();
        Task<List<T>> ReadAsync<T>(string functionName, IDictionary<string, object> parameters) where T : new();
        Task WriteAsync<T>(string functionName, IDictionary<string, object> parameters, IEnumerable<T> data);
        Task WriteAsync<T>(string tableName, IEnumerable<T> data);

        /// <summary>
        /// Executes a KQL query and returns results as a DataTable.
        /// Use this for dynamic queries where the schema is not known at compile time.
        /// </summary>
        Task<DataTable> ExecuteQueryAsync(string query);
    }
}