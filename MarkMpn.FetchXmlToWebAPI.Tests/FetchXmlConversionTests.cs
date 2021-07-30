﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using FakeXrmEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk.Metadata;

namespace MarkMpn.FetchXmlToWebAPI.Tests
{
    [TestClass]
    public class FetchXmlConversionTests
    {

        [TestMethod]
        public void SimpleQuery()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name", odata);
        }

        [TestMethod]
        public void LeftOuterJoinParentLink()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='contactid' to='primarycontactid' link-type='outer'>
                            <attribute name='firstname' />
                        </link-entity>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=primarycontactid($select=firstname)", odata);
        }

        [TestMethod]
        public void LeftOuterJoinChildLink()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='outer'>
                            <attribute name='firstname' />
                        </link-entity>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=contact_customer_accounts($select=firstname)", odata);
        }

        [TestMethod]
        public void SimpleFilter()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='eq' value='FXB' />
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(name eq 'FXB')", odata);
        }

        [TestMethod]
        public void NestedFilter()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='eq' value='FXB' />
                            <filter type='or'>
                                <condition attribute='websiteurl' operator='eq' value='xrmtoolbox.com' />
                                <condition attribute='websiteurl' operator='eq' value='fetchxmlbuilder.com' />
                            </filter>
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(name eq 'FXB' and (websiteurl eq 'xrmtoolbox.com' or websiteurl eq 'fetchxmlbuilder.com'))", odata);
        }

        [TestMethod]
        public void Sort()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <order attribute='name' />
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$orderby=name asc", odata);
        }

        [TestMethod]
        public void Top()
        {
            var fetch = @"
                <fetch top='10'>
                    <entity name='account'>
                        <attribute name='name' />
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$top=10", odata);
        }

        [TestMethod]
        public void AggregateCount()
        {
            var fetch = @"
                <fetch aggregate='true'>
                    <entity name='account'>
                        <attribute name='name' groupby='true' alias='name' />
                        <attribute name='accountid' aggregate='count' alias='count' />
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$apply=groupby((name),aggregate($count as count))", odata);
        }

        [TestMethod]
        public void AggregateMax()
        {
            var fetch = @"
                <fetch aggregate='true'>
                    <entity name='account'>
                        <attribute name='name' groupby='true' alias='name' />
                        <attribute name='websiteurl' aggregate='max' alias='maxwebsite' />
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$apply=groupby((name),aggregate(websiteurl with max as maxwebsite))", odata);
        }

        [TestMethod]
        public void InnerJoinParentLink()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='contactid' to='primarycontactid' link-type='inner'>
                            <attribute name='firstname' />
                        </link-entity>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=primarycontactid($select=firstname)&$filter=(primarycontactid/contactid ne null)", odata);
        }

        [TestMethod]
        public void InnerJoinParentLinkWithFilter()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='contactid' to='primarycontactid' link-type='inner'>
                            <attribute name='firstname' />
                            <filter>
                                <condition attribute='firstname' operator='eq' value='Mark' />
                            </filter>
                        </link-entity>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=primarycontactid($select=firstname)&$filter=(primarycontactid/firstname eq 'Mark')", odata);
        }

        [TestMethod]
        public void InnerJoinParentLinkWithComplexFilter()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='contactid' to='primarycontactid' link-type='inner'>
                            <attribute name='firstname' />
                            <filter>
                                <condition attribute='createdon' operator='on' value='2020-01-01' />
                            </filter>
                        </link-entity>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=primarycontactid($select=firstname)&$filter=(primarycontactid/Microsoft.Dynamics.CRM.On(PropertyName='createdon',PropertyValue='2020-01-01'))", odata);
        }

        [TestMethod]
        public void InnerJoinChildLink()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='inner'>
                            <attribute name='firstname' />
                        </link-entity>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=contact_customer_accounts($select=firstname)&$filter=(contact_customer_accounts/any(o1:(o1/contactid ne null)))", odata);
        }

        [TestMethod]
        public void InnerJoinChildLinkWithFilter()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='inner'>
                            <attribute name='firstname' />
                            <filter>
                                <condition attribute='firstname' operator='eq' value='Mark' />
                            </filter>
                        </link-entity>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=contact_customer_accounts($select=firstname;$filter=(firstname eq 'Mark'))&$filter=(contact_customer_accounts/any(o1:(o1/firstname eq 'Mark')))", odata);
        }

        [TestMethod]
        public void InnerJoinChildLinkWithComplexFilter()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='inner'>
                            <attribute name='firstname' />
                            <filter>
                                <condition attribute='createdon' operator='on' value='2020-01-01' />
                            </filter>
                        </link-entity>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=contact_customer_accounts($select=firstname;$filter=(Microsoft.Dynamics.CRM.On(PropertyName='createdon',PropertyValue='2020-01-01')))&$filter=(contact_customer_accounts/any(o1:(o1/Microsoft.Dynamics.CRM.On(PropertyName='createdon',PropertyValue='2020-01-01'))))", odata);
        }

        [TestMethod]
        public void FilterPrefix()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='like' value='FXB%' />
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(startswith(name, 'FXB'))", odata);
        }

        [TestMethod]
        public void InnerJoinChildLinkWithPrefixFilter()
        {
            var fetch = @"
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
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=contact_customer_accounts($select=firstname;$filter=(startswith(firstname, 'FXB')))&$filter=(contact_customer_accounts/any(o1:(startswith(o1%2ffirstname, 'FXB'))))", odata);
        }

        [TestMethod]
        public void FilterSuffix()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='like' value='%FXB' />
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(endswith(name, 'FXB'))", odata);
        }

        [TestMethod]
        public void FilterContains()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='like' value='%FXB%' />
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(contains(name, 'FXB'))", odata);
        }

        [TestMethod]
        public void FilterPrefixEscaped()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='like' value='[[]FXB%' />
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(startswith(name, '%5bFXB'))", odata);
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void FilterComplexWildcard()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='like' value='%F_XB%' />
                        </filter>
                    </entity>
                </fetch>";

            ConvertFetchToOData(fetch);
        }

        [TestMethod]
        public void FilterOnEntityName()
        {
            var fetch = @"
                <fetch>
                    <entity name='connection'>
                        <attribute name='connectionid' />
                        <filter>
                            <condition attribute='record1idobjecttypecode' operator='eq' value='8' />
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/connections?$select=connectionid&$filter=(record1idobjecttypecode eq 8)", odata);
        }

        [TestMethod]
        public void FilterOnOptionSet()
        {
            var fetch = @"
                <fetch>
                    <entity name='connection'>
                        <attribute name='connectionid' />
                        <filter>
                            <condition attribute='record1objecttypecode' operator='eq' value='8' />
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/connections?$select=connectionid&$filter=(record1objecttypecode eq 8)", odata);
        }

        [TestMethod]
        public void FilterOnManagedProperty()
        {
            var fetch = @"
                <fetch>
                    <entity name='webresource'>
                        <attribute name='name' />
                        <attribute name='iscustomizable' />
                        <filter>
                            <condition attribute='iscustomizable' operator='eq' value='1' />
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/webresourceset?$select=name,iscustomizable&$filter=(iscustomizable/Value eq true)", odata);
        }

        private string ConvertFetchToOData(string fetch)
        {
            var context = new XrmFakedContext();

            // Add basic metadata
            var relationships = new[]
            {
                new OneToManyRelationshipMetadata
                {
                    SchemaName = "contact_customer_accounts",
                    ReferencedEntity = "account",
                    ReferencedAttribute = "accountid",
                    ReferencingEntity = "contact",
                    ReferencingAttribute = "parentcustomerid"
                },
                new OneToManyRelationshipMetadata
                {
                    SchemaName = "account_primarycontact",
                    ReferencedEntity = "contact",
                    ReferencedAttribute = "contactid",
                    ReferencingEntity = "account",
                    ReferencingAttribute = "primarycontactid"
                }
            };

            var entities = new[]
            {
                new EntityMetadata
                {
                    LogicalName = "account",
                    EntitySetName = "accounts"
                },
                new EntityMetadata
                {
                    LogicalName = "contact",
                    EntitySetName = "contacts"
                },
                new EntityMetadata
                {
                    LogicalName = "connection",
                    EntitySetName = "connections"
                },
                new EntityMetadata
                {
                    LogicalName = "webresource",
                    EntitySetName = "webresourceset"
                }
            };

            var attributes = new Dictionary<string, AttributeMetadata[]>
            {
                ["account"] = new AttributeMetadata[]
                {
                    new UniqueIdentifierAttributeMetadata
                    {
                        LogicalName = "accountid"
                    },
                    new StringAttributeMetadata
                    {
                        LogicalName = "name"
                    },
                    new StringAttributeMetadata
                    {
                        LogicalName = "websiteurl"
                    },
                    new LookupAttributeMetadata
                    {
                        LogicalName = "primarycontactid",
                        Targets = new[] { "contact" }
                    }
                },
                ["contact"] = new AttributeMetadata[]
                {
                    new UniqueIdentifierAttributeMetadata
                    {
                        LogicalName = "contactid"
                    },
                    new StringAttributeMetadata
                    {
                        LogicalName = "firstname"
                    },
                    new LookupAttributeMetadata
                    {
                        LogicalName = "parentcustomerid",
                        Targets = new[] { "account", "contact" }
                    },
                    new DateTimeAttributeMetadata
                    {
                        LogicalName = "createdon"
                    }
                },
                ["connection"] = new AttributeMetadata[]
                {
                    new UniqueIdentifierAttributeMetadata
                    {
                        LogicalName = "connectionid"
                    },
                    new EntityNameAttributeMetadata
                    {
                        LogicalName = "record1idobjecttypecode"
                    },
                    new PicklistAttributeMetadata
                    {
                        LogicalName = "record1objecttypecode"
                    }
                },
                ["webresource"] = new AttributeMetadata[]
                {
                    new UniqueIdentifierAttributeMetadata
                    {
                        LogicalName = "webresourceid"
                    },
                    new StringAttributeMetadata
                    {
                        LogicalName = "name"
                    },
                    new ManagedPropertyAttributeMetadata
                    {
                        LogicalName = "iscustomizable"
                    }
                }
            };

            SetSealedProperty(attributes["webresource"].Single(a => a.LogicalName == "iscustomizable"), nameof(ManagedPropertyAttributeMetadata.ValueAttributeTypeCode), AttributeTypeCode.Boolean);
            SetRelationships(entities, relationships);
            SetAttributes(entities, attributes);

            foreach (var entity in entities)
                context.SetEntityMetadata(entity);

            var org = context.GetOrganizationService();
            var converter = new FetchXmlToWebAPIConverter(new MetadataProvider(org), $"https://example.crm.dynamics.com/api/data/v9.0");
            return converter.ConvertFetchXmlToWebAPI(fetch);
        }

        private void SetAttributes(EntityMetadata[] entities, Dictionary<string, AttributeMetadata[]> attributes)
        {
            foreach (var entity in entities)
            {
                SetSealedProperty(entity, nameof(EntityMetadata.PrimaryIdAttribute), attributes[entity.LogicalName].OfType<UniqueIdentifierAttributeMetadata>().Single().LogicalName);
                SetSealedProperty(entity, nameof(EntityMetadata.Attributes), attributes[entity.LogicalName]);
            }
        }

        private void SetRelationships(EntityMetadata[] entities, OneToManyRelationshipMetadata[] relationships)
        {
            foreach (var relationship in relationships)
            {
                relationship.ReferencingEntityNavigationPropertyName = relationship.ReferencingAttribute;
                relationship.ReferencedEntityNavigationPropertyName = relationship.SchemaName;
            }

            foreach (var entity in entities)
            {
                var oneToMany = relationships.Where(r => r.ReferencedEntity == entity.LogicalName).ToArray();
                var manyToOne = relationships.Where(r => r.ReferencingEntity == entity.LogicalName).ToArray();

                SetSealedProperty(entity, nameof(EntityMetadata.OneToManyRelationships), oneToMany);
                SetSealedProperty(entity, nameof(EntityMetadata.ManyToOneRelationships), manyToOne);
            }
        }

        private void SetSealedProperty(object target, string name, object value)
        {
            var prop = target.GetType().GetProperty(name);
            prop.SetValue(target, value, null);
        }
    }
}
