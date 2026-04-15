using Azure.Search.Documents.Models;
using Azure.Search.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure;

namespace Provider.Interfaces
{
    public interface IAzureSearchProvider
    {


        /// <summary>
        /// Look Up all the documents with the searchText.
        /// </summary>
        /// <param name="searchText">WildCard to search.</param>
        /// <param name="searchOptions">Search Options asc,desc .</param>
        /// <returns>The <see cref="Task{SearchResults}"/>.</returns>
        Task<SearchResults<T>> SearchDocumentsByFilterAsync<T>( string searchText, SearchOptions searchOptions = null!);


        /// <summary>
        /// Upload the Documents to Search Index.
        /// </summary>
        /// <param name="documents">Documents.</param>
        /// <param name="options">Document Options.</param>
        /// <returns>The <see cref="Task{IndexDocumentsResult}"/>.</returns>
        Task<IndexDocumentsResult> MergeOrUploadDocument<T>( List<T> documents, IndexDocumentsOptions options = null!);

        /// <summary>
        /// Delete documents from the Search Index based on the key.
        /// </summary>
        /// <param name="documentKeyFieldName">The name of the document key field.</param>
        /// <param name="documentKeys">The keys of the documents to delete.</param>
        /// <returns>The <see cref="Task{IndexDocumentsResult}"/>.</returns>
        Task<IndexDocumentsResult> DeleteDocumentByKeyAsync(string documentKeyFieldName, IEnumerable<string> documentKeys);

        /// <summary>
        /// Search documents using only SearchOptions (filter-only and vector search).
        /// </summary>
        /// <typeparam name="T">The document type.</typeparam>
        /// <param name="searchOptions">Search options including filters and vector queries.</param>
        /// <returns>The search results.</returns>
        Task<SearchResults<T>> SearchAsync<T>(SearchOptions searchOptions);
    }
}

