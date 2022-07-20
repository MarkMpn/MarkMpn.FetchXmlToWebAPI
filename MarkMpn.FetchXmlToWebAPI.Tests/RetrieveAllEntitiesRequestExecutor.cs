using System;
using FakeXrmEasy;
using FakeXrmEasy.FakeMessageExecutors;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace MarkMpn.FetchXmlToWebAPI.Tests
{
    internal class RetrieveAllEntitiesRequestExecutor : IFakeMessageExecutor
    {
        private readonly EntityMetadata[] _entities;

        public RetrieveAllEntitiesRequestExecutor(EntityMetadata[] entities)
        {
            _entities = entities;
        }

        public bool CanExecute(OrganizationRequest request)
        {
            return request is RetrieveAllEntitiesRequest;
        }

        public OrganizationResponse Execute(OrganizationRequest request, XrmFakedContext ctx)
        {
            return new RetrieveAllEntitiesResponse
            {
                Results = new ParameterCollection
                {
                    ["EntityMetadata"] = _entities
                }
            };
        }

        public Type GetResponsibleRequestType()
        {
            return typeof(RetrieveAllEntitiesRequest);
        }
    }
}