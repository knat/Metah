using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Metah.Compilation;

namespace Test {
    class Program {
        static void CreateXTokens() {
            using (var writer = File.CreateText(@"d:\test0.txt")) {
                foreach (var i in Enum.GetNames(typeof(XTokenKind)))
                    writer.WriteLine("internal static readonly Node {0}Kind = Node.Atom(XTokenKind.{0});", i);
            }
            using (var writer = File.CreateText(@"d:\test1.txt")) {
                foreach (var i in Enum.GetValues(typeof(XTokenKind)).Cast<XTokenKind>().Where(i => (int)i >= 21000))
                    writer.WriteLine(@"c{0} returns[Node nd]:{{input.LT(1).Text==""{1}""}}? t=IdentifierToken{{nd = t.CloneNode(kindNode: XTokens.{0}Kind, label: NodeExtensions.XTokenLabel);}};", i, XTokens.GetText(i));
            }
            using (var writer = File.CreateText(@"d:\test2.txt")) {
                foreach (var i in Enum.GetValues(typeof(XTokenKind)).Cast<XTokenKind>().Where(i => (int)i < 21000))
                    writer.WriteLine(@"{0}: '{1}';", i, XTokens.GetText(i));
                writer.WriteLine();
                foreach (var i in Enum.GetValues(typeof(XTokenKind)).Cast<XTokenKind>().Where(i => (int)i < 21000))
                    writer.WriteLine(@"            {{{0}, XTokens.{0}Kind}},", i);
                writer.WriteLine();
                foreach (var i in Enum.GetValues(typeof(XTokenKind)).Cast<XTokenKind>().Where(i => (int)i < 21000))
                    writer.WriteLine(@"            {{""{0}"", @""{1}""}},", i, XTokens.GetText(i));

            }
        }
        static void CreateWTokens() {
            using (var writer = File.CreateText(@"test0.txt")) {
                foreach (var i in Enum.GetNames(typeof(WTokenKind)))
                    writer.WriteLine("internal static readonly Node {0}Kind = Node.Atom(WTokenKind.{0});", i);
            }
            using (var writer = File.CreateText(@"test1.txt")) {
                foreach (var i in Enum.GetValues(typeof(WTokenKind)).Cast<WTokenKind>().Where(i => (int)i >= 31000))
                    writer.WriteLine(@"c{0} returns[Node nd]:{{input.LT(1).Text==""{1}""}}? t=IdentifierToken{{nd = t.CloneNode(kindNode: WTokens.{0}Kind, label: NodeExtensions.WTokenLabel);}};", i, WTokens.GetText(i));
            }
            using (var writer = File.CreateText(@"test2.txt")) {
                foreach (var i in Enum.GetValues(typeof(WTokenKind)).Cast<WTokenKind>().Where(i => (int)i < 31000))
                    writer.WriteLine(@"{0}: '{1}';", i, WTokens.GetText(i));
                writer.WriteLine();
                foreach (var i in Enum.GetValues(typeof(WTokenKind)).Cast<WTokenKind>().Where(i => (int)i < 31000))
                    writer.WriteLine(@"            {{{0}, WTokens.{0}Kind}},", i);
                writer.WriteLine();
                foreach (var i in Enum.GetValues(typeof(WTokenKind)).Cast<WTokenKind>().Where(i => (int)i < 31000))
                    writer.WriteLine(@"            {{""{0}"", @""{1}""}},", i, WTokens.GetText(i));

            }
        }
        static void Main(string[] args) {
            //CreateXTokens();
            CreateWTokens();
        }
    }
}
