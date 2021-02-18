# FetchXML to Web API Converter

Convert FetchXML queries to Web API format. Requires a connection to a Dataverse instance to provide metadata for the conversion,
and the base URL of the Web API service.

```csharp
var converter = new FetchXmlToWebAPIConverter(metadata, url);
var query = @"
    <fetch>
        <entity name='account'>
            <attribute name='name' />
            <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='inner'>
                <attribute name='firstname' />
                <filter>
                    <condition attribute='firstname' operator='like' value='FXB%' />
                </filter>
            </link-entity>
        </entity>
    </fetch>
";
var converted = converter.ConvertFetchXmlToWebAPI(query);

// https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=contact_customer_accounts($select=firstname;$filter=(startswith(firstname, 'FXB')))&$filter=(contact_customer_accounts/any(o1:(startswith(o1%2ffirstname, 'FXB'))))
```

Not all FetchXML queries can be converted to Web API due to differences in the query structure. If the query cannot be converted
a `NotSupportedException` is thrown.

## Shared Project

The conversion routine is provided in a shared project to be compiled into other applications. Add this repo as a submodule, include
the shared project to your solution and then reference the project.

## Metadata Source

The metadata for the conversion is provided via the `IMetadataProvider` interface defined as part of this solution. The caller needs
to implement this interface. A naive implementation would be:

```csharp
class MetadataProvider : IMetadataProvider
{
    private IOrganizationService org;

    public MetadataProvider(IOrganizationService org)
    {
        this.org = org;
    }

    public bool IsConnected => true;

    public EntityMetadata GetEntity(string logicalName)
    {
        var resp = (RetrieveEntityResponse)org.Execute(new RetrieveEntityRequest { LogicalName = logicalName, EntityFilters = EntityFilters.Entity | EntityFilters.Attributes | EntityFilters.Relationships });
        return resp.EntityMetadata;
    }
}
```

However, retrieving the metadata is an expensive operation and many apps already implement local caching of metadata for other purposes,
so it is expected that the caller will include such caching in their implementations.