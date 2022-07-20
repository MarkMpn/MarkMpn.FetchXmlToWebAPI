using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.UriParser;
using Microsoft.OData.UriParser.Aggregation;

namespace MarkMpn.FetchXmlToWebAPI
{
    public class WebAPIToFetchXmlConverter
    {
        private readonly IEdmModel _model;

        public WebAPIToFetchXmlConverter(IEdmModel model)
        {
            _model = model;
        }

        public string ConvertWebAPIToFetchXml(string odata)
        {
            var uri = new Uri(odata, UriKind.RelativeOrAbsolute);

            if (uri.IsAbsoluteUri)
            {
                var rootMatch = Regex.Match(odata, @"/api/data/v\d+.\d+/");
                uri = new Uri(odata.Substring(rootMatch.Index + rootMatch.Length), UriKind.Relative);
            }

            var parser = new ODataUriParser(_model, uri);
            var fetch = new FetchType();

            fetch.top = parser.ParseTop()?.ToString();

            var entity = ParseEntity(parser);
            fetch.Items = new object[] { entity };

            var items = ParseSelect(parser.ParseSelectAndExpand()).ToList();
            var filter = ParseFilter(parser.ParseFilter());
            var sorts = ParseSorts(parser.ParseOrderBy());
            var groupingsAndAggregates = ParseApply(parser.ParseApply(), entity).ToList();

            if (filter != null)
                items.Add(filter);

            items.AddRange(sorts);
            items.AddRange(groupingsAndAggregates);

            entity.Items = items
                .OrderBy(GetTypePreference)
                .ToArray();

            if (groupingsAndAggregates.Count > 0)
            {
                fetch.aggregate = true;
                fetch.aggregateSpecified = true;
            }

            var serializer = new XmlSerializer(typeof(FetchType));

            using (var writer = new StringWriter())
            {
                // Add in a separate namespace to remove the xsi and xsd namespaces added by default
                var xsn = new XmlSerializerNamespaces();
                xsn.Add("generator", "MarkMpn.WebAPIToFetchXmlConverter");

                serializer.Serialize(writer, fetch, xsn);
                return writer.ToString();
            }
        }

        private IEnumerable<object> ParseApply(ApplyClause applyClause, FetchEntityType entity)
        {
            if (applyClause == null)
                yield break;

            foreach (var transformation in applyClause.Transformations)
            {
                if (!(transformation is GroupByTransformationNode groupBy))
                    throw new FormatException("Unhandled transformation type");

                foreach (var grouping in groupBy.GroupingProperties)
                {
                    yield return new FetchAttributeType
                    {
                        name = grouping.Name,
                        alias = grouping.Name,
                        groupby = FetchBoolType.@true,
                        groupbySpecified = true
                    };
                }

                if (groupBy.ChildTransformations == null)
                    continue;

                if (!(groupBy.ChildTransformations is AggregateTransformationNode aggregate))
                    throw new FormatException("Unhandled transformation type");

                foreach (var child in aggregate.AggregateExpressions)
                {
                    if (!(child is AggregateExpression aggregateExpression))
                        throw new FormatException("Unhandled aggregate expression");

                    if (aggregateExpression.Expression is CountVirtualPropertyNode)
                    {
                        var rootEntity = _model.EntityContainer.EntitySets().Single(set => set.EntityType().Name == entity.name).EntityType();
            
                        yield return new FetchAttributeType
                        {
                            name = GetPrimaryKey(rootEntity),
                            alias = aggregateExpression.Alias,
                            aggregate = AggregateType.count,
                            aggregateSpecified = true
                        };
                    }
                }
            }
        }

        private IEnumerable<FetchOrderType> ParseSorts(OrderByClause orderByClause)
        {
            if (orderByClause == null)
                yield break;

            if (!(orderByClause.Expression is SingleValuePropertyAccessNode prop))
                throw new FormatException("Unhandled sort expression type");

            yield return new FetchOrderType
            {
                attribute = prop.Property.Name,
                descending = orderByClause.Direction == OrderByDirection.Descending,
            };

            foreach (var sort in ParseSorts(orderByClause.ThenBy))
                yield return sort;
        }

        private FetchEntityType ParseEntity(ODataUriParser parser)
        {
            var path = parser.ParsePath();
            if (path.Count != 1)
                throw new FormatException("Unhandled multiple path segments");

            if (!(path.FirstSegment is EntitySetSegment entitySet))
                throw new FormatException("Unhandled path segment type");

            if (!(entitySet.EdmType is EdmCollectionType type))
                throw new FormatException("Unhandled entity set type");

            if (!(type.ElementType.Definition is IEdmFullNamedElement element))
                throw new FormatException("Unhandled entity set element type");

            var entity = new FetchEntityType { name = element.Name };
            return entity;
        }

        private IEnumerable<object> ParseSelect(SelectExpandClause select)
        {
            var items = new List<object>();

            if (select == null)
                return items;

            if (select.AllSelected)
                items.Add(new allattributes());

            foreach (var segment in select.SelectedItems)
            {
                if (segment is ExpandedNavigationSelectItem expand)
                {
                    if (expand.PathToNavigationProperty.Count != 1)
                        throw new FormatException("Unhandled multiple expand segments");

                    if (!(expand.PathToNavigationProperty.FirstSegment is NavigationPropertySegment navigationProperty))
                        throw new FormatException("Unhandled navigation property type");

                    if (navigationProperty.EdmType is IEdmCollectionType collectionType)
                    {
                        if (!(collectionType.ElementType.Definition is IEdmEntityType entityType))
                            throw new FormatException("Unhandled expand element type");
                        
                        var linkEntity = new FetchLinkEntityType
                        {
                            name = entityType.Name,
                            from = SanitizeLookupProperty(navigationProperty.NavigationProperty.Partner.DependentProperties().Single().Name),
                            to = GetPrimaryKey(navigationProperty.NavigationProperty.DeclaringEntityType()),
                            linktype = "outer",
                            Items = ParseSelect(expand.SelectAndExpand).OrderBy(GetTypePreference).ToArray()
                        };

                        items.Add(linkEntity);
                    }
                    else if (navigationProperty.EdmType is IEdmEntityType entityType)
                    {
                        var linkEntity = new FetchLinkEntityType
                        {
                            name = entityType.Name,
                            from = GetPrimaryKey(entityType),
                            to = navigationProperty.Identifier,
                            linktype = "outer",
                            Items = ParseSelect(expand.SelectAndExpand).OrderBy(GetTypePreference).ToArray()
                        };

                        items.Add(linkEntity);
                    }
                    else
                    {
                        throw new FormatException("Unhandled navigation property target type");
                    }
                    continue;
                }

                if (!(segment is PathSelectItem pathSelect))
                    throw new FormatException("Unhandled select type");

                if (pathSelect.SelectedPath.Count != 1)
                    throw new FormatException("Unhandled multiple select segments");

                items.Add(new FetchAttributeType { name = pathSelect.SelectedPath.FirstSegment.Identifier });
            }

            return items;
        }

        private object ParseFilter(FilterClause filter)
        {
            var condition = ParseCondition(filter);

            if (condition is condition c)
                return new filter { Items = new object[] { c } };

            return condition;
        }

        private object ParseCondition(FilterClause filter)
        {
            if (filter == null || filter.Expression == null)
                return null;

            if (filter.Expression is BinaryOperatorNode op)
                return ParseCondition(op);

            return null;
        }

        private object ParseCondition(BinaryOperatorNode op)
        {
            if (op.Left is BinaryOperatorNode lhsOp &&
                op.Right is BinaryOperatorNode rhsOp &&
                (op.OperatorKind == BinaryOperatorKind.And || op.OperatorKind == BinaryOperatorKind.Or))
            {
                var lhsConverted = ParseCondition(lhsOp);
                var rhsConverted = ParseCondition(rhsOp);

                return new filter
                {
                    type = op.OperatorKind == BinaryOperatorKind.And ? filterType.and : filterType.or,
                    Items = new[]
                    {
                        lhsConverted,
                        rhsConverted
                    }
                };
            }

            var condition = new condition();

            var lhs = op.Left;
            if (lhs is ConvertNode convert)
                lhs = convert.Source;

            if (!(lhs is SingleValuePropertyAccessNode prop))
                throw new FormatException("Unhandled filter source");

            condition.attribute = prop.Property.Name;

            if (!(op.Right is ConstantNode value))
                throw new FormatException("Unhandled filter target");

            condition.value = value.Value.ToString();

            switch (op.OperatorKind)
            {
                case BinaryOperatorKind.Equal:
                    condition.@operator = @operator.eq;
                    break;

                default:
                    throw new FormatException("Unhandled filter operator");
            }

            return condition;
        }

        private string SanitizeLookupProperty(string name)
        {
            if (name.StartsWith("_"))
                name = name.Substring(1);

            if (name.EndsWith("_value"))
                name = name.Substring(0, name.Length - 6);

            return name;
        }

        private string GetPrimaryKey(IEdmEntityType entityType)
        {
            if (entityType.DeclaredKey.Count() != 1)
                throw new FormatException("Unhandled compound key");

            if (!(entityType.DeclaredKey.Single() is IEdmStructuralProperty keyProperty))
                throw new FormatException("Unhandled key property type");

            return keyProperty.Name;
        }

        private int GetTypePreference(object o)
        {
            if (o is allattributes)
                return 0;

            if (o is FetchAttributeType)
                return 1;

            if (o is FetchLinkEntityType)
                return 2;

            if (o is filter)
                return 3;

            if (o is condition)
                return 4;

            if (o is FetchOrderType)
                return 5;

            return 6;
        }
    }
}
