using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HTMLBuilder
{
    public static class Query
    {
        private static IEnumerable<T> Validate<T>(IEnumerable<T> query, string? name, bool allowNone, bool allowMultiple, params (string attribute, string value)[] attributes)
        {
            if (!allowNone && !query.Any())
            {
                StringBuilder error = new(128);
                error.Append($"ERROR: Failed to find element ");
                if (name != null) error.Append($"'{name}' ");
                error.Append($"with attributes: {string.Join(", ", attributes.Select(a => $"{ a.attribute}= '{a.value}'"))}");
                throw new ArgumentException(error.ToString());
            }
            if (!allowMultiple && query.Count() > 1)
            {
                StringBuilder error = new(128);
                error.Append($"ERROR: Query for element ");
                if (name != null) error.Append($"'{name}' ");
                error.AppendLine($"with attributes: {string.Join(", ", attributes.Select(a => $"{ a.attribute}= '{a.value}'"))} was ambiguous between {query.Count()} results:");
                foreach (T conflict in query)
                {
                    switch (conflict)
                    {
                        case XElement e:
                            error.AppendLine($"\t{e.Name}: {string.Join(", ", e.Attributes().Select(a => $"{a.Name}={a.Value}"))}");
                            break;
                        case HtmlNode n:
                            error.AppendLine($"\t{n.Name}: {string.Join(", ", n.Attributes.Select(a => $"{a.Name}={a.Value}"))}");
                            break;
                    }
                }
                throw new ArgumentException(error.ToString());
            }
            return query;
        }

        private static IEnumerable<XElement> BuildXML(XElement root, string? name, params (string attribute, string value)[] attributes)
        {
            IEnumerable<XElement> query;
            if (name != null) query = root.Descendants(name);
            else query = root.Elements();

            foreach ((string attribute, string value) in attributes)
            {
                query = query.Where(e => e.Attribute(attribute)?.Value == value);
            }
            return query;
        }
        private static IEnumerable<HtmlNode> BuildHTML(HtmlNode root, string? name, params (string attribute, string value)[] attributes)
        {
            IEnumerable<HtmlNode> query;
            if (name != null) query = root.Descendants(name);
            else query = root.ChildNodes;

            foreach ((string attribute, string value) in attributes)
            {
                query = query.Where(e => e.GetAttributeValue(attribute, "") == value);
            }
            return query;
        }

        public static XElement ForSingle(XElement root, string? name, params (string attribute, string value)[] attributes)
        {
            return Validate(BuildXML(root, name, attributes), name, false, false, attributes).First();
        }
        public static HtmlNode ForSingle(HtmlNode root, string? name, params (string attribute, string value)[] attributes)
        {
            return Validate(BuildHTML(root, name, attributes), name, false, false, attributes).First();
        }
        public static XElement? ForSingleOrNone(XElement root, string? name, params (string attribute, string value)[] attributes)
        {
            return Validate(BuildXML(root, name, attributes), name, true, false, attributes).FirstOrDefault();
        }
        public static HtmlNode? ForSingleOrNone(HtmlNode root, string? name, params (string attribute, string value)[] attributes)
        {
            return Validate(BuildHTML(root, name, attributes), name, true, false, attributes).FirstOrDefault();
        }
        public static IEnumerable<XElement> ForMultiple(XElement root, string? name, params (string attribute, string value)[] attributes)
        {
            return Validate(BuildXML(root, name, attributes), name, false, true, attributes);
        }
        public static IEnumerable<HtmlNode> ForMultiple(HtmlNode root, string? name, params (string attribute, string value)[] attributes)
        {
            return Validate(BuildHTML(root, name, attributes), name, false, true, attributes);
        }
    }
}
