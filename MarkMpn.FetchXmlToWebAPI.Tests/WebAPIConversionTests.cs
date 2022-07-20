using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using FakeXrmEasy;
using Microsoft.OData.Edm.Csdl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk.Metadata;

namespace MarkMpn.FetchXmlToWebAPI.Tests
{
    [TestClass]
    public class WebAPIConversionTests
    {

        [TestMethod]
        public void SimpleQuery()
        {
            var odata = "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name";
            var expected = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                    </entity>
                </fetch>";

            var actual = ConvertODataToFetch(odata);

            AssertFetchXml(expected, actual);
        }

        [TestMethod]
        public void LeftOuterJoinParentLink()
        {
            var odata = "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=primarycontactid($select=firstname)";
            var expected = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='contactid' to='primarycontactid' link-type='outer'>
                            <attribute name='firstname' />
                        </link-entity>
                    </entity>
                </fetch>";

            var actual = ConvertODataToFetch(odata);

            AssertFetchXml(expected, actual);
        }

        [TestMethod]
        public void LeftOuterJoinChildLink()
        {
            var odata = "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=contact_customer_accounts($select=firstname)";
            var expected = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='outer'>
                            <attribute name='firstname' />
                        </link-entity>
                    </entity>
                </fetch>";

            var actual = ConvertODataToFetch(odata);

            AssertFetchXml(expected, actual);
        }

        [TestMethod]
        public void SimpleFilter()
        {
            var odata = "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(name eq 'FXB')";
            var expected = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='eq' value='FXB' />
                        </filter>
                    </entity>
                </fetch>";

            var actual = ConvertODataToFetch(odata);

            AssertFetchXml(expected, actual);
        }

        [TestMethod]
        public void NestedFilter()
        {
            var odata = "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(name eq 'FXB' and (websiteurl eq 'xrmtoolbox.com' or websiteurl eq 'fetchxmlbuilder.com'))";
            var expected = @"
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

            var actual = ConvertODataToFetch(odata);

            AssertFetchXml(expected, actual);
        }

        [TestMethod]
        public void Sort()
        {
            var odata = "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$orderby=name asc";
            var expected = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <order attribute='name' />
                    </entity>
                </fetch>";

            var actual = ConvertODataToFetch(odata);

            AssertFetchXml(expected, actual);
        }

        [TestMethod]
        public void Top()
        {
            var odata = "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$top=10";
            var expected = @"
                <fetch top='10'>
                    <entity name='account'>
                        <attribute name='name' />
                    </entity>
                </fetch>";

            var actual = ConvertODataToFetch(odata);

            AssertFetchXml(expected, actual);
        }

        [TestMethod]
        public void AggregateCount()
        {
            var odata = "https://example.crm.dynamics.com/api/data/v9.0/accounts?$apply=groupby((name),aggregate($count as count))";
            var expected = @"
                <fetch aggregate='true'>
                    <entity name='account'>
                        <attribute name='name' groupby='true' alias='name' />
                        <attribute name='accountid' aggregate='count' alias='count' />
                    </entity>
                </fetch>";

            var actual = ConvertODataToFetch(odata);

            AssertFetchXml(expected, actual);
        }

        private string ConvertODataToFetch(string odata)
        {
            var model = CsdlReader.Parse(XmlReader.Create(File.OpenRead(@"C:\Users\mark_\OneDrive\Documents\data8ltd.csdl")));
            var converter = new WebAPIToFetchXmlConverter(model);
            return converter.ConvertWebAPIToFetchXml(odata);
        }

        private void AssertFetchXml(string expected, string actual)
        {
            try
            {
                FetchType expectedFetch;
                FetchType actualFetch;

                var serializer = new XmlSerializer(typeof(FetchType));
                using (var reader = new StringReader(expected))
                {
                    expectedFetch = (FetchType)serializer.Deserialize(reader);
                }
                using (var reader = new StringReader(actual))
                {
                    actualFetch = (FetchType)serializer.Deserialize(reader);
                }

                PropertyEqualityAssert.Equals(expectedFetch, actualFetch);
            }
            catch (AssertFailedException ex)
            {
                Assert.Fail($"Expected:\r\n{expected}\r\n\r\nActual:\r\n{actual}\r\n\r\n{ex.Message}");
            }
        }
    }
}
