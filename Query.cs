using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PortfolioProjectHTMLInserter
{
    public static class Query
    {
        static bool Validate<T>(IEnumerable<T> query, string name, bool allowMultiple)
        {
            if (!query.Any())
            {
                Console.WriteLine($"ERROR: Failed to find element '{name}'");
                return false;
            }
            else if (!allowMultiple && query.Count() > 1)
            {
                StringBuilder output = new(64 + 24 * query.Count());
                output.AppendLine($"ERROR: Query '{name}' was ambiguous with {query.Count()} results:");
                foreach(T element in query)
                {
                    switch (element)
                    {
                        case XElement e:
                            output.AppendLine($"\t{e.Name} = {e.Value}");
                            break;
                        case HtmlNode n:
                            output.AppendLine($"\t{n.Name}");
                            break;
                    }
                }
                return false;
            }
            return true;
        }
        static bool Validate<T>(IEnumerable<T> query, string name, string id, bool allowMultiple)
        {
            if (!query.Any())
            {
                Console.WriteLine($"ERROR: Failed to find element '{name}' with id '{id}'");
                return false;
            }
            else if (!allowMultiple && query.Count() > 1)
            {
                Console.WriteLine($"ERROR: Query '{name}' with id '{id}' was ambiguous with {query.Count()} results.");
                return false;
            }
            return true;
        }

        public static bool Perform(XElement root, string name, out IEnumerable<XElement> query, bool allowMultiple = false)
        {
            query = from element in root.Elements(name) select element;
            return Validate(query, name, allowMultiple);
        }

        public static bool Perform(XElement root, string name, string id, out IEnumerable<XElement> query, bool allowMultiple = false)
        {
            query = from element in root.Elements(name) where (string?)element.Attribute("id") == id select element;
            return Validate(query, name, id, allowMultiple);
        }
        public static bool Perform(HtmlDocument root, string name, out IEnumerable<HtmlNode> query, bool allowMultiple = false)
        {
            query = from node in root.DocumentNode.Descendants(name) select node;
            return Validate(query, name, allowMultiple);
        }
        public static bool Perform(HtmlDocument root, string name, string id, out IEnumerable<HtmlNode> query, bool allowMultiple = false)
        {
            query = from node in root.DocumentNode.Descendants(name) where node.Attributes["id"]?.Value == id select node;
            return Validate(query, name, id, allowMultiple);
        }
    }
}
