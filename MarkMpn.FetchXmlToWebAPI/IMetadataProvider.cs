using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xrm.Sdk.Metadata;

namespace MarkMpn.FetchXmlToWebAPI
{
    /// <summary>
    /// Provides metadata for the <see cref="FetchXmlToWebAPIConverter"/>
    /// </summary>
    public interface IMetadataProvider
    {
        /// <summary>
        /// Indicates if a connection to CRM is available
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets the metadata for an entity
        /// </summary>
        /// <param name="logicalName">The logical name of the entity to get the metadata of</param>
        /// <returns>The metadata of the requested entity</returns>
        /// <remarks>
        /// If the <paramref name="logicalName"/> is not valid, this method throws an exception
        /// </remarks>
        EntityMetadata GetEntity(string logicalName);

        /// <summary>
        /// Gets the metadata for an entity
        /// </summary>
        /// <param name="otc">The object type code of the entity to get the metadata of</param>
        /// <returns>The metadata of the requested entity</returns>
        /// <remarks>
        /// If the <paramref name="otc"/> is not valid, this method throws an exception
        /// </remarks>
        EntityMetadata GetEntity(int otc);
    }
}
