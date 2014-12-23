using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Metah.X;
using Metah.X.Extensions;
using MX = Metah.X;
using SType = System.Type;

namespace Metah.Compilation.X {
    public abstract class Object : ObjectBase {
        protected Object() { }
        private Analyzer _analyzerAncestor;
        internal Analyzer AnalyzerAncestor { get { return _analyzerAncestor ?? (_analyzerAncestor = GetAncestor<Analyzer>()); } }
        private CompilationUnit _compilationUnitAncestor;
        internal CompilationUnit CompilationUnitAncestor { get { return _compilationUnitAncestor ?? (_compilationUnitAncestor = GetAncestor<CompilationUnit>()); } }
        private Namespace _namespaceAncestor;
        internal Namespace NamespaceAncestor { get { return _namespaceAncestor ?? (_namespaceAncestor = GetAncestor<Namespace>()); } }
        internal Namespace LogicalNamespaceAncestor { get { return NamespaceAncestor.Logical; } }
    }
    public abstract class AnnotatableObject : Object {
        protected AnnotatableObject() { }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            NodeExtensions.ProcessAnnotations(node, InitializeAnnotations, ErrorKind.DuplicateAnnotation);
        }
        protected virtual bool InitializeAnnotations(string name, Node node) {
            CompilationContext.Throw(node, ErrorKind.AnnotationNotAllowed, name);
            return false;
        }
    }
    public abstract class AnnotatableValueBase : ValueBase {
        protected AnnotatableValueBase() { }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            NodeExtensions.ProcessAnnotations(node, InitializeAnnotations, ErrorKind.DuplicateAnnotation);
        }
        protected virtual bool InitializeAnnotations(string name, Node node) {
            CompilationContext.Throw(node, ErrorKind.AnnotationNotAllowed, name);
            return false;
        }
    }
    public sealed class Analyzer : Object {
        internal readonly XAnalyzerInput AnalyzerInput;
        internal readonly List<CompilationUnit> CompilationUnitList = new List<CompilationUnit>();
        public IReadOnlyList<CompilationUnit> CompilationUnits { get { return CompilationUnitList; } }
        //public readonly IReadOnlyDictionary<string, string> XsdTexts;//key: ns name
        internal readonly IReadOnlyList<Namespace> NamespaceList;
        internal readonly IReadOnlyDictionary<XNamespace, IReadOnlyList<Namespace>> NamespacesMap;
        internal readonly ProgramInfo ProgramInfo;
        internal T GetObjectInfo<T>(Object obj, NameResolutionKind kind) where T : ObjectInfo { return ProgramInfo.GetObjectInfo<T>(obj, kind); }
        private IReadOnlyDictionary<XNamespace, IReadOnlyList<Namespace>> CreateNamespacesMap() {
            var nssMap = new Dictionary<XNamespace, IReadOnlyList<Namespace>>();
            foreach (var ns in NamespaceList) {
                var uri = ns.Uri;
                if (!nssMap.ContainsKey(uri)) nssMap.Add(uri, new List<Namespace>());
                ((List<Namespace>)nssMap[uri]).Add(ns);
            }
            return nssMap;
        }
        internal void AddClassAlias(CSClass csClass, NameSyntax value) {
            foreach (var cscls in csClass.Parts<CSClass>()) {
                if (cscls.AliasId != null) {
                    var added = false;
                    foreach (var ns in NamespaceList) {
                        var csns = ns.CSNamespace;
                        if (csns.CompilationUnitIndex == cscls.CompilationUnitIndex && csns.NamespaceIndex == cscls.NamespaceIndex) {
                            csns.AddClassAlias(cscls.AliasId, value);
                            added = true;
                            break;
                        }
                    }
                    if (!added) throw new InvalidOperationException();
                }
            }
        }
        internal readonly List<CSClass> CSClassList = new List<CSClass>();
        private static readonly CSharpCompilationOptions _compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        internal Analyzer(XAnalyzerInput analyzerInput) {
            if (analyzerInput == null) throw new ArgumentNullException("analyzerInput");
            AnalyzerInput = analyzerInput;
            var index = 0;
            foreach (var xCSharpItem in analyzerInput.XCSharpItemList)
                CompilationUnitList.Add(new CompilationUnit(this, index++, xCSharpItem, false));
            foreach (var xItem in analyzerInput.XItemList)
                CompilationUnitList.Add(new CompilationUnit(this, index++, xItem, true));
            if (CompilationUnitList.Count == 0) throw new InvalidOperationException();
            NamespaceList = CompilationUnitList.SelectMany(cu => cu.NamespaceList).ToList();
            NamespacesMap = CreateNamespacesMap();
            foreach (var nss in NamespacesMap.Values) {
                var logicalNs = nss[0];
                for (var i = 1; i < nss.Count; i++)
                    logicalNs = nss[i].MergeTo(logicalNs);
                logicalNs.IsLogical = true;
                foreach (var ns in nss) ns.Logical = logicalNs;
            }
            foreach (var nss in NamespacesMap.Values) nss[0].Logical.MergeObjects();
            foreach (var ns in NamespaceList) ns.ResolveImports();
            foreach (var nss in NamespacesMap.Values) nss[0].Logical.PreResolveObjects();
            ProgramInfo = new ProgramInfo();
            foreach (var nss in NamespacesMap.Values) ProgramInfo.AddNamespace(new NamespaceInfo(nss[0].Logical));
            foreach (var nss in NamespacesMap.Values) nss[0].Logical.CreateInfos();
            foreach (var nsInfo in ProgramInfo.NamespaceMap.Values) nsInfo.GenerateCS(CompilationUnitList);
            CompilationUnitList[0].CSMemberList.Add(
                //>[Serializable]
                //>internal sealed class MetahXProgramInfo : ProgramInfo {
                //>    private MetahXProgramInfo() { }
                //>    public static readonly MetahXProgramInfo Instance = new MetahXProgramInfo();
                //>    protected override IEnumerable<NamespaceInfo> GetNamespaces() {
                //>        return new NamespaceInfo[] {
                //>            new NamespaceInfo(XNamespace.Get("..."), new TypeInfo[] { ... }, new AttributeInfo[] { ... }, new ElementInfo[] { ... }),
                //>        };
                //>    }
                //>}
                CS.Class(new[] { CS.SerializableAttributeList }, CS.InternalSealedTokenList, "MetahXProgramInfo", new[] { CSEX.ProgramInfoName },
                    CS.Constructor(CS.PrivateTokenList, "MetahXProgramInfo", null, null),
                    CS.Field(CS.PublicStaticReadOnlyTokenList, CS.IdName("MetahXProgramInfo"), "Instance", CS.NewObjExpr(CS.IdName("MetahXProgramInfo"))),
                    CS.Method(CS.ProtectedOverrideTokenList, CS.IEnumerableOf(CSEX.NamespaceInfoName), "GetNamespaces", null,
                        SyntaxFactory.ReturnStatement(CS.NewArrOrNullExpr(CSEX.NamespaceInfoArrayType, ProgramInfo.NamespaceMap.Values.Where(i => !i.IsSystem).Select(i => i.InfoLiteral)))))
            );

            var csCompilation = CSharpCompilation.Create(
                assemblyName: "__TEMP__",
                options: _compilationOptions,
                syntaxTrees: CompilationUnitList.Select(i => i.CSSyntaxTree).Concat(AnalyzerInput.CSharpItemList.Select(i => i.SyntaxTree)),
                references: AnalyzerInput.CompilationInput.MetadataReferenceList
                );
            if (CSClassList.Count > 0) {
                foreach (var compilationUnit in CompilationUnitList) {
                    var cuSyntax = (CompilationUnitSyntax)compilationUnit.CSSyntaxTree.GetRoot();
                    var semanticModel = csCompilation.GetSemanticModel(compilationUnit.CSSyntaxTree);
                    for (var i = 0; i < CSClassList.Count; i++) {
                        var csClass = CSClassList[i];
                        if (csClass != null) {
                            var clsSyntax = cuSyntax.TryGetAnnedNode<ClassDeclarationSyntax>(csClass.CSSyntaxAnnotation);
                            if (clsSyntax != null) {
                                var namedTypeSymbol = semanticModel.GetDeclaredSymbol(clsSyntax);
                                if (!namedTypeSymbol.IsAbstract) {
                                    if (!namedTypeSymbol.HasParameterlessConstructor()) {
                                        CompilationContext.Error(csClass.Keyword, ErrorKind.ParameterlessConstructorRequired);
                                    }
                                }
                                CSClassList[i] = null;
                            }
                        }
                    }
                }
                CompilationContext.ThrowIfHasErrors();
            }
            CS.ReportDiagnostics(csCompilation.GetDiagnostics(),
                AnalyzerInput.XItemList.Select(i => i.FilePath).Concat(AnalyzerInput.XCSharpItemList.Select(i => i.FilePath)));
            CompilationContext.ThrowIfHasErrors();
            //
            //var xsdTexts = new Dictionary<string, string>();
            //foreach (var nss in NamespacesMap.Values) {
            //    var logicalNs = nss[0].Logical;
            //    foreach (var ns in nss) {
            //        foreach (var nsi in ns.NamespaceImportList)
            //            logicalNs.AddXsdImport(nsi.Uri);
            //    }
            //    var buf = new TextBuffer("  ");
            //    buf.Write(Extensions.XmlBanner);
            //    logicalNs.GenerateXsd(buf);
            //    xsdTexts.Add(logicalNs.Uri.NamespaceName, buf.ToString());
            //}
            //XsdTexts = xsdTexts;
        }
    }
    internal sealed class CSMemberList : List<MemberDeclarationSyntax> { }
    public abstract class CSPart : ValueBase {
        protected CSPart(int compilationUnitIndex, int namespaceIndex) {
            CompilationUnitIndex = compilationUnitIndex;
            NamespaceIndex = namespaceIndex;
        }
        internal readonly int CompilationUnitIndex;
        internal readonly int NamespaceIndex;
        private CSMemberList _memberList;
        internal CSMemberList MemberList { get { return _memberList ?? (_memberList = new CSMemberList()); } }
        private CSPart _next;
        //internal CSPart Next { get { return _next; } }
        internal IEnumerable<T> Parts<T>() where T : CSPart {
            for (var obj = this; obj != null; obj = obj._next)
                yield return (T)obj;
        }
        protected CSPart MergeTo(CSPart other) {
            if (other == null) return this;
            for (var obj = other; ; obj = obj._next)
                if (obj._next == null) {
                    obj._next = this;
                    break;
                }
            return other;
        }
        protected CSPart Clone() {
            var obj = (CSPart)base.MemberwiseClone();
            obj._memberList = new CSMemberList();
            if (_memberList != null) obj._memberList.AddRange(_memberList);
            if (obj._next != null) obj._next = obj._next.Clone();
            return obj;
        }
        internal static void CreateSyntax<T>(List<T> list, Node node, bool isSchemaOnly, bool filterNonCSNode = false) where T : CSharpSyntaxNode {
            IEnumerable<Node> itemNodes = node.Items;
            if (filterNonCSNode) itemNodes = itemNodes.CSNodes();
            foreach (var itemNode in itemNodes) {
                if (isSchemaOnly) CompilationContext.Error(itemNode, ErrorKind.CodeNotAllowedInSchemaOnlyFile);
                else list.Add((T)itemNode.ToSyntaxNode());
            }
        }
    }
    public sealed class CSNamespace : CSPart {
        internal CSNamespace(int compilationUnitIndex, int namespaceIndex) : base(compilationUnitIndex, namespaceIndex) { }
        private List<ExternAliasDirectiveSyntax> _externList;
        internal List<ExternAliasDirectiveSyntax> ExternList { get { return _externList ?? (_externList = new List<ExternAliasDirectiveSyntax>()); } }
        private List<UsingDirectiveSyntax> _usingList;
        internal List<UsingDirectiveSyntax> UsingList { get { return _usingList ?? (_usingList = new List<UsingDirectiveSyntax>()); } }
        private Dictionary<Identifier, NameSyntax> _classAliasMap;
        internal Dictionary<Identifier, NameSyntax> ClassAliasMap { get { return _classAliasMap ?? (_classAliasMap = new Dictionary<Identifier, NameSyntax>()); } }
        private List<UsingDirectiveSyntax> _finalUsingList;
        internal List<UsingDirectiveSyntax> FinalUsingList {
            get {
                if (_finalUsingList == null) {
                    if (_classAliasMap == null) _finalUsingList = UsingList;
                    else {
                        _finalUsingList = new List<UsingDirectiveSyntax>(UsingList);
                        foreach (var pair in _classAliasMap) {
                            _finalUsingList.Add(SyntaxFactory.UsingDirective(SyntaxFactory.NameEquals(pair.Key.Value), pair.Value));
                        }
                    }
                }
                return _finalUsingList;
            }
        }
        internal void AddClassAlias(Identifier aliasId, NameSyntax value) {
            var map = ClassAliasMap;
            if (map.ContainsKey(aliasId)) CompilationContext.Throw(aliasId, ErrorKind.DuplicateClassAlias, aliasId);
            map.Add(aliasId, value);
        }
        internal CSNamespace MergeTo(CSNamespace other) { return (CSNamespace)base.MergeTo(other); }
        //new internal CSNamespace Clone() { return (CSNamespace)base.Clone(); }
    }
    public sealed class CSClass : CSPart {
        internal CSClass(Object parent, Node node, SimpleToken keyword)
            : base(parent.CompilationUnitAncestor.Index, parent.NamespaceAncestor.Index) {
            Parent = parent;
            if (node == null || node.IsNull) {
                IsGenerated = true;
                Keyword = keyword;
            }
            else {
                if (parent.CompilationUnitAncestor.IsSchemaOnly) CompilationContext.Throw(node, ErrorKind.CodeNotAllowedInSchemaOnlyFile);
                base.Initialize(node);
                Keyword = new SimpleToken(node.Member("Keyword"));
                AliasId = node.Member("Alias").ToIdentifierOpt();
                CreateSyntax(AttributeListList, node.Member("AttributeLists"), false);
                CreateSyntax(BaseNameList, node.Member("BaseNames"), false);
                CreateSyntax(MemberList, node.Member("Members"), false);
            }
        }
        internal readonly Object Parent;
        internal readonly bool IsGenerated;
        internal readonly SimpleToken Keyword;//'##' token
        internal readonly Identifier AliasId;
        private List<AttributeListSyntax> _attributeListList;
        internal List<AttributeListSyntax> AttributeListList { get { return _attributeListList ?? (_attributeListList = new List<AttributeListSyntax>()); } }
        private List<NameSyntax> _baseNameList;
        internal List<NameSyntax> BaseNameList { get { return _baseNameList ?? (_baseNameList = new List<NameSyntax>()); } }
        internal CSClass MergeTo(CSClass other) { return (CSClass)base.MergeTo(other); }
        new internal CSClass Clone() { return (CSClass)base.Clone(); }
        internal SyntaxAnnotation CSSyntaxAnnotation { get; private set; }//opt
        internal void SetCSSyntaxAnnotation(SyntaxAnnotation ann) {
            CSSyntaxAnnotation = ann;
            Parent.AnalyzerAncestor.CSClassList.Add(this);
        }
    }
    //
    //
    public sealed class CompilationUnit : Object {
        internal CompilationUnit(Analyzer parent, int index, NodeAnalyzerInputItem nodeItem, bool isSchemaOnly) {
            Parent = parent;
            Index = index;
            CompilationInputFile = nodeItem.CompilationInputFile;
            IsSchemaOnly = isSchemaOnly;
            Initialize(nodeItem.GetNodeOnce());
        }
        internal readonly int Index;
        internal readonly CompilationInputFile CompilationInputFile;
        public string FilePath { get { return CompilationInputFile.FilePath; } }
        internal readonly bool IsSchemaOnly;
        //
        private List<ExternAliasDirectiveSyntax> _csExternList;
        internal List<ExternAliasDirectiveSyntax> CSExternList { get { return _csExternList ?? (_csExternList = new List<ExternAliasDirectiveSyntax>()); } }
        private List<UsingDirectiveSyntax> _csUsingList;
        internal List<UsingDirectiveSyntax> CSUsingList { get { return _csUsingList ?? (_csUsingList = new List<UsingDirectiveSyntax>()); } }
        private List<AttributeListSyntax> _csAttributeListList;
        internal List<AttributeListSyntax> CSAttributeListList { get { return _csAttributeListList ?? (_csAttributeListList = new List<AttributeListSyntax>()); } }
        private CSMemberList _csMemberList;
        internal CSMemberList CSMemberList { get { return _csMemberList ?? (_csMemberList = new CSMemberList()); } }
        private CompilationUnitSyntax _csCompilationUnit;
        internal CompilationUnitSyntax CSCompilationUnit {
            get {
                return _csCompilationUnit ?? (_csCompilationUnit = SyntaxFactory.CompilationUnit(SyntaxFactory.List<ExternAliasDirectiveSyntax>(CSExternList),
                    SyntaxFactory.List<UsingDirectiveSyntax>(CSUsingList), SyntaxFactory.List<AttributeListSyntax>(CSAttributeListList),
                    SyntaxFactory.List<MemberDeclarationSyntax>(CSMemberList)));
            }
        }
        private SyntaxTree _csSyntaxTree;
        internal SyntaxTree CSSyntaxTree { get { return _csSyntaxTree ?? (_csSyntaxTree = CSharpSyntaxTree.Create(CSCompilationUnit, path: FilePath)); } }
        private string _csText;
        public string CSText { get { return _csText ?? (_csText = Extensions.CSBanner + CSCompilationUnit.NormalizeWhitespace().ToString()); } }
        //
        private UriAliasingMap _uriAliasingMap;
        internal UriAliasingMap UriAliasingMap { get { return _uriAliasingMap ?? (_uriAliasingMap = new UriAliasingMap()); } }
        internal XNamespace TryGetUri(Identifier aliasId) {
            IUriAliasing aliasing;
            if (UriAliasingMap.TryGetValue(aliasId, out aliasing)) return aliasing.UriValue.Value;
            return null;
        }
        private List<Namespace> _namespaceList;
        internal List<Namespace> NamespaceList { get { return _namespaceList ?? (_namespaceList = new List<Namespace>()); } }
        //
        protected override void Initialize(Node node) {
            base.Initialize(node);
            CSPart.CreateSyntax(CSExternList, node.Member("Externs"), IsSchemaOnly);
            var prologsNode = node.Member("Prologs");
            CSPart.CreateSyntax(CSUsingList, prologsNode, IsSchemaOnly, true);
            CSPart.CreateSyntax(CSAttributeListList, node.Member("AttributeLists"), IsSchemaOnly);
            var membersNode = node.Member("Members");
            CSPart.CreateSyntax(CSMemberList, membersNode, IsSchemaOnly, true);
            CompilationContext.ThrowIfHasErrors();
            foreach (var prologNode in prologsNode.Items.NonCSNodes()) {
                switch (prologNode.Label) {
                    case UriAliasing.NodeLabel:
                        UriAliasingMap.AddOrThrow(new UriAliasing(prologNode));
                        break;
                    default: throw new InvalidOperationException();
                }
            }
            var index = 0;
            foreach (var memberNode in membersNode.Items.NonCSNodes())
                NamespaceList.Add(new Namespace(this, index++, memberNode));
        }
    }
    public sealed class UriValue : SimpleValue<XNamespace> {
        internal UriValue(Node node) {
            base.Initialize(node);
            Value = XNamespace.Get(MX.SimpleType.TrimWhitespaces(node.GetStringLiteralTokenValue()));
        }
        internal UriValue(XNamespace value, SourceSpan sourceSpan = null) : base(value, sourceSpan: sourceSpan) { }
        internal bool IsEmpty { get { return Value.IsEmpty(); } }
    }
    public interface IUriAliasing {
        UriValue UriValue { get; }
        Identifier AliasId { get; }//opt
    }
    public sealed class UriAliasingMap : Dictionary<Identifier, IUriAliasing> {
        internal UriAliasingMap() { }
        internal void AddOrThrow(IUriAliasing aliasing) {
            var aliasId = aliasing.AliasId;
            if (ContainsKey(aliasId)) CompilationContext.Throw(aliasId, ErrorKind.DuplicateUriAlias, aliasId);
            Add(aliasId, aliasing);
        }
    }
    public sealed class UriAliasing : ValueBase, IUriAliasing {
        internal UriAliasing(Node node) {
            base.Initialize(node);
            UriValue = new UriValue(node.Member("Uri"));
            AliasId = new Identifier(node.Member("Alias"));
        }
        public UriValue UriValue { get; private set; }
        public Identifier AliasId { get; private set; }
        internal const string NodeLabel = "UriAliasing";
    }
    public sealed class UriOrAlias : ValueBase {
        internal UriOrAlias(Namespace nsObj, Node node, bool resolveFromCompilationUnitOnly = false) {
            base.Initialize(node);
            var valueNode = node.Singleton;
            if (valueNode.IsNull) {
                UriValue = new UriValue(XNamespace.None, node.SourceSpan);
            }
            else {
                if (valueNode.MemberCSTokenKind() == SyntaxKind.StringLiteralToken) UriValue = new UriValue(valueNode);
                else {
                    var aliasId = new Identifier(valueNode);
                    XNamespace uri;
                    if (resolveFromCompilationUnitOnly) uri = nsObj.CompilationUnitAncestor.TryGetUri(aliasId);
                    else uri = nsObj.TryGetUri(aliasId);
                    if (uri == null) CompilationContext.Throw(aliasId, ErrorKind.InvalidUriAlias, aliasId);
                    UriValue = new UriValue(uri, aliasId.SourceSpan);
                }
            }
        }
        internal readonly UriValue UriValue;
        internal XNamespace Uri { get { return UriValue.Value; } }
        internal bool IsEmpty { get { return UriValue.IsEmpty; } }
        internal const string NodeLabel = "UriOrAlias";
    }
    public sealed class NamespaceImport : ValueBase, IUriAliasing {
        internal NamespaceImport(Namespace parent, Node node) {
            base.Initialize(node);
            Keyword = new SimpleToken(node.Member("Keyword"));
            UriOrAlias = new UriOrAlias(parent, node.Member("UriOrAlias"), true);
            UriValue = UriOrAlias.UriValue;
            AliasId = node.Member("Alias").ToIdentifierOpt();
            if (AliasId == Namespace.SystemAliasId)
                CompilationContext.Throw(AliasId, ErrorKind.NamespaceImportAliasReserved, AliasId);
        }
        internal NamespaceImport(UriValue uriValue, Identifier aliasId, Namespace referentialNamespace) {//for sys import
            UriValue = uriValue;
            AliasId = aliasId;
            ReferentialNamespace = referentialNamespace;
        }
        internal readonly SimpleToken Keyword;//null for sys import
        internal readonly UriOrAlias UriOrAlias;//null for sys import
        public UriValue UriValue { get; private set; }
        internal XNamespace Uri { get { return UriValue.Value; } }
        public Identifier AliasId { get; private set; }//opt
        //internal string Alias { get { return AliasId == null ? null : AliasId.PlainValue; } }
        internal Namespace ReferentialNamespace;//logical ns
        internal const string NodeLabel = "NamespaceImport";
    }
    public sealed class NamespaceImportList : List<NamespaceImport> {
        internal NamespaceImportList() { }
        internal NamespaceImport TryGet(Identifier aliasId) {
            foreach (var i in this)
                if (i.AliasId == aliasId)
                    return i;
            return null;
        }
        internal void AddOrThrow(NamespaceImport nsImport) {
            var aliasId = nsImport.AliasId;
            var uri = nsImport.Uri;
            foreach (var item in this) {
                if (item.Uri == uri)
                    CompilationContext.Throw(nsImport.UriOrAlias, ErrorKind.DuplicateNamespaceImportUri, uri);
                if (aliasId != null && item.AliasId == aliasId)
                    CompilationContext.Throw(aliasId, ErrorKind.DuplicateNamespaceImportAlias, aliasId);
            }
            Add(nsImport);
        }
    }
    public sealed class Qualification : SimpleValue<bool> {
        internal Qualification(Node node) {
            base.Initialize(node);
            Value = node.Singleton.MemberXTokenKind() == XTokenKind.QualifiedKeyword;
        }
        internal const string NodeLabel = "Qualification";
    }
    public enum DerivationMethod {
        None,
        Extension = XmlSchemaDerivationMethod.Extension,//2
        Restriction = XmlSchemaDerivationMethod.Restriction,//4
    }
    [Flags]
    public enum DerivationProhibition {//xsd final
        None = 0,//allow any derivation
        Extension = XmlSchemaDerivationMethod.Extension,//2
        Restriction = XmlSchemaDerivationMethod.Restriction,//4
        List = XmlSchemaDerivationMethod.List,//8
        Union = XmlSchemaDerivationMethod.Union,//16
        All = Extension | Restriction | List | Union,
        //AllSimpleType = Restriction | List | Union,
        //AllComplexType = Extension | Restriction,
        //All = AllSimpleType | AllComplexType
    }
    public sealed class DerivationProhibitionValue : SimpleValue<DerivationProhibition> {
        internal DerivationProhibitionValue(Node node) {
            base.Initialize(node);
            var finalValue = DerivationProhibition.None;
            foreach (var itemNode in node.Items) {
                DerivationProhibition value;
                switch (itemNode.MemberXTokenKind()) {
                    case XTokenKind.ExtendKeyword: value = DerivationProhibition.Extension; break;
                    case XTokenKind.RestrictKeyword: value = DerivationProhibition.Restriction; break;
                    case XTokenKind.ListKeyword: value = DerivationProhibition.List; break;
                    case XTokenKind.UniteKeyword: value = DerivationProhibition.Union; break;
                    case XTokenKind.NoneKeyword: value = DerivationProhibition.None; break;
                    case XTokenKind.AllKeyword: value = DerivationProhibition.All; break;
                    default: throw new InvalidOperationException();
                }
                finalValue |= value;
            }
            Value = finalValue;
        }
        internal const string NodeLabel = "DerivationProhibition";
    }
    public sealed class InstanceProhibitionValue : SimpleValue<InstanceProhibition> {
        internal InstanceProhibitionValue(Node node) {
            base.Initialize(node);
            var finalValue = InstanceProhibition.None;
            foreach (var itemNode in node.Items) {
                InstanceProhibition value;
                switch (itemNode.MemberXTokenKind()) {
                    case XTokenKind.ExtendKeyword: value = InstanceProhibition.Extension; break;
                    case XTokenKind.RestrictKeyword: value = InstanceProhibition.Restriction; break;
                    case XTokenKind.SubstituteKeyword: value = InstanceProhibition.Substitution; break;
                    case XTokenKind.NoneKeyword: value = InstanceProhibition.None; break;
                    case XTokenKind.AllKeyword: value = InstanceProhibition.All; break;
                    default: throw new InvalidOperationException();
                }
                finalValue |= value;
            }
            Value = finalValue;
        }
        internal const string NodeLabel = "InstanceProhibition";
    }
    internal enum NameResolutionKind { Type, Attribute, AttributeSet, Element, ChildStruct, KeyOrUnique }
    public interface IDisplayableObject {
        string DisplayName { get; }
    }
    public sealed class Namespace : AnnotatableObject, IDisplayableObject {
        internal Namespace(CompilationUnit parent, int index, Node node) {
            Parent = parent;
            Index = index;
            CSNamespace = new CSNamespace(CompilationUnitAncestor.Index, index);
            Initialize(node);
        }
        private Namespace() {
            IsSystem = true;
            IsLogical = true;
            UriValue = new UriValue(MX.NamespaceInfo.SystemUri);
            CSDottedNameOpt = new DottedName(CSEX.MXNameString);
            foreach (var kind in Enum.GetValues(typeof(MX.TypeKind)).Cast<MX.TypeKind>().Where(i => i < MX.TypeKind.ComplexType))
                AddType(new SystemType(this, kind));
        }
        internal static readonly Identifier SystemAliasId = new Identifier("sys");
        internal static readonly Namespace System = new Namespace();
        private static readonly NamespaceImport SystemNamespaceImport = new NamespaceImport(System.UriValue, SystemAliasId, System);
        //
        internal readonly int Index;
        internal readonly bool IsSystem;
        internal bool IsLogical;
        internal Namespace Logical;
        internal SimpleToken Keyword { get; private set; }//null for sys
        internal UriOrAlias UriOrAlias { get; private set; }//null for sys
        internal UriValue UriValue { get; private set; }
        internal XNamespace Uri { get { return UriValue.Value; } }
        internal XName GetFullName(string localName) { return Uri.GetName(localName); }
        private string _displayName;
        public string DisplayName { get { return _displayName ?? (_displayName = "{" + Uri.ToString() + "}"); } }
        internal DottedName CSDottedNameOpt { get; private set; }
        internal readonly CSNamespace CSNamespace;//null for sys
        internal DerivationProhibitionValue DerivationProhibitionValue { get; private set; }//xsd final
        internal InstanceProhibitionValue InstanceProhibitionValue { get; private set; }//xsd block
        internal Qualification ElementQualification { get; private set; }
        internal Qualification AttributeQualification { get; private set; }
        private NamespaceImportList _namespaceImportList;
        internal NamespaceImportList NamespaceImportList { get { return _namespaceImportList ?? (_namespaceImportList = new NamespaceImportList()); } }
        private UriAliasingMap _uriAliasingMap;
        internal UriAliasingMap UriAliasingMap { get { return _uriAliasingMap ?? (_uriAliasingMap = new UriAliasingMap()); } }
        internal XNamespace TryGetUri(Identifier aliasId) {
            IUriAliasing aliasing;
            if (UriAliasingMap.TryGetValue(aliasId, out aliasing)) return aliasing.UriValue.Value;
            return CompilationUnitAncestor.TryGetUri(aliasId);
        }
        private List<Type> _unresolvedTypeList;
        internal List<Type> UnresolvedTypeList { get { return _unresolvedTypeList ?? (_unresolvedTypeList = new List<Type>()); } }
        private List<GlobalAttribute> _attributeList;
        internal List<GlobalAttribute> AttributeList { get { return _attributeList ?? (_attributeList = new List<GlobalAttribute>()); } }
        private List<GlobalAttributeSet> _attributeSetList;
        internal List<GlobalAttributeSet> AttributeSetList { get { return _attributeSetList ?? (_attributeSetList = new List<GlobalAttributeSet>()); } }
        private List<GlobalElement> _elementList;
        internal List<GlobalElement> ElementList { get { return _elementList ?? (_elementList = new List<GlobalElement>()); } }
        private List<IdentityConstraint> _identityConstraintList;
        internal List<IdentityConstraint> IdentityConstraintList { get { return _identityConstraintList ?? (_identityConstraintList = new List<IdentityConstraint>()); } }
        private List<CommonChildStruct> _childStructList;
        internal List<CommonChildStruct> ChildStructList { get { return _childStructList ?? (_childStructList = new List<CommonChildStruct>()); } }
        private HashSet<XNamespace> _xsdUriSet;
        private HashSet<XNamespace> XsdUriSet { get { return _xsdUriSet ?? (_xsdUriSet = new HashSet<XNamespace>()); } }
        internal void AddXsdUri(XNamespace uri) {
            if (uri == null || uri.IsEmpty()) throw new InvalidOperationException();
            XsdUriSet.Add(uri);
        }
        //
        //has meaning in logical ns
        internal DottedName CSDottedName {
            get {
                if (CSDottedNameOpt == null) CompilationContext.Throw(Keyword, ErrorKind.CSNamespaceRequired);
                return CSDottedNameOpt;
            }
        }
        internal NameSyntax CSNonGlobalFullName { get { return CSDottedName.CSNonGlobalFullName; } }
        internal NameSyntax CSFullName { get { return CSDottedName.CSFullName; } }
        internal ExpressionSyntax CSFullExp { get { return CSDottedName.CSFullExp; } }
        internal DerivationProhibition DerivationProhibition { get { return DerivationProhibitionValue != null ? DerivationProhibitionValue.Value : DerivationProhibition.None; } }
        internal InstanceProhibition InstanceProhibition { get { return InstanceProhibitionValue != null ? InstanceProhibitionValue.Value : InstanceProhibition.None; } }
        internal bool IsElementQualified { get { return ElementQualification != null ? ElementQualification.Value : true; } }
        internal bool IsAttributeQualified { get { return AttributeQualification != null ? AttributeQualification.Value : false; } }
        //
        private Dictionary<Identifier, Type> _unresolvedTypeMap;
        internal Dictionary<Identifier, Type> UnresolvedTypeMap { get { return _unresolvedTypeMap ?? (_unresolvedTypeMap = new Dictionary<Identifier, Type>()); } }
        private Dictionary<Identifier, Type> _typeMap;
        internal Dictionary<Identifier, Type> TypeMap { get { return _typeMap ?? (_typeMap = new Dictionary<Identifier, Type>()); } }
        private void AddType(Type type) { TypeMap.Add(type.NameId, type); }
        private Dictionary<Identifier, GlobalAttribute> _attributeMap;
        internal Dictionary<Identifier, GlobalAttribute> AttributeMap { get { return _attributeMap ?? (_attributeMap = new Dictionary<Identifier, GlobalAttribute>()); } }
        private Dictionary<Identifier, GlobalAttributeSet> _attributeSetMap;
        internal Dictionary<Identifier, GlobalAttributeSet> AttributeSetMap { get { return _attributeSetMap ?? (_attributeSetMap = new Dictionary<Identifier, GlobalAttributeSet>()); } }
        private Dictionary<Identifier, GlobalElement> _elementMap;
        internal Dictionary<Identifier, GlobalElement> ElementMap { get { return _elementMap ?? (_elementMap = new Dictionary<Identifier, GlobalElement>()); } }
        private Dictionary<Identifier, IdentityConstraint> _identityConstraintMap;
        internal Dictionary<Identifier, IdentityConstraint> IdentityConstraintMap { get { return _identityConstraintMap ?? (_identityConstraintMap = new Dictionary<Identifier, IdentityConstraint>()); } }
        private Dictionary<Identifier, CommonChildStruct> _childStructMap;
        internal Dictionary<Identifier, CommonChildStruct> ChildStructMap { get { return _childStructMap ?? (_childStructMap = new Dictionary<Identifier, CommonChildStruct>()); } }
        //
        private Dictionary<XNamespace, string> _xsdPrefixMap;//value:prefix
        private Dictionary<XNamespace, string> XsdPrefixMap {
            get {
                if (_xsdPrefixMap == null) {
                    _xsdPrefixMap = new Dictionary<XNamespace, string>();
                    _xsdPrefixMap.Add(System.Uri, "xs");
                    _xsdPrefixMap.Add(Uri, "tns");
                    foreach (var uri in XsdUriSet) AddXsdPrefix(uri);
                }
                return _xsdPrefixMap;
            }
        }
        private HashSet<XNamespace> _xsdImportSet;
        private HashSet<XNamespace> XsdImportSet { get { return _xsdImportSet ?? (_xsdImportSet = new HashSet<XNamespace>()); } }
        private int _xsdPrefixIndex;
        internal string GetXsdPrefix(XNamespace uri) { return XsdPrefixMap[uri]; }
        internal string GetXsdPrefix(Object obj) { return GetXsdPrefix(obj.NamespaceAncestor.Uri); }
        private void AddXsdPrefix(XNamespace uri) {
            if (!XsdPrefixMap.ContainsKey(uri)) XsdPrefixMap.Add(uri, "ns" + (_xsdPrefixIndex++).ToInvariantString());
        }
        internal void AddXsdImport(XNamespace uri) {
            if (uri != Uri) {
                XsdImportSet.Add(uri);
                AddXsdPrefix(uri);
            }
        }
        //end has meaning in logical ns
        //
        protected override bool InitializeAnnotations(string name, Node node) {
            switch (name) {
                case EX.CSNamespaceNodeLabel:
                    if (CSDottedNameOpt != null) return false;
                    if (CompilationUnitAncestor.IsSchemaOnly) CompilationContext.Throw(node, ErrorKind.CodeNotAllowedInSchemaOnlyFile);
                    CSDottedNameOpt = new DottedName(node.Singleton);
                    return true;
                case DerivationProhibitionValue.NodeLabel:
                    if (DerivationProhibitionValue != null) return false;
                    DerivationProhibitionValue = new DerivationProhibitionValue(node.Singleton);
                    return true;
                case InstanceProhibitionValue.NodeLabel:
                    if (InstanceProhibitionValue != null) return false;
                    InstanceProhibitionValue = new InstanceProhibitionValue(node.Singleton);
                    return true;
                case EX.ElementQualificationNodeLabel:
                    if (ElementQualification != null) return false;
                    ElementQualification = new Qualification(node.Singleton);
                    return true;
                case EX.AttributeQualificationNodeLabel:
                    if (AttributeQualification != null) return false;
                    AttributeQualification = new Qualification(node.Singleton);
                    return true;
            }
            return base.InitializeAnnotations(name, node);
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            Keyword = new SimpleToken(node.Member("Keyword"));
            UriOrAlias = new UriOrAlias(this, node.Member("UriOrAlias"), true);
            if (UriOrAlias.IsEmpty) CompilationContext.Throw(UriOrAlias, ErrorKind.InvalidNamespaceUri);
            UriValue = UriOrAlias.UriValue;
            var uriString = Uri.NamespaceName;
            if (uriString == MX.NamespaceInfo.XsUriValue || uriString == MX.NamespaceInfo.XsiUriValue || uriString == MX.Element.XmlnsPrefixUriValue || uriString == MX.Element.XmlPrefixUriValue)
                CompilationContext.Throw(UriOrAlias, ErrorKind.NamespaceUriReserved, uriString);
            //
            var isSchemaOnly = CompilationUnitAncestor.IsSchemaOnly;
            CSPart.CreateSyntax(CSNamespace.ExternList, node.Member("Externs"), isSchemaOnly);
            var prologsNode = node.Member("Prologs");
            CSPart.CreateSyntax(CSNamespace.UsingList, prologsNode, isSchemaOnly, true);
            var membersNode = node.Member("Members");
            CSPart.CreateSyntax(CSNamespace.MemberList, membersNode, isSchemaOnly, true);
            CompilationContext.ThrowIfHasErrors();
            //
            foreach (var prologNode in prologsNode.Items.NonCSNodes()) {
                switch (prologNode.Label) {
                    case NamespaceImport.NodeLabel:
                        NamespaceImportList.AddOrThrow(new NamespaceImport(this, prologNode));
                        break;
                    case UriAliasing.NodeLabel:
                        UriAliasingMap.AddOrThrow(new UriAliasing(prologNode));
                        break;
                    default: throw new InvalidOperationException();
                }
            }
            foreach (var nsImport in NamespaceImportList) {
                if (nsImport.AliasId != null && !UriAliasingMap.ContainsKey(nsImport.AliasId))
                    UriAliasingMap.Add(nsImport.AliasId, nsImport);
            }
            //
            foreach (var memberNode in membersNode.Items.NonCSNodes()) {
                switch (memberNode.Label) {
                    case Type.NodeLabel:
                        UnresolvedTypeList.Add(Type.Create(this, memberNode));
                        break;
                    case Attribute.NodeLabel:
                        AttributeList.Add(new GlobalAttribute(this, memberNode));
                        break;
                    case AttributeSet.NodeLabel:
                        AttributeSetList.Add(new GlobalAttributeSet(this, memberNode));
                        break;
                    case Element.NodeLabel:
                        ElementList.Add(new GlobalElement(this, memberNode));
                        break;
                    case CommonChildStruct.NodeLabel:
                        ChildStructList.Add(new CommonChildStruct(this, memberNode));
                        break;
                    default: throw new InvalidOperationException();
                }
            }
        }
        internal Namespace MergeTo(Namespace other) {
            if (other == null) return this;
            if (CSDottedNameOpt != null) {
                if (other.CSDottedNameOpt == null) other.CSDottedNameOpt = CSDottedNameOpt;
                else if (CSDottedNameOpt != other.CSDottedNameOpt)
                    CompilationContext.Error(CSDottedNameOpt, ErrorKind.CSNamespaceNotEqualTo, CSDottedNameOpt, other.CSDottedNameOpt);
            }
            if (DerivationProhibitionValue != null) {
                if (other.DerivationProhibitionValue == null) other.DerivationProhibitionValue = DerivationProhibitionValue;
                else if (DerivationProhibitionValue != other.DerivationProhibitionValue)
                    CompilationContext.Error(DerivationProhibitionValue, ErrorKind.DerivationProhibitionNotEqualTo, DerivationProhibitionValue, other.DerivationProhibitionValue);
            }
            if (InstanceProhibitionValue != null) {
                if (other.InstanceProhibitionValue == null) other.InstanceProhibitionValue = InstanceProhibitionValue;
                else if (InstanceProhibitionValue != other.InstanceProhibitionValue)
                    CompilationContext.Error(InstanceProhibitionValue, ErrorKind.InstanceProhibitionNotEqualTo, InstanceProhibitionValue, other.InstanceProhibitionValue);
            }
            if (ElementQualification != null) {
                if (other.ElementQualification == null) other.ElementQualification = ElementQualification;
                else if (ElementQualification != other.ElementQualification)
                    CompilationContext.Error(ElementQualification, ErrorKind.ElementQualificationNotEqualTo, ElementQualification, other.ElementQualification);
            }
            if (AttributeQualification != null) {
                if (other.AttributeQualification == null) other.AttributeQualification = AttributeQualification;
                else if (AttributeQualification != other.AttributeQualification)
                    CompilationContext.Error(AttributeQualification, ErrorKind.AttributeQualificationNotEqualTo, AttributeQualification, other.AttributeQualification);
            }
            CompilationContext.ThrowIfHasErrors();
            CSNamespace.MergeTo(other.CSNamespace);
            other.UnresolvedTypeList.AddRange(UnresolvedTypeList);
            other.AttributeList.AddRange(AttributeList);
            other.AttributeSetList.AddRange(AttributeSetList);
            other.ElementList.AddRange(ElementList);
            other.IdentityConstraintList.AddRange(IdentityConstraintList);
            other.ChildStructList.AddRange(ChildStructList);
            other.XsdUriSet.AddRange(XsdUriSet);
            return other;
        }
        internal void MergeObjects() {
            var typeMap = UnresolvedTypeMap;
            foreach (var type in UnresolvedTypeList) {
                if (!typeMap.ContainsKey(type.NameId)) typeMap.Add(type.NameId, type);
                else typeMap[type.NameId] = type.MergeTo(typeMap[type.NameId]);
            }
            var attMap = AttributeMap;
            foreach (var att in AttributeList) {
                if (!attMap.ContainsKey(att.NameId)) attMap.Add(att.NameId, att);
                else attMap[att.NameId] = (GlobalAttribute)att.MergeTo(attMap[att.NameId]);
            }
            var attSetMap = AttributeSetMap;
            foreach (var attSet in AttributeSetList) {
                if (!attSetMap.ContainsKey(attSet.NameId)) attSetMap.Add(attSet.NameId, attSet);
                else attSetMap[attSet.NameId] = (GlobalAttributeSet)attSet.MergeTo(attSetMap[attSet.NameId]);
            }
            var eleMap = ElementMap;
            foreach (var ele in ElementList) {
                if (!eleMap.ContainsKey(ele.NameId)) eleMap.Add(ele.NameId, ele);
                else eleMap[ele.NameId] = (GlobalElement)ele.MergeTo(eleMap[ele.NameId]);
            }
            var icMap = IdentityConstraintMap;
            foreach (var ic in IdentityConstraintList) {
                if (icMap.ContainsKey(ic.NameId))
                    CompilationContext.Throw(ic.NameId, ErrorKind.DuplicateIdentityConstraintName, ic.NameId);
                icMap.Add(ic.NameId, ic);
            }
            var csMap = ChildStructMap;
            foreach (var cs in ChildStructList) {
                if (!csMap.ContainsKey(cs.NameId)) csMap.Add(cs.NameId, cs);
                else csMap[cs.NameId] = (CommonChildStruct)cs.MergeTo(csMap[cs.NameId]);
            }
            CompilationContext.ThrowIfHasErrors();
        }
        internal void ResolveImports() {
            var nssMap = AnalyzerAncestor.NamespacesMap;
            foreach (var ni in NamespaceImportList) {
                IReadOnlyList<Namespace> nss;
                if (!nssMap.TryGetValue(ni.Uri, out nss))
                    CompilationContext.Throw(ni.UriOrAlias, ErrorKind.InvalidNamespaceImportUri, ni.Uri);
                ni.ReferentialNamespace = nss[0].Logical;
            }
            NamespaceImportList.Add(SystemNamespaceImport);
        }
        private Object TryResolveNameCore(Identifier nameId, NameResolutionKind kind) {
            switch (kind) {
                case NameResolutionKind.Type:
                    var type = TypeMap.TryGetValue(nameId);
                    if (type != null) return type;
                    type = UnresolvedTypeMap.TryGetValue(nameId);
                    if (type == null) return null;
                    type = type.ResolveType();
                    TypeMap.Add(type.NameId, type);
                    return type;
                case NameResolutionKind.Attribute:
                    return AttributeMap.TryGetValue(nameId);
                case NameResolutionKind.AttributeSet:
                    return AttributeSetMap.TryGetValue(nameId);
                case NameResolutionKind.Element:
                    return ElementMap.TryGetValue(nameId);
                case NameResolutionKind.ChildStruct:
                    return ChildStructMap.TryGetValue(nameId);
                case NameResolutionKind.KeyOrUnique:
                    var ic = IdentityConstraintMap.TryGetValue(nameId);
                    if (ic != null && ic.IsKeyRef) ic = null;
                    return ic;
                default: throw new InvalidOperationException();
            }
        }
        internal Object TryResolveName(QualifiableName qName, NameResolutionKind kind) {
            if (qName.IsQualified) {
                var nsi = NamespaceImportList.TryGet(qName.AliasId);
                if (nsi == null) CompilationContext.Throw(qName.AliasId, ErrorKind.InvalidQualifiableNameAlias, qName.AliasId);
                return nsi.ReferentialNamespace.TryResolveNameCore(qName.NameId, kind);
            }
            else {
                var obj = Logical.TryResolveNameCore(qName.NameId, kind);
                if (obj != null) return obj;
                foreach (var nsi in NamespaceImportList) {
                    var newobj = nsi.ReferentialNamespace.TryResolveNameCore(qName.NameId, kind);
                    if (newobj != null) {
                        if (obj == null) obj = newobj;
                        else CompilationContext.Throw(qName, ErrorKind.AmbiguousQualifiableName, qName.NameId);
                    }
                }
                return obj;
            }
        }
        internal T ResolveName<T>(QualifiableName qName, NameResolutionKind kind) where T : Object {
            var obj = TryResolveName(qName, kind) as T;
            if (obj == null) CompilationContext.Throw(qName, ErrorKind.InvalidQualifiableName, qName);
            return obj;
        }
        internal void PreResolveObjects() {
            foreach (var type in UnresolvedTypeMap.Values) {
                var newType = type.ResolveType();
                if (!TypeMap.ContainsKey(newType.NameId))
                    TypeMap.Add(newType.NameId, newType);
            }
            foreach (var element in ElementMap.Values) element.ResolveSubstitution();
            foreach (var ic in IdentityConstraintMap.Values)
                if (ic.IsKeyRef && ic.Referential == null) {
                    ic.Referential = ic.NamespaceAncestor.ResolveName<IdentityConstraint>(ic.QName, NameResolutionKind.KeyOrUnique);
                    if (ic.ValuePathExpressionList.Count != ic.Referential.ValuePathExpressionList.Count)
                        CompilationContext.Throw(ic.Keyword, ErrorKind.KeyRefValueCountNotEqualToReferentialValueCount, ic.ValuePathExpressionList.Count, ic.Referential.ValuePathExpressionList.Count);
                }
        }
        internal void CreateInfos() {
            var analyzer = AnalyzerAncestor;
            foreach (var type in TypeMap.Values) analyzer.GetObjectInfo<ObjectInfo>(type, NameResolutionKind.Type);
            foreach (var attribute in AttributeMap.Values) analyzer.GetObjectInfo<ObjectInfo>(attribute, NameResolutionKind.Attribute);
            foreach (var element in ElementMap.Values) analyzer.GetObjectInfo<ObjectInfo>(element, NameResolutionKind.Element);
        }
        internal void GenerateXsd(TextBuffer buf) {
            buf.Write(@"<xs:schema targetNamespace='{0}' elementFormDefault='{1}' attributeFormDefault='{2}' finalDefault='{3}' blockDefault='{4}'",
                Uri.ToXmlString(), IsElementQualified.ToXsdQualification(), IsAttributeQualified.ToXsdQualification(),
                DerivationProhibition.ToXsd(), InstanceProhibition.ToXsd());
            foreach (var pair in XsdPrefixMap)
                buf.Write(" xmlns:{0}='{1}'", pair.Value, pair.Key.ToXmlString());
            buf.WriteLine(">");
            buf.PushIndent();
            buf.WriteLine("<!--Imports-->");
            foreach (var ns in XsdImportSet) buf.WriteLine("<xs:import namespace ='{0}' />", ns.ToXmlString());
            buf.WriteLine("<!--Global Types-->");
            foreach (var type in TypeMap.Values) type.GenerateXsd(buf);
            buf.WriteLine("<!--Global Attributes-->");
            foreach (var attribute in AttributeMap.Values) attribute.GenerateXsd(buf);
            buf.WriteLine("<!--Global Elements-->");
            foreach (var element in ElementMap.Values) element.GenerateXsd(buf);
            buf.WriteLine("<!--Attribute Groups-->");
            foreach (var attributeSet in AttributeSetMap.Values) attributeSet.GenerateXsd(buf);
            buf.WriteLine("<!--Groups-->");
            foreach (var childStruct in ChildStructMap.Values) childStruct.GenerateXsd(buf);
            buf.PopIndent();
            buf.WriteLine("</xs:schema>");
        }
    }
    public sealed class QualifiableNameEx : ValueBase {
        internal QualifiableNameEx(Object parent, Node node) {
            base.Initialize(node);
            _itemList.Add(new Item(parent, new QualifiableName(node)));
        }
        private struct Item {
            internal Item(Object parent, QualifiableName qName) {
                Parent = parent;
                QName = qName;
            }
            internal readonly Object Parent;
            internal readonly QualifiableName QName;
            internal T Resolve<T>(NameResolutionKind kind) where T : Object {
                return Parent.NamespaceAncestor.ResolveName<T>(QName, kind);
            }
        }
        private readonly List<Item> _itemList = new List<Item>();
        internal Identifier NameId { get { return _itemList[0].QName.NameId; } }
        internal QualifiableNameEx MergeTo(QualifiableNameEx other) {
            if (other == null) return this;
            other._itemList.AddRange(_itemList);
            return other;
        }
        internal T Resolve<T>(NameResolutionKind kind) where T : Object {
            var obj = _itemList[0].Resolve<T>(kind);
            for (var i = 1; i < _itemList.Count; i++)
                if (!object.ReferenceEquals(obj, _itemList[i].Resolve<T>(kind)))
                    CompilationContext.Throw(_itemList[i].QName, ErrorKind.QualifiableNameNotEqualTo, _itemList[i].QName, _itemList[0].QName);
            return obj;
        }
    }
    public class Type : AnnotatableObject, IDisplayableObject {
        protected Type() { }
        protected Type(UnresolvedType unresolvedType) {
            Parent = unresolvedType;
            SourceSpan = unresolvedType.SourceSpan;
            Keyword = unresolvedType.Keyword;
            NameId = unresolvedType.NameId;
            DerivationProhibitionValue = unresolvedType.DerivationProhibitionValue;
            CSClass = unresolvedType.CSClass;
        }
        private Type(Object parent, Node node) {
            Parent = parent;
            IsUntyped = true;
            Initialize(node);
        }
        internal const string NodeLabel = "Type";
        internal readonly bool IsUntyped;
        internal SimpleToken Keyword { get; private set; }
        public Identifier NameId { get; protected set; }//null for local
        internal string Name { get { return NameId != null ? NameId.PlainValue : null; } }
        internal bool IsGlobal { get { return NameId != null; } }
        private string _displayName;
        public string DisplayName {
            get { return _displayName ?? (_displayName = GetAncestor<IDisplayableObject>().DisplayName + ".Type" + (IsGlobal ? "'" + Name + "'" : null)); }
        }
        internal virtual string ObjectMergeName { get { return null; } }
        internal DerivationProhibitionValue DerivationProhibitionValue { get; private set; }//xsd final
        internal DerivationProhibition DerivationProhibition { get { return DerivationProhibitionValue != null ? DerivationProhibitionValue.Value : LogicalNamespaceAncestor.DerivationProhibition; } }
        internal CSClass CSClass { get; private set; }
        internal CSClass GetCSClass() { return CSClass.Clone(); }
        //
        internal static Type Create(Object parent, Node node) {
            var bodyNode = node.Member("Body");
            if (bodyNode.IsNull) return new Type(parent, node);
            switch (bodyNode.Label) {
                case ListedSimpleType.NodePartLabel: return new ListedSimpleType(parent, node);
                case UnitedSimpleType.NodePartLabel: return new UnitedSimpleType(parent, node);
                case UnresolvedType.DirectnessNodePartLabel:
                case UnresolvedType.ExtensionNodePartLabel:
                case UnresolvedType.RestrictionNodePartLabel: return new UnresolvedType(parent, node);
                default: throw new InvalidOperationException();
            }
        }
        protected override bool InitializeAnnotations(string name, Node node) {
            switch (name) {
                case DerivationProhibitionValue.NodeLabel:
                    if (DerivationProhibitionValue != null) return false;
                    DerivationProhibitionValue = new DerivationProhibitionValue(node.Singleton);
                    return true;
            }
            return base.InitializeAnnotations(name, node);
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            Keyword = new SimpleToken(node.Member("Keyword"));
            NameId = node.Member("Name").ToIdentifierOpt();
            if (base.Parent is Namespace) {
                if (NameId == null) CompilationContext.Throw(Keyword, ErrorKind.NameRequiredForGlobalType);
            }
            else {
                if (NameId != null) CompilationContext.Throw(NameId, ErrorKind.NameNotAllowedForLocalType);
                if (DerivationProhibitionValue != null)
                    CompilationContext.Throw(DerivationProhibitionValue, ErrorKind.DerivationProhibitionNotAllowedForLocalType);
            }
            CSClass = new CSClass(this, node.Member("CSClass"), Keyword);
        }
        internal virtual Type MergeTo(Type other) {
            if (other == null) return this;
            if (DerivationProhibitionValue != null) {
                if (other.DerivationProhibitionValue == null) other.DerivationProhibitionValue = DerivationProhibitionValue;
                else if (DerivationProhibitionValue.Value != other.DerivationProhibitionValue.Value)
                    CompilationContext.Error(DerivationProhibitionValue, ErrorKind.DerivationProhibitionNotEqualTo, DerivationProhibitionValue, other.DerivationProhibitionValue);
            }
            CompilationContext.ThrowIfHasErrors();
            CSClass.MergeTo(other.CSClass);
            return other;
        }
        private bool _inProcessing;
        private Type _resolvedType;
        internal Type ResolveType() {
            if (_resolvedType == null) {
                if (_inProcessing) CompilationContext.Throw(Keyword, ErrorKind.CircularReferenceDetected);
                _inProcessing = true;
                if (IsUntyped) CompilationContext.Throw(Keyword, ErrorKind.SpecificTypeRequired);
                var unresolvedType = this as UnresolvedType;
                if (unresolvedType != null) _resolvedType = unresolvedType.CreateResolvedType();
                else _resolvedType = this;
                _inProcessing = false;
            }
            return _resolvedType;
        }
        internal TypeInfo CreateInfo(string givingCSName) {
            if (_inProcessing) CompilationContext.Throw(Keyword, ErrorKind.CircularReferenceDetected);
            _inProcessing = true;
            XName fullName; string csName;
            if (IsGlobal) {
                if (givingCSName != null) throw new InvalidOperationException();
                fullName = NamespaceAncestor.GetFullName(Name);
                csName = NameId.Value;
            }
            else {
                if (givingCSName == null) throw new InvalidOperationException();
                fullName = null;
                csName = givingCSName;
            }
            var info = CreateInfoCore(fullName, csName);
            _inProcessing = false;
            return info;
        }
        protected virtual TypeInfo CreateInfoCore(XName fullName, string csName) { throw new InvalidOperationException(); }
        //
        internal virtual void GenerateXsd(TextBuffer buf) { throw new InvalidOperationException(); }
        protected virtual void GenerateXsdTagPart(TextBuffer buf) {
            if (NameId != null) buf.Write(" name='{0}'", Name);
            if (DerivationProhibitionValue != null) buf.Write(" final='{0}'", DerivationProhibitionValue.Value.ToXsd());
        }
    }
    public sealed class TypeOrReference : Object {
        internal TypeOrReference(Object parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal Type LocalType { get; private set; }//opt
        internal QualifiableNameEx QName { get; private set; }//opt
        internal bool IsLocalType { get { return LocalType != null; } }
        internal bool IsReference { get { return !IsLocalType; } }
        internal Type Type { get; private set; }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            if (node.Label == QualifiableName.NodeLabel) QName = new QualifiableNameEx(this, node);
            else LocalType = Type.Create(this, node);
        }
        internal TypeOrReference MergeTo(TypeOrReference other) {
            if (other == null) return this;
            if (LocalType != null) {
                if (other.LocalType == null) CompilationContext.Throw(LocalType.Keyword, ErrorKind.LocalTypeAndTypeReferenceNotCompatible);
                other.LocalType = LocalType.MergeTo(other.LocalType);
            }
            else {
                if (other.QName == null) CompilationContext.Throw(QName, ErrorKind.LocalTypeAndTypeReferenceNotCompatible);
                other.QName = QName.MergeTo(other.QName);
            }
            return other;
        }
        internal Type ResolveType() {
            if (Type == null) {
                if (LocalType != null) Type = LocalType.ResolveType();
                else Type = QName.Resolve<Type>(NameResolutionKind.Type);
            }
            return Type;
        }
        internal TypeInfo CreateInfo(string givingCSName) {
            var type = ResolveType();
            if (IsLocalType) return type.CreateInfo(givingCSName);
            return AnalyzerAncestor.GetObjectInfo<TypeInfo>(type, NameResolutionKind.Type);
        }
        internal string XsdQName {
            get {
                if (IsLocalType) return null;
                var systemType = Type as SystemType;
                if (systemType != null) return "xs:" + systemType.XsdName;
                return LogicalNamespaceAncestor.GetXsdPrefix(Type) + ":" + Type.Name;
            }
        }
    }
    public sealed class SystemType : Type {
        internal SystemType(Namespace parent, MX.TypeKind kind) {
            Parent = parent;
            NameId = new Identifier(kind.ToString());
            Kind = kind;
            XsdName = kind.ToXsName();
        }
        internal readonly MX.TypeKind Kind;
        internal readonly string XsdName;
        //internal bool IsSimpleType { get { return Kind != MX.TypeKind.Type; } }
    }
    internal enum UnresolvedTypeKind { Directness, Extension, Restriction }
    public sealed class UnresolvedType : Type {
        internal UnresolvedType(Object parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal const string DirectnessNodePartLabel = "TypeDirectness";
        internal const string ExtensionNodePartLabel = "TypeExtension";
        internal const string RestrictionNodePartLabel = "TypeRestriction";
        internal UnresolvedTypeKind Kind { get; private set; }
        //for complex type
        internal SimpleToken AbstractKeyword { get; private set; }
        internal SimpleToken MixedKeyword { get; private set; }
        internal InstanceProhibitionValue InstanceProhibitionValue { get; private set; }//xsd block
        //end for complex type
        internal TypeOrReference BaseTypeOrReference { get; private set; }//null for directness
        internal RootAttributeSet AttributeSet { get; private set; }//opt
        internal FacetSet FacetSet { get; private set; }//opt
        internal RootChildStruct ChildStruct { get; private set; }//opt
        internal override string ObjectMergeName { get { return Kind.ToString(); } }
        //
        protected override bool InitializeAnnotations(string name, Node node) {
            switch (name) {
                case EX.AbstractNodeLabel:
                    if (AbstractKeyword != null) return false;
                    AbstractKeyword = new SimpleToken(node);
                    return true;
                case EX.MixedNodeLabel:
                    if (MixedKeyword != null) return false;
                    MixedKeyword = new SimpleToken(node);
                    return true;
                case InstanceProhibitionValue.NodeLabel:
                    if (InstanceProhibitionValue != null) return false;
                    InstanceProhibitionValue = new InstanceProhibitionValue(node.Singleton);
                    return true;
            }
            return base.InitializeAnnotations(name, node);
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            if (AbstractKeyword != null && !IsGlobal)
                CompilationContext.Throw(AbstractKeyword, ErrorKind.AbstractNotAllowedForLocalType);
            var bodyNode = node.Member("Body");
            switch (bodyNode.Label) {
                case DirectnessNodePartLabel: Kind = UnresolvedTypeKind.Directness; break;
                case ExtensionNodePartLabel: Kind = UnresolvedTypeKind.Extension; break;
                case RestrictionNodePartLabel: Kind = UnresolvedTypeKind.Restriction; break;
                default: throw new InvalidOperationException();
            }
            BaseTypeOrReference = bodyNode.Member("BaseTypeOrReference").ToTypeOrReferenceOpt(this);
            var attributeSetNode = bodyNode.Member("AttributeSet");
            if (attributeSetNode.IsNotNull) {
                switch (attributeSetNode.Label) {
                    case X.AttributeSet.NodeLabel: AttributeSet = new RootAttributeSet(this, attributeSetNode); break;
                    default: throw new InvalidOperationException();
                }
            }
            var childStructOrFacetSetNode = bodyNode.Member("ChildStructOrFacetSet");
            if (childStructOrFacetSetNode.IsNotNull) {
                switch (childStructOrFacetSetNode.Label) {
                    case FacetSet.NodeLabel: FacetSet = new FacetSet(this, childStructOrFacetSetNode); break;
                    case X.ChildStruct.NodeLabel: ChildStruct = new RootChildStruct(this, childStructOrFacetSetNode); break;
                    default: throw new InvalidOperationException();
                }
            }
        }
        internal override Type MergeTo(Type otherType) {
            if (otherType == null) return this;
            if (otherType.IsUntyped) return otherType.MergeTo(this);
            var other = otherType as UnresolvedType;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.TypeNotCompatibleWith, ObjectMergeName, otherType.ObjectMergeName);
            base.MergeTo(other);
            if (other.Kind != Kind) CompilationContext.Throw(Keyword, ErrorKind.TypeNotCompatibleWith, ObjectMergeName, other.ObjectMergeName);
            if (AbstractKeyword != null && other.AbstractKeyword == null) other.AbstractKeyword = AbstractKeyword;
            if (MixedKeyword != null && other.MixedKeyword == null) other.MixedKeyword = MixedKeyword;
            if (InstanceProhibitionValue != null) {
                if (other.InstanceProhibitionValue == null) other.InstanceProhibitionValue = InstanceProhibitionValue;
                else if (other.InstanceProhibitionValue.Value != InstanceProhibitionValue.Value)
                    CompilationContext.Error(InstanceProhibitionValue, ErrorKind.InstanceProhibitionNotEqualTo, InstanceProhibitionValue, other.InstanceProhibitionValue);
            }
            CompilationContext.ThrowIfHasErrors();
            if (BaseTypeOrReference != null) other.BaseTypeOrReference = BaseTypeOrReference.MergeTo(other.BaseTypeOrReference);
            if (AttributeSet != null) other.AttributeSet = (RootAttributeSet)AttributeSet.MergeTo(other.AttributeSet);
            if (FacetSet != null) other.FacetSet = FacetSet.MergeTo(other.FacetSet);
            if (ChildStruct != null) other.ChildStruct = (RootChildStruct)ChildStruct.MergeTo(other.ChildStruct);
            return other;
        }
        internal Type CreateResolvedType() {
            if (Kind == UnresolvedTypeKind.Directness) return new ExtendedComplexChildComplexType(this);
            var baseType = BaseTypeOrReference.ResolveType();
            var baseIsSimpleType = baseType is SimpleType;
            var systemBaseType = baseType as SystemType;
            if (systemBaseType != null) {
                if (systemBaseType.Kind == MX.TypeKind.Type) CompilationContext.Throw(BaseTypeOrReference, ErrorKind.CannotExtendOrRestrictSystemRootType);
                if (systemBaseType.Kind == MX.TypeKind.SimpleType && Kind == UnresolvedTypeKind.Restriction)
                    CompilationContext.Throw(BaseTypeOrReference, ErrorKind.CannotRestrictSystemRootSimpleType);
                baseIsSimpleType = true;
            }
            //SimpleType
            if (baseIsSimpleType && Kind == UnresolvedTypeKind.Restriction) {
                if (AbstractKeyword != null) CompilationContext.Error(AbstractKeyword, ErrorKind.AbstractNotAllowedForSimpleType);
                if (MixedKeyword != null) CompilationContext.Error(MixedKeyword, ErrorKind.MixedNotAllowedForSimpleType);
                if (InstanceProhibitionValue != null) CompilationContext.Error(InstanceProhibitionValue, ErrorKind.InstanceProhibitionNotAllowedForSimpleType);
                if (AttributeSet != null) CompilationContext.Error(AttributeSet, ErrorKind.AttributeSetNotAllowedForSimpleType);
                if (ChildStruct != null) CompilationContext.Error(ChildStruct, ErrorKind.ChildrenNotAllowedForSimpleType);
                if (FacetSet != null && !FacetSet.CSClass.IsGenerated) CompilationContext.Error(FacetSet.CSClass, ErrorKind.CodeNotAllowedInFacetSetForSimpleType);
                CompilationContext.ThrowIfHasErrors();
                return new RestrictedSimpleType(this);
            }
            //ComplexType
            if (BaseTypeOrReference.IsLocalType) CompilationContext.Throw(BaseTypeOrReference, ErrorKind.BaseTypeOfComplexTypeCannotBeLocal);
            //SimpleChildComplexType
            if (baseIsSimpleType || baseType is SimpleChildComplexType) {
                if (MixedKeyword != null) CompilationContext.Error(MixedKeyword, ErrorKind.MixedNotAllowedForSimpleChildComplexType);
                if (ChildStruct != null) CompilationContext.Error(ChildStruct, ErrorKind.ChildStructNotAllowedForSimpleChildComplexType);
                CompilationContext.ThrowIfHasErrors();
                if (Kind == UnresolvedTypeKind.Extension) return new ExtendedSimpleChildComplexType(this);
                return new RestrictedSimpleChildComplexType(this);
            }
            //ComplexChildComplexType
            if (FacetSet != null) CompilationContext.Throw(FacetSet, ErrorKind.FacetSetNotAllowedForComplexChildComplexType);
            if (Kind == UnresolvedTypeKind.Extension) return new ExtendedComplexChildComplexType(this);
            return new RestrictedComplexChildComplexType(this);
        }
    }
    public abstract class SimpleType : Type {
        protected SimpleType() { }
        protected SimpleType(UnresolvedType unresolvedType) : base(unresolvedType) { }
    }
    public sealed class ListedSimpleType : SimpleType {
        internal ListedSimpleType(Object parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal const string NodePartLabel = "TypeList";
        internal TypeOrReference ItemTypeOrReference { get; private set; }
        internal override string ObjectMergeName { get { return "List"; } }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            ItemTypeOrReference = new TypeOrReference(this, node.Member("Body").Singleton);
        }
        internal override Type MergeTo(Type otherType) {
            if (otherType == null) return this;
            if (otherType.IsUntyped) return otherType.MergeTo(this);
            var other = otherType as ListedSimpleType;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.TypeNotCompatibleWith, ObjectMergeName, otherType.ObjectMergeName);
            base.MergeTo(other);
            ItemTypeOrReference.MergeTo(other.ItemTypeOrReference);
            return other;
        }
        protected override TypeInfo CreateInfoCore(XName fullName, string csName) {
            var itemTypeInfo = ItemTypeOrReference.CreateInfo("ItemClass") as SimpleTypeInfo;
            if (itemTypeInfo == null) CompilationContext.Throw(ItemTypeOrReference, ErrorKind.SimpleTypeRequired);
            if (itemTypeInfo.Kind == MX.TypeKind.ListedSimpleType)
                CompilationContext.Throw(ItemTypeOrReference, ErrorKind.ListedSimpleTypeItemCannotBeList);
            if (itemTypeInfo.DerivationProhibition.IsSet(DerivationProhibition.List))
                CompilationContext.Throw(Keyword, ErrorKind.ListDerivationProhibited);
            return new ListedSimpleTypeInfo(Keyword, this, csName, GetCSClass(), fullName, DerivationProhibition, itemTypeInfo);
        }
        internal override void GenerateXsd(TextBuffer buf) {
            buf.Write("<xs:simpleType");
            GenerateXsdTagPart(buf);
            buf.WriteLine(">");
            buf.PushIndent();
            buf.Write("<xs:list");
            var itemQName = ItemTypeOrReference.XsdQName;
            if (itemQName != null) buf.Write(" itemType='{0}'", itemQName);
            buf.WriteLine(">");
            if (itemQName == null) {
                buf.PushIndent();
                ItemTypeOrReference.Type.GenerateXsd(buf);
                buf.PopIndent();
            }
            buf.WriteLine("</xs:list>");
            buf.PopIndent();
            buf.WriteLine("</xs:simpleType>");
        }
    }
    public sealed class UnitedSimpleTypeMember : Object, IMemberObject {
        internal UnitedSimpleTypeMember(UnitedSimpleType parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        public SimpleToken Keyword { get; private set; }
        internal Identifier NameId { get; private set; }
        Identifier IMemberObject.MemberNameId { get { return NameId; } }
        internal TypeOrReference TypeOrReferenceOpt { get; private set; }
        internal TypeOrReference TypeOrReference {
            get {
                if (TypeOrReferenceOpt == null) CompilationContext.Throw(Keyword, ErrorKind.TypeRequired);
                return TypeOrReferenceOpt;
            }
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            Keyword = new SimpleToken(node.Member("Keyword"));
            NameId = new Identifier(node.Member("Name"));
            TypeOrReferenceOpt = node.Member("TypeOrReference").ToTypeOrReferenceOpt(this);
        }
        internal UnitedSimpleTypeMember MergeTo(UnitedSimpleTypeMember other) {
            if (other == null) return this;
            if (TypeOrReferenceOpt != null)
                other.TypeOrReferenceOpt = TypeOrReferenceOpt.MergeTo(other.TypeOrReferenceOpt);
            return other;
        }
        IMemberObject IMemberObject.MergeTo(IMemberObject other) { return MergeTo((UnitedSimpleTypeMember)other); }
        internal UnitedSimpleTypeMemberInfo CreateInfo(ref bool hasLocalTypes) {
            if (TypeOrReference.IsLocalType) hasLocalTypes = true;
            else if (hasLocalTypes) CompilationContext.Throw(TypeOrReference, ErrorKind.UnitedSimpleTypeMemberTypeReferenceMustPrecedeLocalType);
            var memberTypeInfo = TypeOrReference.CreateInfo(NameId.Value + "_Class") as SimpleTypeInfo;
            if (memberTypeInfo == null) CompilationContext.Throw(TypeOrReference, ErrorKind.SimpleTypeRequired);
            if (memberTypeInfo.DerivationProhibition.IsSet(DerivationProhibition.Union))
                CompilationContext.Throw(Keyword, ErrorKind.UnionDerivationProhibited);
            return new UnitedSimpleTypeMemberInfo(Keyword, NameId, memberTypeInfo);
        }
    }
    public sealed class UnitedSimpleType : SimpleType {
        internal UnitedSimpleType(Object parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal const string NodePartLabel = "TypeUnion";
        private MemberObjectListEx<UnitedSimpleTypeMember> _memberListEx;
        internal MemberObjectListEx<UnitedSimpleTypeMember> MemberListEx { get { return _memberListEx ?? (_memberListEx = new MemberObjectListEx<UnitedSimpleTypeMember>()); } }
        internal IReadOnlyList<UnitedSimpleTypeMember> MemberList { get { return MemberListEx.GetResult(true); } }
        internal override string ObjectMergeName { get { return "Union"; } }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            foreach (var memberNode in node.Member("Body").Singleton.Items)
                MemberListEx.AddMember(new UnitedSimpleTypeMember(this, memberNode));
        }
        internal override Type MergeTo(Type otherType) {
            if (otherType == null) return this;
            if (otherType.IsUntyped) return otherType.MergeTo(this);
            var other = otherType as UnitedSimpleType;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.TypeNotCompatibleWith, ObjectMergeName, otherType.ObjectMergeName);
            base.MergeTo(other);
            MemberListEx.MergeTo(other.MemberListEx);
            return other;
        }
        protected override TypeInfo CreateInfoCore(XName fullName, string csName) {
            bool hasLocalTypes = false;
            var memberInfoList = new List<UnitedSimpleTypeMemberInfo>();
            foreach (var member in MemberList)
                memberInfoList.Add(member.CreateInfo(ref hasLocalTypes));
            return new UnitedSimpleTypeInfo(Keyword, this, csName, GetCSClass(), fullName, DerivationProhibition, memberInfoList);
        }
        internal override void GenerateXsd(TextBuffer buf) {
            buf.Write("<xs:simpleType");
            GenerateXsdTagPart(buf);
            buf.WriteLine(">");
            buf.PushIndent();
            buf.Write("<xs:union");
            var hasQNameMembers = false;
            var hasLocalMembers = false;
            foreach (var member in MemberList) {
                var memberTypeOrReference = member.TypeOrReference;
                if (memberTypeOrReference.IsReference) {
                    if (!hasQNameMembers) {
                        buf.Write(" memberTypes='");
                        hasQNameMembers = true;
                    }
                    buf.Write(" " + memberTypeOrReference.XsdQName);
                }
                else {
                    if (!hasLocalMembers) {
                        if (hasQNameMembers) buf.WriteLine("'>");
                        else buf.WriteLine(">");
                        buf.PushIndent();
                        hasLocalMembers = true;
                    }
                    memberTypeOrReference.Type.GenerateXsd(buf);
                }
            }
            if (hasLocalMembers) buf.PopIndent();
            else buf.WriteLine("'>");
            buf.WriteLine("</xs:union>");
            buf.PopIndent();
            buf.WriteLine("</xs:simpleType>");
        }
    }
    //
    //
    public sealed class Literal : ValueBase {
        internal const string NumericLiteralNodeLabel = "NumericLiteral";
        internal const string UriOrFullNameLiteralNodeLabel = "UriOrFullNameLiteral";
        internal Literal(Namespace nsObj, Node node) {
            base.Initialize(node);
            _nsObj = nsObj;
            switch (node.Label) {
                case NodeExtensions.CSTokenLabel:
                    switch (node.MemberCSTokenKind()) {
                        case SyntaxKind.StringLiteralToken:
                            Text = node.GetStringLiteralTokenValue();
                            _isStringLiteral = true;
                            break;
                        case SyntaxKind.TrueKeyword: Text = "true"; break;
                        case SyntaxKind.FalseKeyword: Text = "false"; break;
                        default: throw new InvalidOperationException();
                    }
                    break;
                case NumericLiteralNodeLabel: {
                        var signNode = node.Member("Sign");
                        Text = (signNode.IsNotNull ? signNode.MemberTokenText() : "") + node.Member("Literal").MemberTokenText();
                    }
                    break;
                case UriOrFullNameLiteralNodeLabel: {
                        var localNameId = node.Member("LocalName").ToIdentifierOpt();
                        if (localNameId != null) _xsdQNameLocalName = localNameId.PlainValue;
                        string uriString = "";
                        var uri = new UriOrAlias(nsObj, node.Member("UriOrAlias")).Uri;
                        if (!uri.IsEmpty()) {
                            uriString = uri.NamespaceName;
                            if (localNameId != null) {
                                nsObj.AddXsdUri(uri);
                                _xsdQNameUri = uri;
                            }
                        }
                        if (localNameId == null) Text = uriString;
                        else Text = "{" + uriString + "}" + _xsdQNameLocalName;
                    }
                    break;
                default: throw new InvalidOperationException();
            }
        }
        internal readonly string Text;
        private readonly bool _isStringLiteral;
        public override string ToString() { return Text; }
        internal bool TextEquals(Literal other) { return Text == other.Text && _isStringLiteral == other._isStringLiteral; }
        internal object ParseAndValidate(ISimpleTypeInfo simpleTypeInfo) {
            object value;
            if (simpleTypeInfo != null) {
                var ctx = new MX.Context();
                if (!MX.SimpleType.TryParseAndValidateValue(Text, simpleTypeInfo, ctx, out value)) {
                    var sb = new StringBuilder();
                    foreach (var diag in ctx.Diagnostics) {
                        sb.AppendLine();
                        sb.Append(" - ");
                        sb.Append(diag.Message);
                    }
                    CompilationContext.Throw(this, ErrorKind.InvalidSimpleTypeLiteral, sb.ToString());
                }
                //if (value is MX.FullNameValue && _isStringLiteral)
                //    Errors.Throw(this, ErrorKind.FullNameLiteralCannotBeString);
            }
            else {
                if (!_isStringLiteral) CompilationContext.Throw(this, ErrorKind.ComplexChildComplexTypeLiteralMustBeString);
                value = Text;
            }
            return value;
        }
        private readonly Namespace _nsObj;
        private readonly XNamespace _xsdQNameUri;
        private readonly string _xsdQNameLocalName;
        private string _xsdText;
        internal string XsdText {
            get {
                if (_xsdText == null) {
                    if (_xsdQNameLocalName != null) {
                        if (_xsdQNameUri != null) _xsdText = _nsObj.Logical.GetXsdPrefix(_xsdQNameUri) + ":" + _xsdQNameLocalName;
                        else _xsdText = _xsdQNameLocalName;
                    }
                    else _xsdText = Text;
                }
                return _xsdText;
            }
        }
    }
    #region facets
    public abstract class Facet : AnnotatableValueBase {
        protected Facet() { }
        internal SimpleToken Keyword { get; private set; }//opt
        internal SimpleToken FixedKeyword { get; private set; }//opt
        internal bool IsFixed { get { return FixedKeyword != null; } }
        protected override bool InitializeAnnotations(string name, Node node) {
            switch (name) {
                case EX.FixedNodeLabel:
                    if (FixedKeyword != null) return false;
                    FixedKeyword = new SimpleToken(node);
                    return true;
            }
            return base.InitializeAnnotations(name, node);
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            if (node.HasMember("Keyword")) Keyword = new SimpleToken(node.Member("Keyword"));
        }
        protected void MergeTo(Facet other) {
            if (FixedKeyword != null && other.FixedKeyword == null) other.FixedKeyword = FixedKeyword;
        }
    }
    public sealed class UnsignedIntegerFacet<T> : Facet where T : struct {
        internal UnsignedIntegerFacet(Node node) { Initialize(node); }
        internal UnsignedIntegerValue<T> UnsignedIntegerValue { get; private set; }
        internal T Value { get { return UnsignedIntegerValue.Value; } }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            UnsignedIntegerValue = new UnsignedIntegerValue<T>(node.Member("Value"), ErrorKind.InvalidInteger);
        }
        internal UnsignedIntegerFacet<T> MergeTo(UnsignedIntegerFacet<T> other) {
            if (other == null) return this;
            base.MergeTo(other);
            if (UnsignedIntegerValue != other.UnsignedIntegerValue)
                CompilationContext.Throw(UnsignedIntegerValue, ErrorKind.FacetNotEqualTo, "value", UnsignedIntegerValue, other.UnsignedIntegerValue);
            return other;
        }
    }
    public sealed class LengthRangeFacet : Facet {
        internal LengthRangeFacet(Node node) { Initialize(node); }
        internal const string NodeLabel = "LengthRange";
        internal UnsignedIntegerFacet<ulong> MinLengthValue { get; private set; }
        internal ulong? MinLength { get { return MinLengthValue != null ? (ulong?)MinLengthValue.Value : null; } }
        internal UnsignedIntegerFacet<ulong> MaxLengthValue { get; private set; }
        internal ulong? MaxLength { get { return MaxLengthValue != null ? (ulong?)MaxLengthValue.Value : null; } }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var minLengthNode = node.Member("MinLength");
            if (minLengthNode.IsNotNull) MinLengthValue = new UnsignedIntegerFacet<ulong>(minLengthNode);
            var maxLengthNode = node.Member("MaxLength");
            if (maxLengthNode.IsNotNull) MaxLengthValue = new UnsignedIntegerFacet<ulong>(maxLengthNode);
            if (MaxLength < MinLength)
                CompilationContext.Error(MaxLengthValue, ErrorKind.MaxLengthMustGreaterThanOrEqualToMinLength, MaxLength, MinLength);
        }
        internal LengthRangeFacet MergeTo(LengthRangeFacet other) {
            if (other == null) return this;
            if (MinLengthValue != null) other.MinLengthValue = MinLengthValue.MergeTo(other.MinLengthValue);
            if (MaxLengthValue != null) other.MaxLengthValue = MaxLengthValue.MergeTo(other.MaxLengthValue);
            return other;
        }
    }
    public sealed class DigitsFacet : Facet {
        internal DigitsFacet(Node node) { Initialize(node); }
        internal const string NodeLabel = "Digits";
        internal UnsignedIntegerFacet<byte> TotalDigitsValue { get; private set; }
        internal byte? TotalDigits { get { return TotalDigitsValue != null ? (byte?)TotalDigitsValue.Value : null; } }
        internal UnsignedIntegerFacet<byte> FractionDigitsValue { get; private set; }
        internal byte? FractionDigits { get { return FractionDigitsValue != null ? (byte?)FractionDigitsValue.Value : null; } }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var totalDigitsNode = node.Member("TotalDigits");
            if (totalDigitsNode.IsNotNull) TotalDigitsValue = new UnsignedIntegerFacet<byte>(totalDigitsNode);
            var fractionDigitsNode = node.Member("FractionDigits");
            if (fractionDigitsNode.IsNotNull) FractionDigitsValue = new UnsignedIntegerFacet<byte>(fractionDigitsNode);
            if (TotalDigits == 0)
                CompilationContext.Error(TotalDigitsValue, ErrorKind.TotalDigitsMustGreaterThanZero);
            if (TotalDigits < FractionDigits)
                CompilationContext.Error(FractionDigitsValue, ErrorKind.FractionDigitsMustLessThanOrEqualToTotalDigits, FractionDigits, TotalDigits);
            CompilationContext.ThrowIfHasErrors();
        }
        internal DigitsFacet MergeTo(DigitsFacet other) {
            if (other == null) return this;
            if (TotalDigitsValue != null) other.TotalDigitsValue = TotalDigitsValue.MergeTo(other.TotalDigitsValue);
            if (FractionDigitsValue != null) other.FractionDigitsValue = FractionDigitsValue.MergeTo(other.FractionDigitsValue);
            return other;
        }
    }
    public sealed class ValueFacet : Facet {
        internal ValueFacet(Namespace nsObj, Node node) { Initialize(nsObj, node); }
        internal SimpleToken Token { get; private set; }//[ ( ) ]
        internal bool IsInclusive { get; private set; }
        internal Literal Literal { get; private set; }
        internal string Text { get { return Literal.Text; } }
        internal string XsdText { get { return Literal.XsdText; } }
        private void Initialize(Namespace nsObj, Node node) {
            base.Initialize(node);
            var tokenNode = node.Member("Token");
            Token = new SimpleToken(tokenNode);
            var tokenKind = tokenNode.MemberCSTokenKind();
            IsInclusive = tokenKind == SyntaxKind.OpenBracketToken || tokenKind == SyntaxKind.CloseBracketToken;
            Literal = new Literal(nsObj, node.Member("Value"));
        }
        internal ValueFacet MergeTo(ValueFacet other) {
            if (other == null) return this;
            base.MergeTo(other);
            if (IsInclusive != other.IsInclusive) CompilationContext.Error(Token, ErrorKind.FacetNotEqualTo, "inclusive", IsInclusive, other.IsInclusive);
            if (!Literal.TextEquals(other.Literal)) CompilationContext.Error(Literal, ErrorKind.FacetNotEqualTo, "value", Literal, other.Literal);
            CompilationContext.ThrowIfHasErrors();
            return other;
        }
    }
    public sealed class ValueRangeFacet : Facet {
        internal ValueRangeFacet(Namespace nsObj, Node node) { Initialize(nsObj, node); }
        internal const string NodeLabel = "ValueRange";
        internal ValueFacet LowerValue { get; private set; }
        internal ValueFacet UpperValue { get; private set; }
        private void Initialize(Namespace nsObj, Node node) {
            base.Initialize(node);
            var lowerValueNode = node.Member("LowerValue");
            if (lowerValueNode.IsNotNull) LowerValue = new ValueFacet(nsObj, lowerValueNode);
            var upperValueNode = node.Member("UpperValue");
            if (upperValueNode.IsNotNull) UpperValue = new ValueFacet(nsObj, upperValueNode);
        }
        internal ValueRangeFacet MergeTo(ValueRangeFacet other) {
            if (other == null) return this;
            if (LowerValue != null) other.LowerValue = LowerValue.MergeTo(other.LowerValue);
            if (UpperValue != null) other.UpperValue = UpperValue.MergeTo(other.UpperValue);
            return other;
        }
    }
    public sealed class EnumerationsFacet : Facet {
        internal EnumerationsFacet(Namespace nsObj, Node node) { Initialize(nsObj, node); }
        internal const string NodeLabel = "Enumerations";
        internal readonly List<EnumerationsFacetItem> ItemList = new List<EnumerationsFacetItem>();
        private void Initialize(Namespace nsObj, Node node) {
            base.Initialize(node);
            foreach (var itemNode in node.Member("Items").Items) {
                var item = new EnumerationsFacetItem(nsObj, itemNode);
                if (item.NameId != null && ItemList.Any(i => i.NameId == item.NameId))
                    CompilationContext.Throw(item.NameId, ErrorKind.DuplicateEnumerationsItemName, item.NameId);
                ItemList.Add(item);
            }
        }
        private string _text;
        internal string Text { get { return _text ?? (_text = Extensions.GetSeparatedString(ItemList, value => value.Text)); } }
        private string _namedText;
        internal string NamedText { get { return _namedText ?? (_namedText = Extensions.GetSeparatedString(ItemList, value => value.NamedText)); } }
        private bool NameAndTextEquals(EnumerationsFacet other) {
            if (ItemList.Count != other.ItemList.Count) return false;
            for (var i = 0; i < ItemList.Count; i++)
                if (!ItemList[i].NameAndTextEquals(other.ItemList[i])) return false;
            return true;
        }
        internal EnumerationsFacet MergeTo(EnumerationsFacet other) {
            if (other == null) return this;
            if (!NameAndTextEquals(other)) CompilationContext.Throw(Keyword, ErrorKind.FacetNotEqualTo, "value", NamedText, other.NamedText);
            return other;
        }
    }
    public sealed class EnumerationsFacetItem : ValueBase {
        internal EnumerationsFacetItem(Namespace nsObj, Node node) {
            base.Initialize(node);
            NameId = node.Member("Name").ToIdentifierOpt();
            Literal = new Literal(nsObj, node.Member("Literal"));
        }
        internal readonly Identifier NameId;//opt
        internal string Name { get { return NameId == null ? null : NameId.PlainValue; } }
        internal readonly Literal Literal;
        internal string Text { get { return Literal.Text; } }
        private string _namedText;
        internal string NamedText {
            get {
                if (_namedText == null) {
                    if (NameId == null) _namedText = Literal.Text;
                    else _namedText = NameId.PlainValue + " = " + Literal.Text;
                }
                return _namedText;
            }
        }
        internal bool NameAndTextEquals(EnumerationsFacetItem other) {
            return NameId == other.NameId && Literal.TextEquals(other.Literal);
        }
    }
    public sealed class PatternsFacet : Facet {
        internal PatternsFacet(Node node) { Initialize(node); }
        internal const string NodeLabel = "Patterns";
        internal readonly List<StringValue> ItemList = new List<StringValue>();
        internal string Pattern { get; private set; }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            foreach (var itemNode in node.Member("Items").Items) ItemList.Add(new StringValue(itemNode));
            if (ItemList.Count == 1) Pattern = ItemList[0].Value;
            else {
                var sb = new StringBuilder();
                for (var i = 0; i < ItemList.Count; i++) {
                    if (i > 0) sb.Append('|');
                    sb.Append('(');
                    sb.Append(ItemList[i].Value);
                    sb.Append(')');
                }
                Pattern = sb.ToString();
            }
            try { new Regex(Pattern); }
            catch (Exception) { CompilationContext.Throw(this, ErrorKind.InvalidFacetPattern, Pattern); }
        }
        internal PatternsFacet MergeTo(PatternsFacet other) {
            if (other == null) return this;
            if (Pattern != other.Pattern) CompilationContext.Throw(Keyword, ErrorKind.FacetNotEqualTo, "value", Pattern, other.Pattern);
            return other;
        }
    }
    public sealed class WhitespaceNormalizationValue : SimpleValue<MX.WhitespaceNormalization> {
        internal WhitespaceNormalizationValue(Node node) {
            base.Initialize(node);
            switch (node.Singleton.MemberXTokenKind()) {
                case XTokenKind.PreserveKeyword: Value = MX.WhitespaceNormalization.Preserve; break;
                case XTokenKind.ReplaceKeyword: Value = MX.WhitespaceNormalization.Replace; break;
                case XTokenKind.CollapseKeyword: Value = MX.WhitespaceNormalization.Collapse; break;
                default: throw new InvalidOperationException();
            }
        }
    }
    public sealed class WhitespaceFacet : Facet {
        internal WhitespaceFacet(Node node) { Initialize(node); }
        internal const string NodeLabel = "Whitespace";
        internal WhitespaceNormalizationValue Value { get; private set; }
        internal WhitespaceNormalization WhitespaceNormalization { get { return Value.Value; } }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            Value = new WhitespaceNormalizationValue(node.Member("Value"));
        }
        internal WhitespaceFacet MergeTo(WhitespaceFacet other) {
            if (other == null) return this;
            base.MergeTo(other);
            if (Value != other.Value) CompilationContext.Throw(Value, ErrorKind.FacetNotEqualTo, "value", Value, other.Value);
            return other;
        }
    }
    internal enum FacetKind {
        LengthRange = 1,
        TotalDigits,
        FractionDigits,
        ValueRange,
        Enumerations,
        Patterns,
        Whitespace,
    }
    public sealed class FacetSet : Object {
        internal FacetSet(UnresolvedType parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal const string NodeLabel = "FacetSet";
        internal SimpleToken Keyword { get; private set; }
        internal LengthRangeFacet LengthRangeFacet { get; private set; }
        internal UnsignedIntegerFacet<ulong> MinLengthFacet { get { return LengthRangeFacet != null ? LengthRangeFacet.MinLengthValue : null; } }
        internal UnsignedIntegerFacet<ulong> MaxLengthFacet { get { return LengthRangeFacet != null ? LengthRangeFacet.MaxLengthValue : null; } }
        internal DigitsFacet DigitsFacet { get; private set; }
        internal UnsignedIntegerFacet<byte> TotalDigitsFacet { get { return DigitsFacet != null ? DigitsFacet.TotalDigitsValue : null; } }
        internal UnsignedIntegerFacet<byte> FractionDigitsFacet { get { return DigitsFacet != null ? DigitsFacet.FractionDigitsValue : null; } }
        internal ValueRangeFacet ValueRangeFacet { get; private set; }
        internal ValueFacet LowerValueFacet { get { return ValueRangeFacet != null ? ValueRangeFacet.LowerValue : null; } }
        internal ValueFacet UpperValueFacet { get { return ValueRangeFacet != null ? ValueRangeFacet.UpperValue : null; } }
        internal EnumerationsFacet EnumerationsFacet { get; private set; }
        internal PatternsFacet PatternsFacet { get; private set; }
        internal WhitespaceFacet WhitespaceFacet { get; private set; }
        internal CSClass CSClass { get; private set; }
        internal CSClass GetCSClass() { return CSClass.Clone(); }
        internal Facet GetFacet(FacetKind kind) {
            switch (kind) {
                case FacetKind.LengthRange: return LengthRangeFacet;
                case FacetKind.TotalDigits: return TotalDigitsFacet;
                case FacetKind.FractionDigits: return FractionDigitsFacet;
                case FacetKind.ValueRange: return ValueRangeFacet;
                case FacetKind.Enumerations: return EnumerationsFacet;
                case FacetKind.Patterns: return PatternsFacet;
                case FacetKind.Whitespace: return WhitespaceFacet;
                default: throw new InvalidOperationException();
            }
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            Keyword = new SimpleToken(node.Member("Keyword"));
            foreach (var facetNode in node.Member("Facets").Items) {
                switch (facetNode.Label) {
                    case LengthRangeFacet.NodeLabel:
                        if (LengthRangeFacet != null) CompilationContext.Throw(facetNode, ErrorKind.DuplicateFacet);
                        LengthRangeFacet = new LengthRangeFacet(facetNode);
                        break;
                    case DigitsFacet.NodeLabel:
                        if (DigitsFacet != null) CompilationContext.Throw(facetNode, ErrorKind.DuplicateFacet);
                        DigitsFacet = new DigitsFacet(facetNode);
                        break;
                    case ValueRangeFacet.NodeLabel:
                        if (ValueRangeFacet != null) CompilationContext.Throw(facetNode, ErrorKind.DuplicateFacet);
                        ValueRangeFacet = new ValueRangeFacet(NamespaceAncestor, facetNode);
                        break;
                    case EnumerationsFacet.NodeLabel:
                        if (EnumerationsFacet != null) CompilationContext.Throw(facetNode, ErrorKind.DuplicateFacet);
                        EnumerationsFacet = new EnumerationsFacet(NamespaceAncestor, facetNode);
                        break;
                    case PatternsFacet.NodeLabel:
                        if (PatternsFacet != null) CompilationContext.Throw(facetNode, ErrorKind.DuplicateFacet);
                        PatternsFacet = new PatternsFacet(facetNode);
                        break;
                    case WhitespaceFacet.NodeLabel:
                        if (WhitespaceFacet != null) CompilationContext.Throw(facetNode, ErrorKind.DuplicateFacet);
                        WhitespaceFacet = new WhitespaceFacet(facetNode);
                        break;
                    default: throw new InvalidOperationException();
                }
            }
            CSClass = new CSClass(this, node.Member("CSClass"), Keyword);
        }
        internal FacetSet MergeTo(FacetSet other) {
            if (other == null) return this;
            if (LengthRangeFacet != null) other.LengthRangeFacet = LengthRangeFacet.MergeTo(other.LengthRangeFacet);
            if (DigitsFacet != null) other.DigitsFacet = DigitsFacet.MergeTo(other.DigitsFacet);
            if (ValueRangeFacet != null) other.ValueRangeFacet = ValueRangeFacet.MergeTo(other.ValueRangeFacet);
            if (EnumerationsFacet != null) other.EnumerationsFacet = EnumerationsFacet.MergeTo(other.EnumerationsFacet);
            if (PatternsFacet != null) other.PatternsFacet = PatternsFacet.MergeTo(other.PatternsFacet);
            if (WhitespaceFacet != null) other.WhitespaceFacet = WhitespaceFacet.MergeTo(other.WhitespaceFacet);
            CSClass.MergeTo(other.CSClass);
            return other;
        }
        internal FacetSetInfo CreateInfo(SimpleTypeInfo baseTypeInfo) {
            if (baseTypeInfo == null) throw new ArgumentNullException("baseTypeInfo");
            ISimpleTypeInfo iBaseTypeInfo = baseTypeInfo;
            var typeKind = iBaseTypeInfo.Kind;
            foreach (FacetKind facetKind in Enum.GetValues(typeof(FacetKind))) {
                var facet = GetFacet(facetKind);
                if (facet != null) {
                    var typeKindSet = _facetApplicableMap[facetKind];
                    if (typeKindSet != null && !typeKindSet.Contains(typeKind))
                        CompilationContext.Error(facet, ErrorKind.FacetNotApplicable, facetKind, typeKind);
                }
            }
            CompilationContext.ThrowIfHasErrors();
            //
            ulong? minLength = null, maxLength = null;
            bool minLengthFixed = false, maxLengthFixed = false;
            byte? totalDigits = null, fractionDigits = null;
            bool totalDigitsFixed = false, fractionDigitsFixed = false;
            object lowerValue = null, upperValue = null;
            bool lowerValueInclusive = false, upperValueInclusive = false;
            bool lowerValueFixed = false, upperValueFixed = false;
            string lowerValueText = null, upperValueText = null;
            IReadOnlyList<EnumerationItemInfo> enumerations = null;
            string enumerationsText = null;
            List<PatternItemInfo> patternList = null;
            WhitespaceNormalization? whitespaceNormalization = null;
            bool whitespaceNormalizationFixed = false;
            //
            var baseFacetSetInfo = iBaseTypeInfo.FacetSet;
            //
            var minLengthFacet = MinLengthFacet;
            if (minLengthFacet != null) {
                minLength = minLengthFacet.Value;
                minLengthFixed = minLengthFacet.IsFixed;
            }
            if (baseFacetSetInfo != null) {
                if (minLength == null) minLength = baseFacetSetInfo.MinLength;
                else if (minLength < baseFacetSetInfo.MinLength)
                    CompilationContext.Throw(minLengthFacet, ErrorKind.MinLengthMustGreaterThanOrEqualToBaseMinLength, minLength, baseFacetSetInfo.MinLength);
                else if (baseFacetSetInfo.MinLengthFixed && minLength != baseFacetSetInfo.MinLength)
                    CompilationContext.Throw(minLengthFacet, ErrorKind.MinLengthMustEqualToBaseMinLengthIfBaseIsFixed, minLength, baseFacetSetInfo.MinLength);
                if (baseFacetSetInfo.MinLengthFixed) minLengthFixed = true;
            }
            //
            var maxLengthFacet = MaxLengthFacet;
            if (maxLengthFacet != null) {
                maxLength = maxLengthFacet.Value;
                maxLengthFixed = maxLengthFacet.IsFixed;
            }
            if (baseFacetSetInfo != null) {
                if (maxLength == null) maxLength = baseFacetSetInfo.MaxLength;
                else if (maxLength > baseFacetSetInfo.MaxLength)
                    CompilationContext.Throw(maxLengthFacet, ErrorKind.MaxLengthMustLessThanOrEqualToBaseMaxLength, maxLength, baseFacetSetInfo.MaxLength);
                else if (baseFacetSetInfo.MaxLengthFixed && maxLength != baseFacetSetInfo.MaxLength)
                    CompilationContext.Throw(maxLengthFacet, ErrorKind.MaxLengthMustEqualToBaseMaxLengthIfBaseIsFixed, maxLength, baseFacetSetInfo.MaxLength);
                if (baseFacetSetInfo.MaxLengthFixed) maxLengthFixed = true;
            }
            //
            if (maxLength < minLength) {
                if (minLengthFacet != null)
                    CompilationContext.Throw(minLengthFacet, ErrorKind.MinLengthMustLessThanOrEqualToBaseMaxLength, minLength, maxLength);
                CompilationContext.Throw(maxLengthFacet, ErrorKind.MaxLengthMustGreaterThanOrEqualToBaseMinLength, maxLength, minLength);
            }
            //
            var totalDigitsFacet = TotalDigitsFacet;
            if (totalDigitsFacet != null) {
                totalDigits = totalDigitsFacet.Value;
                totalDigitsFixed = totalDigitsFacet.IsFixed;
            }
            if (baseFacetSetInfo != null) {
                if (totalDigits == null) totalDigits = baseFacetSetInfo.TotalDigits;
                else if (totalDigits > baseFacetSetInfo.TotalDigits)
                    CompilationContext.Throw(totalDigitsFacet, ErrorKind.TotalDigitsMustLessThanOrEqualToBaseTotalDigits, totalDigits, baseFacetSetInfo.TotalDigits);
                else if (baseFacetSetInfo.TotalDigitsFixed && totalDigits != baseFacetSetInfo.TotalDigits)
                    CompilationContext.Throw(totalDigitsFacet, ErrorKind.TotalDigitsMustEqualToBaseTotalDigitsIfBaseIsFixed, totalDigits, baseFacetSetInfo.TotalDigits);
                if (baseFacetSetInfo.TotalDigitsFixed) totalDigitsFixed = true;
            }
            //
            var fractionDigitsFacet = FractionDigitsFacet;
            if (fractionDigitsFacet != null) {
                fractionDigits = fractionDigitsFacet.Value;
                fractionDigitsFixed = fractionDigitsFacet.IsFixed;
            }
            if (baseFacetSetInfo != null) {
                if (fractionDigits == null) fractionDigits = baseFacetSetInfo.FractionDigits;
                else if (fractionDigits > baseFacetSetInfo.FractionDigits)
                    CompilationContext.Throw(fractionDigitsFacet, ErrorKind.FractionDigitsMustLessThanOrEqualToBaseFractionDigits, fractionDigits, baseFacetSetInfo.FractionDigits);
                else if (baseFacetSetInfo.FractionDigitsFixed && fractionDigits != baseFacetSetInfo.FractionDigits)
                    CompilationContext.Throw(fractionDigitsFacet, ErrorKind.FractionDigitsMustEqualToBaseFractionDigitsIfBaseIsFixed, fractionDigits, baseFacetSetInfo.FractionDigits);
                if (baseFacetSetInfo.FractionDigitsFixed) fractionDigitsFixed = true;
            }
            //
            if (totalDigits < fractionDigits) {
                if (fractionDigitsFacet != null)
                    CompilationContext.Throw(fractionDigitsFacet, ErrorKind.FractionDigitsMustLessThanOrEqualToBaseTotalDigits, fractionDigits, totalDigits);
                CompilationContext.Throw(totalDigitsFacet, ErrorKind.TotalDigitsMustGreaterThanOrEqualToBaseFractionDigits, totalDigits, fractionDigits);
            }
            //
            var lowerValueFacet = LowerValueFacet;
            if (lowerValueFacet != null) {
                lowerValue = lowerValueFacet.Literal.ParseAndValidate(iBaseTypeInfo);
                lowerValueInclusive = lowerValueFacet.IsInclusive;
                lowerValueFixed = lowerValueFacet.IsFixed;
                lowerValueText = lowerValueFacet.Text;
            }
            if (baseFacetSetInfo != null) {
                if (lowerValue == null) {
                    lowerValue = baseFacetSetInfo.LowerValue;
                    lowerValueInclusive = baseFacetSetInfo.LowerValueInclusive;
                    lowerValueText = baseFacetSetInfo.LowerValueText;
                }
                else if (baseFacetSetInfo.LowerValue != null) {
                    if (baseFacetSetInfo.LowerValueFixed && !MX.SimpleType.ValueEquals(lowerValue, baseFacetSetInfo.LowerValue))
                        CompilationContext.Throw(lowerValueFacet.Literal, ErrorKind.LowerValueMustEqualToBaseLowerValueIfBaseIsFixed, lowerValueFacet.Text, baseFacetSetInfo.LowerValueText);
                }
                if (baseFacetSetInfo.LowerValueFixed) lowerValueFixed = true;
            }
            //
            var upperValueFacet = UpperValueFacet;
            if (upperValueFacet != null) {
                upperValue = upperValueFacet.Literal.ParseAndValidate(iBaseTypeInfo);
                upperValueInclusive = upperValueFacet.IsInclusive;
                upperValueFixed = upperValueFacet.IsFixed;
                upperValueText = upperValueFacet.Text;
            }
            if (baseFacetSetInfo != null) {
                if (upperValue == null) {
                    upperValue = baseFacetSetInfo.UpperValue;
                    upperValueInclusive = baseFacetSetInfo.UpperValueInclusive;
                    upperValueText = baseFacetSetInfo.UpperValueText;
                }
                else if (baseFacetSetInfo.UpperValue != null) {
                    if (baseFacetSetInfo.UpperValueFixed && !MX.SimpleType.ValueEquals(upperValue, baseFacetSetInfo.UpperValue))
                        CompilationContext.Throw(upperValueFacet.Literal, ErrorKind.UpperValueMustEqualToBaseUpperValueIfBaseIsFixed, upperValueFacet.Text, baseFacetSetInfo.UpperValueText);
                }
                if (baseFacetSetInfo.UpperValueFixed) upperValueFixed = true;
            }
            //
            if (lowerValueFacet != null && upperValueFacet != null) {
                var r = MX.SimpleType.CompareValue(lowerValue, upperValue);
                if (r > 0) {
                    CompilationContext.Throw(upperValueFacet.Literal, ErrorKind.UpperValueMustGreaterThanOrEqualToLowerValue, upperValue, lowerValue);
                }
                else if (r == 0) {
                    if (lowerValueInclusive == false)
                        CompilationContext.Error(lowerValueFacet.Token, ErrorKind.LowerMustBeInclusiveIfLowerValueEqualToUpperValue);
                    if (upperValueInclusive == false)
                        CompilationContext.Error(upperValueFacet.Token, ErrorKind.UpperMustBeInclusiveIfLowerValueEqualToUpperValue);
                    CompilationContext.ThrowIfHasErrors();
                }
            }
            //
            var enumerationsFacet = EnumerationsFacet;
            if (enumerationsFacet != null) {
                var enumerationList = new List<EnumerationItemInfo>();
                foreach (var item in enumerationsFacet.ItemList)
                    enumerationList.Add(new EnumerationItemInfo(item.Name, item.Literal.ParseAndValidate(iBaseTypeInfo)));
                enumerations = enumerationList;
                enumerationsText = enumerationsFacet.Text;
            }
            else if (baseFacetSetInfo != null) {
                enumerations = baseFacetSetInfo.Enumerations;
                enumerationsText = baseFacetSetInfo.EnumerationsText;
            }
            //
            if (baseFacetSetInfo != null && baseFacetSetInfo.Patterns != null) patternList = baseFacetSetInfo.Patterns.ToList();
            var patternsFacet = PatternsFacet;
            if (patternsFacet != null) {
                if (patternList == null) patternList = new List<PatternItemInfo>();
                patternList.Add(new PatternItemInfo(patternsFacet.Pattern));
            }
            //
            var whitespaceFacet = WhitespaceFacet;
            if (whitespaceFacet != null) {
                whitespaceNormalization = whitespaceFacet.WhitespaceNormalization;
                whitespaceNormalizationFixed = whitespaceFacet.IsFixed;
            }
            if (baseFacetSetInfo != null) {
                if (whitespaceNormalization == null) whitespaceNormalization = baseFacetSetInfo.WhitespaceNormalization;
                else if (whitespaceNormalization < baseFacetSetInfo.WhitespaceNormalization)
                    CompilationContext.Throw(whitespaceFacet, ErrorKind.WhitespaceNormalizationMustStrongerThanOrEqualToBaseWhitespaceNormalization, whitespaceNormalization, baseFacetSetInfo.WhitespaceNormalization);
                else if (baseFacetSetInfo.WhitespaceNormalizationFixed && whitespaceNormalization != baseFacetSetInfo.WhitespaceNormalization)
                    CompilationContext.Throw(whitespaceFacet, ErrorKind.WhitespaceNormalizationMustEqualToBaseWhitespaceNormalizationIfBaseIsFixed, whitespaceNormalization, baseFacetSetInfo.WhitespaceNormalization);
                if (baseFacetSetInfo.WhitespaceNormalizationFixed) whitespaceNormalizationFixed = true;
            }
            //
            //
            return new FacetSetInfo(Keyword, baseTypeInfo.FacetSet, new MX.FacetSetInfo(minLength, minLengthFixed, maxLength, maxLengthFixed,
                totalDigits, totalDigitsFixed, fractionDigits, fractionDigitsFixed,
                lowerValue, lowerValueInclusive, lowerValueFixed, lowerValueText,
                upperValue, upperValueInclusive, upperValueFixed, upperValueText,
                enumerations, enumerationsText, patternList, whitespaceNormalization, whitespaceNormalizationFixed));
        }
        private static readonly Dictionary<FacetKind, HashSet<MX.TypeKind>> _facetApplicableMap = new Dictionary<FacetKind, HashSet<MX.TypeKind>> {
            {FacetKind.LengthRange, new HashSet<MX.TypeKind>(new MX.TypeKind[]{MX.TypeKind.ListedSimpleType, MX.TypeKind.String, MX.TypeKind.Base64Binary, MX.TypeKind.HexBinary, MX.TypeKind.Uri, MX.TypeKind.FullName,/* MX.TypeKind.Notation,*/
                    MX.TypeKind.NormalizedString, MX.TypeKind.Token, MX.TypeKind.Language, MX.TypeKind.NameToken, MX.TypeKind.Name, MX.TypeKind.NonColonizedName, MX.TypeKind.Id, MX.TypeKind.IdRef, MX.TypeKind.Entity, })},
            {FacetKind.TotalDigits, new HashSet<MX.TypeKind>(new MX.TypeKind[]{MX.TypeKind.Decimal,
                    MX.TypeKind.Integer, MX.TypeKind.NonPositiveInteger, MX.TypeKind.NegativeInteger, MX.TypeKind.NonNegativeInteger, MX.TypeKind.PositiveInteger,
                    MX.TypeKind.Int64, MX.TypeKind.Int32, MX.TypeKind.Int16, MX.TypeKind.SByte, MX.TypeKind.UInt64, MX.TypeKind.UInt32, MX.TypeKind.UInt16, MX.TypeKind.Byte, })},
            {FacetKind.FractionDigits, new HashSet<MX.TypeKind>(new MX.TypeKind[]{MX.TypeKind.Decimal})},
            {FacetKind.ValueRange, new HashSet<MX.TypeKind>(new MX.TypeKind[]{MX.TypeKind.Decimal, MX.TypeKind.Single, MX.TypeKind.Double, MX.TypeKind.TimeSpan, MX.TypeKind.DateTime, MX.TypeKind.Time, MX.TypeKind.Date, MX.TypeKind.YearMonth, MX.TypeKind.Year, MX.TypeKind.MonthDay, MX.TypeKind.Day, MX.TypeKind.Month,
                    MX.TypeKind.Integer, MX.TypeKind.NonPositiveInteger, MX.TypeKind.NegativeInteger, MX.TypeKind.NonNegativeInteger, MX.TypeKind.PositiveInteger,
                    MX.TypeKind.Int64, MX.TypeKind.Int32, MX.TypeKind.Int16, MX.TypeKind.SByte, MX.TypeKind.UInt64, MX.TypeKind.UInt32, MX.TypeKind.UInt16, MX.TypeKind.Byte, })},
            {FacetKind.Enumerations, null},
            {FacetKind.Patterns, null},
            {FacetKind.Whitespace, new HashSet<MX.TypeKind>(new MX.TypeKind[]{MX.TypeKind.String, MX.TypeKind.NormalizedString})},
        };
        //If {variety} is list, then  
        //[all datatypes] length, minLength, maxLength, pattern, enumeration, //whiteSpace fixed to the value collapse.
        //If {variety} is union, then  
        //[all datatypes] pattern, enumeration 
        internal void GenerateXsd(TextBuffer buf) {
            var minLengthFacet = MinLengthFacet;
            if (minLengthFacet != null) GenerateXsdCore(buf, "minLength", minLengthFacet.Value.ToInvariantString(), minLengthFacet.IsFixed);
            var maxLengthFacet = MaxLengthFacet;
            if (maxLengthFacet != null) GenerateXsdCore(buf, "maxLength", maxLengthFacet.Value.ToInvariantString(), maxLengthFacet.IsFixed);
            var totalDigitsFacet = TotalDigitsFacet;
            if (totalDigitsFacet != null) GenerateXsdCore(buf, "totalDigits", totalDigitsFacet.Value.ToInvariantString(), totalDigitsFacet.IsFixed);
            var fractionDigitsFacet = FractionDigitsFacet;
            if (fractionDigitsFacet != null) GenerateXsdCore(buf, "fractionDigits", fractionDigitsFacet.Value.ToInvariantString(), fractionDigitsFacet.IsFixed);
            var lowerValueFacet = LowerValueFacet;
            if (lowerValueFacet != null) GenerateXsdCore(buf, lowerValueFacet.IsInclusive ? "minInclusive" : "minExclusive", lowerValueFacet.XsdText, lowerValueFacet.IsFixed);
            var upperValueFacet = UpperValueFacet;
            if (upperValueFacet != null) GenerateXsdCore(buf, upperValueFacet.IsInclusive ? "maxInclusive" : "maxExclusive", upperValueFacet.XsdText, upperValueFacet.IsFixed);
            var enumerationsFacet = EnumerationsFacet;
            if (enumerationsFacet != null)
                foreach (var item in enumerationsFacet.ItemList)
                    GenerateXsdCore(buf, "enumeration", item.Literal.XsdText, false);
            var patternsFacet = PatternsFacet;
            if (patternsFacet != null)
                foreach (var pattern in patternsFacet.ItemList)
                    GenerateXsdCore(buf, "pattern", pattern.Value, false);
            var whitespaceFacet = WhitespaceFacet;
            if (whitespaceFacet != null) GenerateXsdCore(buf, "whiteSpace", whitespaceFacet.WhitespaceNormalization.ToXsd(), whitespaceFacet.IsFixed);
        }
        private static void GenerateXsdCore(TextBuffer buf, string tag, string value, bool isFixed) {
            buf.Write("<xs:{0} value='{1}'", tag, value.ToXmlString());
            if (isFixed) buf.Write(" fixed='true'");
            buf.WriteLine(" />");
        }
    }
    #endregion
    public sealed class RestrictedSimpleType : SimpleType {
        internal RestrictedSimpleType(UnresolvedType unresolvedType)
            : base(unresolvedType) {
            BaseTypeOrReference = unresolvedType.BaseTypeOrReference;
            FacetSet = unresolvedType.FacetSet;
        }
        internal readonly TypeOrReference BaseTypeOrReference;
        internal readonly FacetSet FacetSet;//opt
        protected override TypeInfo CreateInfoCore(XName fullName, string csName) {
            var baseTypeInfo = (SimpleTypeInfo)BaseTypeOrReference.CreateInfo(csName + "_BaseClass");
            if (baseTypeInfo.DerivationProhibition.IsSet(DerivationProhibition.Restriction))
                CompilationContext.Throw(Keyword, ErrorKind.RestrictionDerivationProhibited);
            return CreateSimpleTypeInfo(baseTypeInfo, fullName, FacetSet, DerivationProhibition, Keyword, this, csName, GetCSClass());
        }
        internal static SimpleTypeInfo CreateSimpleTypeInfo(SimpleTypeInfo baseInfo, XName fullName, FacetSet facetSet, DerivationProhibition derivationProhibition,
            SimpleToken keyword, Type obj, string csName, CSClass csClass) {
            var facetSetInfo = facetSet != null ? facetSet.CreateInfo(baseInfo) : baseInfo.FacetSet;
            var baseTypeKind = baseInfo.Kind;
            if (baseTypeKind == MX.TypeKind.ListedSimpleType)
                return new ListedSimpleTypeInfo(keyword, obj, csName, csClass, fullName, (ListedSimpleTypeInfo)baseInfo, derivationProhibition, facetSetInfo);
            else if (baseTypeKind == MX.TypeKind.UnitedSimpleType)
                return new UnitedSimpleTypeInfo(keyword, obj, csName, csClass, fullName, (UnitedSimpleTypeInfo)baseInfo, derivationProhibition, facetSetInfo);
            return new AtomicSimpleTypeInfo(keyword, obj, csName, csClass, baseInfo.Kind, fullName, baseInfo, derivationProhibition, baseInfo.ValueCSFullName,
                baseInfo.NullableValueCSFullName, baseInfo.ValueClrType, baseInfo.IsValueClrTypeRef, facetSetInfo);
        }
        internal override void GenerateXsd(TextBuffer buf) {
            buf.Write("<xs:simpleType");
            GenerateXsdTagPart(buf);
            buf.WriteLine(">");
            buf.PushIndent();
            buf.Write("<xs:restriction");
            var baseQName = BaseTypeOrReference.XsdQName;
            if (baseQName != null) buf.Write(" base='{0}'", baseQName);
            buf.WriteLine(">");
            buf.PushIndent();
            if (baseQName == null) BaseTypeOrReference.Type.GenerateXsd(buf);
            if (FacetSet != null) FacetSet.GenerateXsd(buf);
            buf.PopIndent();
            buf.WriteLine("</xs:restriction>");
            buf.PopIndent();
            buf.WriteLine("</xs:simpleType>");
        }
    }
    //
    //
    public abstract class ComplexType : Type {
        protected ComplexType(UnresolvedType unresolvedType)
            : base(unresolvedType) {
            AbstractKeyword = unresolvedType.AbstractKeyword;
            InstanceProhibitionValue = unresolvedType.InstanceProhibitionValue;
            BaseTypeOrReference = unresolvedType.BaseTypeOrReference;
            AttributeSet = unresolvedType.AttributeSet;
        }
        internal readonly SimpleToken AbstractKeyword;
        internal bool IsAbstract { get { return AbstractKeyword != null; } }
        internal readonly InstanceProhibitionValue InstanceProhibitionValue;
        internal InstanceProhibition InstanceProhibition { get { return InstanceProhibitionValue != null ? InstanceProhibitionValue.Value : LogicalNamespaceAncestor.InstanceProhibition; } }
        internal readonly TypeOrReference BaseTypeOrReference;//null for directness
        internal readonly RootAttributeSet AttributeSet;//opt
        //
        protected override sealed TypeInfo CreateInfoCore(XName fullName, string csName) {
            var isExtension = this is ExtendedSimpleChildComplexType || this is ExtendedComplexChildComplexType;
            TypeInfo baseTypeInfo = null;
            if (BaseTypeOrReference != null) {
                baseTypeInfo = BaseTypeOrReference.CreateInfo(null);
                if (baseTypeInfo.DerivationProhibition.IsSet(isExtension ? DerivationProhibition.Extension : DerivationProhibition.Restriction))
                    CompilationContext.Throw(Keyword, isExtension ? ErrorKind.ExtensionDerivationProhibited : ErrorKind.RestrictionDerivationProhibited);
            }
            var baseComplexTypeInfo = baseTypeInfo as ComplexTypeInfo;
            //
            var baseAttributeSetInfo = baseComplexTypeInfo == null ? null : baseComplexTypeInfo.AttributeSet;
            AttributeSetInfo attributeSetInfo;
            if (AttributeSet == null) attributeSetInfo = baseAttributeSetInfo;
            else {
                if (!isExtension && baseAttributeSetInfo == null) CompilationContext.Throw(Keyword, ErrorKind.CannotRestrictNullBaseAttributeSet);
                attributeSetInfo = AttributeSet.CreateInfo(baseAttributeSetInfo, isExtension);
                attributeSetInfo.IsCSClassOverride = baseAttributeSetInfo != null;
            }
            //
            SimpleTypeInfo simpleChildInfo = null;
            var needSimpleChildMembers = false;
            var hasSimpleChildClass = false;
            if (this is SimpleChildComplexType) {
                var baseSimpleChildInfo = baseTypeInfo as SimpleTypeInfo;
                if (baseSimpleChildInfo != null) needSimpleChildMembers = true;
                else {
                    baseSimpleChildInfo = baseComplexTypeInfo.SimpleChild;
                    hasSimpleChildClass = baseComplexTypeInfo.HasSimpleChildClass;
                }
                var restrictedSimpleChildComplexType = this as RestrictedSimpleChildComplexType;
                if (restrictedSimpleChildComplexType != null && restrictedSimpleChildComplexType.FacetSet != null) {
                    if (baseSimpleChildInfo.Kind == MX.TypeKind.SimpleType)
                        CompilationContext.Throw(BaseTypeOrReference, ErrorKind.CannotRestrictSystemRootSimpleType);
                    simpleChildInfo = RestrictedSimpleType.CreateSimpleTypeInfo(baseSimpleChildInfo, null, restrictedSimpleChildComplexType.FacetSet, DerivationProhibition.None,
                        restrictedSimpleChildComplexType.FacetSet.Keyword, restrictedSimpleChildComplexType, "SimpleChildClass", restrictedSimpleChildComplexType.GetSimpleChildCSClass());
                    simpleChildInfo.IsCSClassOverride = hasSimpleChildClass;
                    needSimpleChildMembers = true;
                    hasSimpleChildClass = true;
                }
                else simpleChildInfo = baseSimpleChildInfo;
            }
            //
            ChildStructInfo complexChildInfo = null;
            var isMixed = false;
            var complexChildComplexType = this as ComplexChildComplexType;
            if (complexChildComplexType != null) {
                isMixed = complexChildComplexType.IsMixed;
                if (baseComplexTypeInfo != null) {
                    var baseIsMixed = baseComplexTypeInfo.IsMixed;
                    if (baseIsMixed != isMixed && (isMixed || isExtension))
                        CompilationContext.Throw(Keyword, ErrorKind.MixedNotEqualToBase, isMixed, baseIsMixed);
                }
                var baseComplexChildInfo = baseComplexTypeInfo == null ? null : baseComplexTypeInfo.ComplexChild;
                if (complexChildComplexType.ChildStruct == null) complexChildInfo = baseComplexChildInfo;
                else {
                    if (!isExtension && baseComplexChildInfo == null) CompilationContext.Throw(Keyword, ErrorKind.CannotRestrictNullBaseChildStruct);
                    complexChildInfo = complexChildComplexType.ChildStruct.CreateInfo(baseComplexChildInfo, isExtension, isMixed);
                    complexChildInfo.IsCSClassOverride = baseComplexChildInfo != null;
                }
            }
            //
            return new ComplexTypeInfo(Keyword, this, csName, GetCSClass(), fullName, baseComplexTypeInfo, isExtension, IsAbstract, isMixed, DerivationProhibition, InstanceProhibition,
               attributeSetInfo, simpleChildInfo, needSimpleChildMembers, hasSimpleChildClass, complexChildInfo);
        }
        //
        internal override sealed void GenerateXsd(TextBuffer buf) {
            buf.Write("<xs:complexType");
            GenerateXsdTagPart(buf);
            buf.WriteLine(">");
            buf.PushIndent();
            GenerateXsdTypeContent(buf);
            buf.PopIndent();
            buf.WriteLine("</xs:complexType>");
        }
        protected override void GenerateXsdTagPart(TextBuffer buf) {
            base.GenerateXsdTagPart(buf);
            if (IsAbstract) buf.Write(" abstract='true'");
            if (InstanceProhibitionValue != null) buf.Write(" block='{0}'", InstanceProhibitionValue.Value.ToXsd());
        }
        protected abstract void GenerateXsdTypeContent(TextBuffer buf);
        protected void GenerateXsdAttributeSet(TextBuffer buf) {//BUG!! 没有考虑这种情况： <attribute name="A1" use="prohibited" ></attribute>
            if (AttributeSet != null) AttributeSet.GenerateXsd(buf);
        }
    }
    public abstract class SimpleChildComplexType : ComplexType {
        protected SimpleChildComplexType(UnresolvedType unresolvedType) : base(unresolvedType) { }
        protected override sealed void GenerateXsdTypeContent(TextBuffer buf) {
            buf.WriteLine("<xs:simpleContent>");
            buf.PushIndent();
            GenerateXsdModelContent(buf);
            buf.PopIndent();
            buf.WriteLine("</xs:simpleContent>");
        }
        protected abstract void GenerateXsdModelContent(TextBuffer buf);
    }
    public sealed class ExtendedSimpleChildComplexType : SimpleChildComplexType {
        internal ExtendedSimpleChildComplexType(UnresolvedType unresolvedType) : base(unresolvedType) { }
        //
        protected override void GenerateXsdModelContent(TextBuffer buf) {
            buf.WriteLine("<xs:extension base='{0}'>", BaseTypeOrReference.XsdQName);
            buf.PushIndent();
            GenerateXsdAttributeSet(buf);
            buf.PopIndent();
            buf.WriteLine("</xs:extension>");
        }
    }
    public sealed class RestrictedSimpleChildComplexType : SimpleChildComplexType {
        internal RestrictedSimpleChildComplexType(UnresolvedType unresolvedType)
            : base(unresolvedType) {
            FacetSet = unresolvedType.FacetSet;
        }
        internal readonly FacetSet FacetSet;//opt
        internal CSClass GetSimpleChildCSClass() { return FacetSet == null ? null : FacetSet.GetCSClass(); }
        //
        protected override void GenerateXsdModelContent(TextBuffer buf) {
            buf.WriteLine("<xs:restriction base='{0}'>", BaseTypeOrReference.XsdQName);
            buf.PushIndent();
            if (FacetSet != null) FacetSet.GenerateXsd(buf);
            GenerateXsdAttributeSet(buf);
            buf.PopIndent();
            buf.WriteLine("</xs:restriction>");
        }
    }
    public abstract class ComplexChildComplexType : ComplexType {
        protected ComplexChildComplexType(UnresolvedType unresolvedType)
            : base(unresolvedType) {
            MixedKeyword = unresolvedType.MixedKeyword;
            ChildStruct = unresolvedType.ChildStruct;
        }
        internal readonly SimpleToken MixedKeyword;
        internal bool IsMixed { get { return MixedKeyword != null; } }
        internal readonly RootChildStruct ChildStruct;//opt
        //
        protected override sealed void GenerateXsdTagPart(TextBuffer buf) {
            base.GenerateXsdTagPart(buf);
            if (IsMixed) buf.Write(" mixed='true'");
        }
        protected override sealed void GenerateXsdTypeContent(TextBuffer buf) {
            if (BaseTypeOrReference == null) {
                GenerateXsdChildStruct(buf, false);
                GenerateXsdAttributeSet(buf);
            }
            else {
                buf.WriteLine("<xs:complexContent>");
                buf.PushIndent();
                var derTag = this is ExtendedComplexChildComplexType ? "extension" : "restriction";
                buf.WriteLine("<xs:{0} base='{1}'>", derTag, BaseTypeOrReference.XsdQName);
                buf.PushIndent();
                GenerateXsdChildStruct(buf, ChildStruct == null && this is RestrictedComplexChildComplexType);
                GenerateXsdAttributeSet(buf);
                buf.PopIndent();
                buf.WriteLine("</xs:{0}>", derTag);
                buf.PopIndent();
                buf.WriteLine("</xs:complexContent>");
            }
        }
        private void GenerateXsdChildStruct(TextBuffer buf, bool useBase) {
            if (ChildStruct != null) ChildStruct.GenerateXsd(buf);
            else if (useBase && BaseTypeOrReference != null) {//BUG!!
                var baseType = BaseTypeOrReference.Type as ComplexChildComplexType;
                if (baseType != null) baseType.GenerateXsdChildStruct(buf, true);
            }
        }
    }
    public sealed class ExtendedComplexChildComplexType : ComplexChildComplexType {
        internal ExtendedComplexChildComplexType(UnresolvedType unresolvedType) : base(unresolvedType) { }
    }
    public sealed class RestrictedComplexChildComplexType : ComplexChildComplexType {
        internal RestrictedComplexChildComplexType(UnresolvedType unresolvedType) : base(unresolvedType) { }
    }
    //
    //
    internal interface IMemberObject {
        SimpleToken Keyword { get; }
        Identifier MemberNameId { get; }
        IMemberObject MergeTo(IMemberObject other);
    }
    internal sealed class MemberObjectListEx<T> where T : class, IMemberObject {
        internal MemberObjectListEx() { }
        internal sealed class List : List<T> { }
        private readonly List<List> _listList = new List<List>();
        internal void AddMember(T obj) {
            if (_listList.Count == 0) _listList.Add(new List());
            var list = _listList[0];
            var memberNameId = obj.MemberNameId;
            foreach (var i in list)
                if (i.MemberNameId == memberNameId)
                    CompilationContext.Throw(obj.Keyword, ErrorKind.DuplicateMemberName, memberNameId);
            list.Add(obj);
        }
        //internal int Count {
        //    get {
        //        if (_listList.Count == 0) return 0;
        //        return _listList[0].Count;
        //    }
        //}
        //internal List RawList {
        //    get {
        //        if (_listList.Count == 0) return null;
        //        return _listList[0];
        //    }
        //}
        internal MemberObjectListEx<T> MergeTo(MemberObjectListEx<T> other) {
            if (other == null) return this;
            other._listList.AddRange(_listList);
            return other;
        }
        private List _resultList;
        internal List GetResult(bool ordered) {
            if (_resultList == null) {
                if (_listList.Count == 0) _resultList = new List();
                else {
                    var resList = _listList[0];
                    if (_listList.Count > 1) {
                        var idx = 0;
                        for (var i = 1; i < _listList.Count; i++) {
                            if (_listList[i].Count > resList.Count) {
                                resList = _listList[i];
                                idx = i;
                            }
                        }
                        _listList.RemoveAt(idx);
                        foreach (var list in _listList) {
                            var resListIdx = 0;
                            foreach (var member in list) {
                                var found = false;
                                for (; resListIdx < resList.Count; resListIdx++) {
                                    if (member.MemberNameId == resList[resListIdx].MemberNameId) {
                                        resList[resListIdx] = (T)member.MergeTo(resList[resListIdx]);
                                        found = true;
                                        resListIdx++;
                                        break;
                                    }
                                }
                                if (!found) {
                                    if (ordered) CompilationContext.Throw(member.Keyword, ErrorKind.UnexpectedMemberName, member.MemberNameId);
                                    resList.Add(member);
                                }
                                if (!ordered) resListIdx = 0;
                            }
                        }
                    }
                    _resultList = resList;
                }
            }
            return _resultList;
        }
    }
    public sealed class DefaultOrFixedValue : ValueBase {
        internal DefaultOrFixedValue(Namespace nsObj, Node node) {
            base.Initialize(node);
            var keywordNode = node.Member("Keyword");
            Keyword = new SimpleToken(keywordNode);
            IsDefault = keywordNode.MemberCSTokenKind() == SyntaxKind.DefaultKeyword;
            Literal = new Literal(nsObj, node.Member("Value"));
        }
        internal const string NodeLabel = "DefaultOrFixedValue";
        internal readonly SimpleToken Keyword;
        internal readonly bool IsDefault;
        internal bool IsFixed { get { return !IsDefault; } }
        internal readonly Literal Literal;
        internal bool TextEquals(DefaultOrFixedValue other) { return IsDefault == other.IsDefault && Literal.TextEquals(other.Literal); }
        private string _text;
        public override string ToString() { return _text ?? (_text = (IsDefault ? "default: " : "fixed: ") + Literal.Text); }
        internal DefaultOrFixedValueInfo CreateInfo(ISimpleTypeInfo simpleTypeInfo) {
            return new DefaultOrFixedValueInfo(IsDefault, Literal.ParseAndValidate(simpleTypeInfo), Literal.Text);
        }
        internal void GenerateXsd(TextBuffer buf) {
            buf.Write(" {0}='{1}'", IsDefault ? "default" : "fixed", Literal.Text.ToXmlString());
        }
    }
    public sealed class WildcardUri : ValueBase, IEquatable<WildcardUri> {
        internal WildcardUri(Namespace nsObj, Node node) {
            base.Initialize(node);
            if (node.Label == UriOrAlias.NodeLabel) {
                var uri = new UriOrAlias(nsObj, node).Uri;
                if (uri.IsEmpty()) Kind = WildcardUriKind.Unqualified;
                else {
                    Kind = WildcardUriKind.Specific;
                    Value = uri;
                }
            }
            else if (node.IsXToken()) {
                switch (node.MemberXTokenKind()) {
                    case XTokenKind.AnyKeyword:
                        Kind = WildcardUriKind.Any;
                        break;
                    case XTokenKind.OtherKeyword:
                        Kind = WildcardUriKind.Other;
                        Value = nsObj.Uri;
                        break;
                    case XTokenKind.UnqualifiedKeyword:
                        Kind = WildcardUriKind.Unqualified;
                        break;
                    default: throw new InvalidOperationException();
                }
            }
            else {//ThisKeyword
                Kind = WildcardUriKind.Specific;
                Value = nsObj.Uri;
            }
        }
        internal readonly WildcardUriKind Kind;
        internal readonly XNamespace Value;//opt
        private WildcardUriInfo _info;
        internal WildcardUriInfo Info { get { return _info ?? (_info = new WildcardUriInfo(Kind, Value)); } }
        public override string ToString() { return Info.ToString(); }
        public bool Equals(WildcardUri other) {
            if (object.ReferenceEquals(this, other)) return true;
            if (object.ReferenceEquals(other, null)) return false;
            return Kind == other.Kind && Value == other.Value;
        }
        public override bool Equals(object obj) { return Equals(obj as WildcardUri); }
        public override int GetHashCode() { return Extensions.CombineHash(Kind.GetHashCode(), Value == null ? 0 : Value.GetHashCode()); }
        public static bool operator ==(WildcardUri left, WildcardUri right) {
            if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
            return left.Equals(right);
        }
        public static bool operator !=(WildcardUri left, WildcardUri right) { return !(left == right); }
    }
    public sealed class WildcardValidationValue : SimpleValue<WildcardValidation> {
        internal WildcardValidationValue(Node node) {
            base.Initialize(node);
            switch (node.Singleton.MemberXTokenKind()) {
                case XTokenKind.SkipValidateKeyword: Value = WildcardValidation.SkipValidate; break;
                case XTokenKind.TryValidateKeyword: Value = WildcardValidation.TryValidate; break;
                case XTokenKind.MustValidateKeyword: Value = WildcardValidation.MustValidate; break;
                default: throw new InvalidOperationException();
            }
        }
    }
    public sealed class Wildcard : ValueBase, IEquatable<Wildcard> {
        internal Wildcard(SimpleToken keyword, Namespace nsObj, Node node) {
            Keyword = keyword;
            base.Initialize(node);
            var urisNode = node.Member("Uris");
            foreach (var uriNode in urisNode.Items) {
                var uri = new WildcardUri(nsObj, uriNode);
                if (UriList.Contains(uri)) CompilationContext.Throw(uri, ErrorKind.DuplicateWildcardUri, uri);
                UriList.Add(uri);
            }
            bool hasAny = false, hasOther = false;
            foreach (var uri in UriList) {
                switch (uri.Kind) {
                    case WildcardUriKind.Any: hasAny = true; break;
                    case WildcardUriKind.Other: hasOther = true; break;
                }
            }
            if ((hasAny || hasOther) && UriList.Count > 1) CompilationContext.Throw(urisNode, ErrorKind.InvalidWildcardUris);
            ValidationValue = new WildcardValidationValue(node.Member("Validation"));
        }
        internal readonly SimpleToken Keyword;
        internal readonly List<WildcardUri> UriList = new List<WildcardUri>();
        internal readonly WildcardValidationValue ValidationValue;
        internal WildcardValidation Validation { get { return ValidationValue.Value; } }
        private WildcardInfo _info;
        internal WildcardInfo Info {
            get { return _info ?? (_info = new WildcardInfo(UriList.Select(i => i.Info), Validation, Keyword)); }
        }
        public override string ToString() { return Info.ToString(); }
        public bool Equals(Wildcard other) {
            if (object.ReferenceEquals(this, other)) return true;
            if (object.ReferenceEquals(other, null)) return false;
            if (UriList.Count != other.UriList.Count) return false;
            for (var i = 0; i < UriList.Count; i++)
                if (UriList[i] != other.UriList[i]) return false;
            return Validation == other.Validation;
        }
        public override bool Equals(object obj) { return Equals(obj as Wildcard); }
        public override int GetHashCode() { return Extensions.CombineHash(UriList.Count.GetHashCode(), Validation.GetHashCode()); }
        public static bool operator ==(Wildcard left, Wildcard right) {
            if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
            return left.Equals(right);
        }
        public static bool operator !=(Wildcard left, Wildcard right) { return !(left == right); }
        internal void GenerateXsd(TextBuffer buf) {
            buf.Write(" namespace='");
            foreach (var ns in UriList) {
                if (ns.Kind == WildcardUriKind.Specific) buf.Write(" " + ns.Value);
                else buf.Write(" " + ns.Kind.ToXsd());
            }
            buf.Write("' processContents='{0}'", Validation.ToXsd());
        }
    }
    //
    //
    public abstract class AttributeSet : AnnotatableObject, IDisplayableObject {
        protected AttributeSet() { }
        internal const string NodeLabel = "AttributeSet";
        internal SimpleToken Keyword { get; private set; }
        private MemberObjectListEx<AttributeMember> _memberListEx;
        internal MemberObjectListEx<AttributeMember> MemberListEx { get { return _memberListEx ?? (_memberListEx = new MemberObjectListEx<AttributeMember>()); } }
        internal IReadOnlyList<AttributeMember> MemberList { get { return MemberListEx.GetResult(false); } }
        private string _displayName;
        public string DisplayName { get { return _displayName ?? (_displayName = GetAncestor<IDisplayableObject>().DisplayName + "." + GetThisDisplayName()); } }
        protected abstract string GetThisDisplayName();//only this part, not including dot
        protected override void Initialize(Node node) {
            base.Initialize(node);
            Keyword = new SimpleToken(node.Member("Keyword"));
            foreach (var memberNode in node.Member("Members").Items) {
                AttributeMember member;
                switch (memberNode.Label) {
                    case Attribute.NodeLabel: member = new LocalAttribute(this, memberNode); break;
                    case AttributeReference.NodeLabel: member = new AttributeReference(this, memberNode); break;
                    case AttributeSetReference.NodeLabel: member = new AttributeSetReference(this, memberNode); break;
                    default: throw new InvalidOperationException();
                }
                MemberListEx.AddMember(member);
            }
            var wildcardNode = node.Member("Wildcard");
            if (wildcardNode.IsNotNull) MemberListEx.AddMember(new AttributesWildcard(this, wildcardNode));
        }
        internal virtual AttributeSet MergeTo(AttributeSet otherSet) {
            if (otherSet == null) return this;
            MemberListEx.MergeTo(otherSet.MemberListEx);
            return otherSet;
        }
        //
        internal void GenerateXsd(TextBuffer buf) {
            //if (IsGlobal) {
            //    buf.WriteLine("<xs:attributeGroup name='{0}'>", Name);
            //    buf.PushIndent();
            //}
            //foreach (var member in MemberList) member.GenerateXsd(buf);
            //if (IsGlobal) {
            //    buf.PopIndent();
            //    buf.WriteLine("</xs:attributeGroup>");
            //}
        }
    }
    public sealed class RootAttributeSet : AttributeSet {
        internal RootAttributeSet(UnresolvedType parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal CSClass CSClass { get; private set; }
        internal CSClass GetCSClass() { return CSClass.Clone(); }
        protected override string GetThisDisplayName() { return "Attributes"; }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            CSClass = new X.CSClass(this, node.Member("CSClass"), Keyword);
        }
        internal override AttributeSet MergeTo(AttributeSet otherSet) {
            if (otherSet == null) return this;
            var other = otherSet as RootAttributeSet;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, GetType().Name, otherSet.GetType().Name);
            CSClass.MergeTo(other.CSClass);
            return other;
        }
        internal AttributeSetInfo CreateInfo(AttributeSetInfo baseSetInfo, bool isExtension) {
            var setInfo = new AttributeSetInfo(Keyword, this, "AttributeSetClass", GetCSClass(), baseSetInfo, isExtension);
            foreach (var member in MemberList) member.CreateInfo(setInfo);
            if (setInfo.TempWildcardList != null) {
                var wildcardInfo = setInfo.TempWildcardList[0];
                for (var i = 1; i < setInfo.TempWildcardList.Count; i++) {
                    wildcardInfo = wildcardInfo.TryIntersectWith(setInfo.TempWildcardList[i]);
                    if (wildcardInfo == null) break;
                }
                setInfo.Wildcard = wildcardInfo;
            }
            var baseWildcardInfo = baseSetInfo == null ? null : baseSetInfo.Wildcard;
            if (isExtension) {
                if (setInfo.Wildcard == null) setInfo.Wildcard = baseWildcardInfo;
                else if (baseWildcardInfo != null) {
                    var wildcardInfo = baseWildcardInfo.TryUniteWith(setInfo.Wildcard);
                    if (wildcardInfo == null) CompilationContext.Throw(Keyword, ErrorKind.CannotUniteWildcardWith, baseWildcardInfo.UrisText, setInfo.Wildcard.UrisText);
                    setInfo.Wildcard = wildcardInfo;
                }
            }
            else {//restriction
                foreach (var baseAttributeInfo in baseSetInfo.AttributeList) {
                    if (!baseAttributeInfo.IsOptional && !setInfo.ContainsAttribute(baseAttributeInfo.Name))
                        CompilationContext.Error(Keyword, ErrorKind.RequiredAttributeNotRestricting, baseAttributeInfo.Name);
                }
                CompilationContext.ThrowIfHasErrors();
                if (setInfo.Wildcard != null) {
                    if (baseWildcardInfo == null) CompilationContext.Throw(Keyword, ErrorKind.RestrictedAttributesWildcardNotFound);
                    if (!setInfo.Wildcard.IsEqualToOrRestrictedThan(baseWildcardInfo))
                        CompilationContext.Throw(Keyword, ErrorKind.RestrictingWildcardNotEqualToOrRestrictedThanRestricted, setInfo.Wildcard, baseWildcardInfo);
                }
            }
            return setInfo;
        }
    }
    public sealed class GlobalAttributeSet : AttributeSet {
        internal GlobalAttributeSet(Namespace parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal Identifier NameId { get; private set; }
        internal string Name { get { return NameId.PlainValue; } }
        protected override string GetThisDisplayName() { return "Attributes'" + Name + "'"; }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            NameId = new Identifier(node.Member("Name"));
        }
    }
    public abstract class AttributeMember : AnnotatableObject, IMemberObject, IDisplayableObject {
        protected AttributeMember() { }
        public SimpleToken Keyword { get; private set; }
        internal virtual bool IsMember { get { return true; } }
        internal Identifier MemberNameIdOpt { get; private set; }
        private Identifier _memberNameId;
        public Identifier MemberNameId {
            get {
                if (_memberNameId == null) {
                    if (!IsMember) throw new InvalidOperationException();
                    _memberNameId = MemberNameIdOpt ?? TryGetDefaultMemberNameId();
                    if (_memberNameId == null) CompilationContext.Throw(Keyword, ErrorKind.NameOrMemberNameRequired);
                }
                return _memberNameId;
            }
        }
        protected abstract Identifier TryGetDefaultMemberNameId();
        private string _displayName;
        public string DisplayName { get { return _displayName ?? (_displayName = GetAncestor<IDisplayableObject>().DisplayName + "." + GetThisDisplayName()); } }
        protected virtual string GetThisDisplayName() { return "'" + MemberNameId.PlainValue + "'"; }//only this part, not including dot
        protected override bool InitializeAnnotations(string name, Node node) {
            switch (name) {
                case EX.MemberNameNodeLabel:
                    if (MemberNameIdOpt != null) return false;
                    MemberNameIdOpt = new Identifier(node.Singleton);
                    return true;
            }
            return base.InitializeAnnotations(name, node);
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            Keyword = new SimpleToken(node.Member("Keyword"));
        }
        internal abstract AttributeMember MergeTo(AttributeMember otherMember);
        IMemberObject IMemberObject.MergeTo(IMemberObject other) { return MergeTo((AttributeMember)other); }
        private bool _isProcessing;
        internal AttributeInfo CreateInfo(AttributeSetInfo setInfo) {
            if (_isProcessing) CompilationContext.Throw(Keyword, ErrorKind.CircularReferenceDetected);
            _isProcessing = true;
            var info = CreateInfoCore(setInfo);
            _isProcessing = false;
            return info;
        }
        protected abstract AttributeInfo CreateInfoCore(AttributeSetInfo setInfo);
        internal abstract void GenerateXsd(TextBuffer buf);
    }
    public sealed class AttributeSetReference : AttributeMember {
        internal AttributeSetReference(AttributeSet parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal const string NodeLabel = "AttributeSetReference";
        internal QualifiableNameEx QNameOpt { get; private set; }
        internal QualifiableNameEx QName {
            get {
                if (QNameOpt == null) CompilationContext.Throw(Keyword, ErrorKind.QualifiableNameRequired);
                return QNameOpt;
            }
        }
        private GlobalAttributeSet _value;
        internal GlobalAttributeSet Value { get { return _value ?? (_value = QName.Resolve<GlobalAttributeSet>(NameResolutionKind.AttributeSet)); } }
        internal IReadOnlyList<AttributeMember> MemberList { get { return Value.MemberList; } }
        protected override Identifier TryGetDefaultMemberNameId() { return QNameOpt != null ? QNameOpt.NameId : null; }
        private string _errorMessageSuffix;
        internal string GetErrorMessageSuffix() { return _errorMessageSuffix ?? (_errorMessageSuffix = " ->at " + DisplayName); }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            QNameOpt = node.Member("QName").ToQualifiableNameExOpt(this);
        }
        internal override AttributeMember MergeTo(AttributeMember otherMember) {
            if (otherMember == null) return this;
            var other = otherMember as AttributeSetReference;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, GetType().Name, otherMember.GetType().Name);
            if (QNameOpt != null) other.QNameOpt = QNameOpt.MergeTo(other.QNameOpt);
            return other;
        }
        protected override AttributeInfo CreateInfoCore(AttributeSetInfo setInfo) {
            Error.PushMessageSuffix(GetErrorMessageSuffix());
            try { foreach (var member in MemberList) member.CreateInfo(setInfo); }
            finally { Error.PopMessageSuffix(); }
            return null;
        }
        internal override void GenerateXsd(TextBuffer buf) {
            buf.WriteLine("<xs:attributeGroup ref='{0}:{1}' />", LogicalNamespaceAncestor.GetXsdPrefix(Value), Value.Name);
        }
    }
    public abstract class AttributeBase : AttributeMember {
        protected AttributeBase() { }
        internal DefaultOrFixedValue DefaultOrFixedValueOpt { get; private set; }
        internal virtual DefaultOrFixedValue DefaultOrFixedValue { get { return DefaultOrFixedValueOpt; } }
        internal CSClass CSClass { get; private set; }
        internal CSClass GetCSClass() { return CSClass.Clone(); }
        internal abstract EntityDeclarationKind Kind { get; }
        internal virtual bool IsOptional { get { return false; } }
        protected override bool InitializeAnnotations(string name, Node node) {
            switch (name) {
                case DefaultOrFixedValue.NodeLabel:
                    if (DefaultOrFixedValueOpt != null) return false;
                    DefaultOrFixedValueOpt = new DefaultOrFixedValue(NamespaceAncestor, node);
                    return true;
            }
            return base.InitializeAnnotations(name, node);
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            CSClass = new X.CSClass(this, node.Member("CSClass"), Keyword);
        }
        internal override AttributeMember MergeTo(AttributeMember otherMember) {
            if (otherMember == null) return this;
            var other = otherMember as AttributeBase;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, GetType().Name, otherMember.GetType().Name);
            if (DefaultOrFixedValueOpt != null) {
                if (other.DefaultOrFixedValueOpt == null) other.DefaultOrFixedValueOpt = DefaultOrFixedValueOpt;
                else if (!DefaultOrFixedValueOpt.TextEquals(other.DefaultOrFixedValueOpt))
                    CompilationContext.Throw(DefaultOrFixedValueOpt, ErrorKind.DefaultOrFixedValueNotEqaulTo, DefaultOrFixedValueOpt, other.DefaultOrFixedValueOpt);
            }
            CSClass.MergeTo(other.CSClass);
            return other;
        }
        protected override sealed AttributeInfo CreateInfoCore(AttributeSetInfo setInfo) {
            var kind = Kind;
            var isGlobal = kind == EntityDeclarationKind.Global;
            var isRef = kind == EntityDeclarationKind.Reference;
            var memberNameId = isGlobal ? null : MemberNameId;
            var attribute = this as Attribute;
            //var globalAttribute = this as GlobalAttribute;
            var attributeRef = this as AttributeReference;
            AttributeInfo referentialAttributeInfo = isRef ? attributeRef.ReferentialAttributeInfo : null;
            var fullName = isRef ? referentialAttributeInfo.Name : attribute.FullName;
            var isOptional = IsOptional;
            AttributeInfo restrictedInfo = null;
            if (setInfo != null) {//for global att, setInfo is null
                if (setInfo.ContainsAttribute(fullName))
                    CompilationContext.Throw(Keyword, ErrorKind.DuplicateAttributeFullName, fullName);
                if (setInfo.IsExtension) {
                    if (!setInfo.AllMemberNameIdSet.Add(memberNameId))
                        CompilationContext.Throw(Keyword, ErrorKind.DuplicateMemberName, memberNameId);
                }
                else {
                    var baseSetInfo = setInfo.BaseAttributeSet;
                    restrictedInfo = baseSetInfo.TryGetAttribute(fullName);
                    if (restrictedInfo != null) {
                        if (memberNameId != restrictedInfo.MemberNameId)
                            CompilationContext.Throw(Keyword, ErrorKind.RestrictingAttributeMemberNameNotEqualToRestrictedAttribute, memberNameId, restrictedInfo.MemberNameId);
                        if (isOptional && !restrictedInfo.IsOptional)
                            CompilationContext.Throw(Keyword, ErrorKind.RestrictingAttributeIsOptionalButRestrictedAttributeIsRequired);
                    }
                    else {
                        if (baseSetInfo.Wildcard == null)
                            CompilationContext.Throw(Keyword, ErrorKind.RestrictedAttributeNotFoundAndNoBaseWildcard, fullName);
                        if (!baseSetInfo.Wildcard.IsMatch(fullName.Namespace))
                            CompilationContext.Throw(Keyword, ErrorKind.RestrictingAttributeNamespaceNotMatchWithBaseWildcard, fullName.Namespace, baseSetInfo.Wildcard.UrisText);
                        if (!setInfo.AllMemberNameIdSet.Add(memberNameId))
                            CompilationContext.Throw(Keyword, ErrorKind.DuplicateMemberName, memberNameId);
                    }
                }
            }
            var typeInfo = isRef ? referentialAttributeInfo.Type : attribute.CreateTypeInfo();
            if (restrictedInfo != null) {
                if (!typeInfo.IsEqualToOrRestrictedDeriveFrom(restrictedInfo.Type))
                    CompilationContext.Throw(Keyword, ErrorKind.RestrictingAttributeTypeNotEqualToOrRestrictedDeriveFromRestrictedAttributeType);
            }
            var restrictedDfValueInfo = restrictedInfo == null ? null : restrictedInfo.DefaultOrFixedValue;
            DefaultOrFixedValueInfo dfValueInfo = null;
            var dfValue = DefaultOrFixedValue;
            if (dfValue != null) {
                dfValueInfo = dfValue.CreateInfo(typeInfo);
                //if (dfValueInfo.IsDefault && isRequired)
                //    Errors.Throw(Keyword, ErrorKind.AttributeHasDefaultValueMustBeOptional);
                if (restrictedDfValueInfo != null && restrictedDfValueInfo.IsFixed) {
                    if (dfValueInfo.IsDefault) CompilationContext.Throw(Keyword, ErrorKind.RestrictedAttributeAndRestrictingAttributeMustBothHasFixedValueOrNeither);
                    if (!MX.SimpleType.ValueEquals(dfValueInfo.Value, restrictedDfValueInfo.Value))
                        CompilationContext.Throw(Keyword, ErrorKind.RestrictedAttributeFixedValueNotEqualToRestrictingAttributeFixedValue, dfValueInfo.ValueText, restrictedDfValueInfo.ValueText);
                }
            }
            else if (restrictedDfValueInfo != null && restrictedDfValueInfo.IsFixed)
                CompilationContext.Throw(Keyword, ErrorKind.RestrictedAttributeAndRestrictingAttributeMustBothHasFixedValueOrNeither);
            AttributeInfo info;
            var csName = isGlobal ? fullName.LocalName + "_AttributeClass" : memberNameId.Value + "_Class";
            if (isRef) {
                info = new AttributeInfo(Keyword, this, csName, GetCSClass(), referentialAttributeInfo, memberNameId, isOptional, dfValueInfo, restrictedInfo);
            }
            else {
                info = new AttributeInfo(Keyword, this, csName, GetCSClass(), kind, fullName, memberNameId, isOptional, dfValueInfo, typeInfo, restrictedInfo);
            }
            info.IsCSClassOverride = restrictedInfo != null;
            if (setInfo != null) setInfo.AttributeList.Add(info);
            return info;
        }
    }
    public abstract class Attribute : AttributeBase {
        protected Attribute() { }
        internal const string NodeLabel = "Attribute";
        internal Identifier NameIdOpt { get; private set; }//opt
        internal Identifier NameId {
            get {
                if (NameIdOpt == null) CompilationContext.Throw(Keyword, ErrorKind.NameRequired);
                return NameIdOpt;
            }
        }
        internal string Name { get { return NameId.PlainValue; } }
        protected override sealed Identifier TryGetDefaultMemberNameId() { return NameIdOpt; }
        internal TypeOrReference TypeOrReferenceOpt { get; private set; }
        internal TypeOrReference TypeOrReference {
            get {
                if (TypeOrReferenceOpt == null) CompilationContext.Throw(Keyword, ErrorKind.TypeRequired);
                return TypeOrReferenceOpt;
            }
        }
        internal SimpleTypeInfo CreateTypeInfo() {
            var typeInfo = TypeOrReference.CreateInfo("TypeClass") as SimpleTypeInfo;
            if (typeInfo == null) CompilationContext.Throw(TypeOrReference, ErrorKind.SimpleTypeRequired);
            return typeInfo;
        }
        internal abstract XName FullName { get; }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            NameIdOpt = node.Member("Name").ToIdentifierOpt();
            TypeOrReferenceOpt = node.Member("TypeOrReference").ToTypeOrReferenceOpt(this);
        }
        internal override AttributeMember MergeTo(AttributeMember otherMember) {
            if (otherMember == null) return this;
            var other = otherMember as Attribute;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, GetType().Name, otherMember.GetType().Name);
            base.MergeTo(other);
            if (NameIdOpt != null) {
                if (other.NameIdOpt == null) other.NameIdOpt = NameIdOpt;
                else if (NameIdOpt != other.NameIdOpt)
                    CompilationContext.Throw(NameIdOpt, ErrorKind.NameNotEqualTo, NameIdOpt, other.NameIdOpt);
            }
            if (TypeOrReferenceOpt != null) other.TypeOrReferenceOpt = TypeOrReferenceOpt.MergeTo(other.TypeOrReferenceOpt);
            return other;
        }
        internal override void GenerateXsd(TextBuffer buf) {
            buf.Write("<xs:attribute name='{0}'", Name);
            GenerateXsdTagPart(buf);
            if (DefaultOrFixedValueOpt != null) DefaultOrFixedValueOpt.GenerateXsd(buf);
            var typeQName = TypeOrReference.XsdQName;
            if (typeQName != null) buf.Write(" type='{0}'", typeQName);
            buf.WriteLine(">");
            if (typeQName == null) {
                buf.PushIndent();
                TypeOrReference.Type.GenerateXsd(buf);
                buf.PopIndent();
            }
            buf.WriteLine("</xs:attribute>");
        }
        protected virtual void GenerateXsdTagPart(TextBuffer buf) { }
    }
    public sealed class LocalAttribute : Attribute {
        internal LocalAttribute(AttributeSet parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal Qualification Qualification { get; private set; }
        internal bool IsQualified { get { return Qualification != null ? Qualification.Value : LogicalNamespaceAncestor.IsAttributeQualified; } }
        internal SimpleToken OptionalKeyword { get; private set; }
        internal override bool IsOptional { get { return OptionalKeyword != null; } }
        private XName _fullName;
        internal override XName FullName { get { return _fullName ?? (_fullName = IsQualified ? NamespaceAncestor.GetFullName(Name) : XName.Get(Name, "")); } }
        internal override EntityDeclarationKind Kind { get { return EntityDeclarationKind.Local; } }
        protected override bool InitializeAnnotations(string name, Node node) {
            switch (name) {
                case Qualification.NodeLabel:
                    if (Qualification != null) return false;
                    Qualification = new Qualification(node);
                    return true;
                case EX.OptionalNodeLabel:
                    if (OptionalKeyword != null) return false;
                    OptionalKeyword = new SimpleToken(node);
                    return true;
            }
            return base.InitializeAnnotations(name, node);
        }
        internal override AttributeMember MergeTo(AttributeMember otherMember) {
            if (otherMember == null) return this;
            var other = otherMember as LocalAttribute;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, GetType().Name, otherMember.GetType().Name);
            base.MergeTo(other);
            if (Qualification != null) {
                if (other.Qualification == null) other.Qualification = Qualification;
                else if (Qualification != other.Qualification)
                    CompilationContext.Error(Qualification, ErrorKind.AttributeQualificationNotEqualTo, Qualification, other.Qualification);
            }
            if (OptionalKeyword != null && other.OptionalKeyword == null) other.OptionalKeyword = OptionalKeyword;
            CompilationContext.ThrowIfHasErrors();
            return other;
        }
        protected override void GenerateXsdTagPart(TextBuffer buf) {
            if (Qualification != null) buf.Write(" form='{0}'", Qualification.Value.ToXsdQualification());
            if (IsOptional) buf.Write(" use='required'");
        }
    }
    public sealed class GlobalAttribute : Attribute {
        internal GlobalAttribute(Namespace parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private XName _fullName;
        internal override XName FullName { get { return _fullName ?? (_fullName = NamespaceAncestor.GetFullName(Name)); } }
        internal override EntityDeclarationKind Kind { get { return EntityDeclarationKind.Global; } }
        internal override bool IsMember { get { return false; } }
        protected override string GetThisDisplayName() { return "Attribute'" + Name + "'"; }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var nameId = NameId;
            if (MemberNameIdOpt != null) CompilationContext.Throw(MemberNameIdOpt, ErrorKind.MemberNameNotAllowedForGlobalAttribute);
        }
        internal AttributeInfo CreateInfo() { return CreateInfo(null); }
    }
    public sealed class AttributeReference : AttributeBase {
        internal AttributeReference(AttributeSet parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal const string NodeLabel = "AttributeReference";
        internal QualifiableNameEx QNameOpt { get; private set; }
        internal QualifiableNameEx QName {
            get {
                if (QNameOpt == null) CompilationContext.Throw(Keyword, ErrorKind.QualifiableNameRequired);
                return QNameOpt;
            }
        }
        protected override Identifier TryGetDefaultMemberNameId() { return QNameOpt != null ? QNameOpt.NameId : null; }
        private GlobalAttribute _value;
        internal GlobalAttribute Value { get { return _value ?? (_value = QName.Resolve<GlobalAttribute>(NameResolutionKind.Attribute)); } }
        internal override EntityDeclarationKind Kind { get { return EntityDeclarationKind.Reference; } }
        internal SimpleToken OptionalKeyword { get; private set; }
        internal override bool IsOptional { get { return OptionalKeyword != null; } }
        private DefaultOrFixedValue _defaultOrFixedValue;
        private bool _hasSetDefaultOrFixedValue;
        internal override DefaultOrFixedValue DefaultOrFixedValue {
            get {
                if (!_hasSetDefaultOrFixedValue) {
                    var dfValue = DefaultOrFixedValueOpt;
                    var globalAttDfValue = Value.DefaultOrFixedValueOpt;
                    if (dfValue == null) _defaultOrFixedValue = globalAttDfValue;
                    else {
                        if (globalAttDfValue != null && globalAttDfValue.IsFixed) {
                            if (dfValue.IsDefault) CompilationContext.Throw(dfValue.Keyword, ErrorKind.IfGlobalAttributeHasFixedValueAttributeReferenceMustHasFixedValueOrAbsent);
                            if (!dfValue.Literal.TextEquals(globalAttDfValue.Literal))
                                CompilationContext.Throw(dfValue.Literal, ErrorKind.AttributeReferenceFixedValueNotEqualToGlobalAttributeFixedValue, dfValue.Literal.Text, globalAttDfValue.Literal.Text);
                        }
                        _defaultOrFixedValue = dfValue;
                    }
                    _hasSetDefaultOrFixedValue = true;
                }
                return _defaultOrFixedValue;
            }
        }
        internal AttributeInfo ReferentialAttributeInfo {
            get { return AnalyzerAncestor.GetObjectInfo<AttributeInfo>(Value, NameResolutionKind.Attribute); }
        }
        protected override bool InitializeAnnotations(string name, Node node) {
            switch (name) {
                case EX.OptionalNodeLabel:
                    if (OptionalKeyword != null) return false;
                    OptionalKeyword = new SimpleToken(node);
                    return true;
            }
            return base.InitializeAnnotations(name, node);
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            QNameOpt = node.Member("QName").ToQualifiableNameExOpt(this);
        }
        internal override AttributeMember MergeTo(AttributeMember otherMember) {
            if (otherMember == null) return this;
            var other = otherMember as AttributeReference;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, GetType().Name, otherMember.GetType().Name);
            base.MergeTo(other);
            if (QNameOpt != null) other.QNameOpt = QNameOpt.MergeTo(other.QNameOpt);
            if (OptionalKeyword != null && other.OptionalKeyword == null) other.OptionalKeyword = OptionalKeyword;
            return other;
        }
        internal override void GenerateXsd(TextBuffer buf) {
            buf.Write("<xs:attribute ref='{0}:{1}'", LogicalNamespaceAncestor.GetXsdPrefix(Value), Value.Name);
            if (IsOptional) buf.Write(" use='required'");
            if (DefaultOrFixedValueOpt != null) DefaultOrFixedValueOpt.GenerateXsd(buf);
            buf.WriteLine(" />");
        }
    }
    public sealed class AttributesWildcard : AttributeMember {
        internal AttributesWildcard(AttributeSet parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal Wildcard WildcardOpt { get; private set; }
        internal Wildcard Wildcard {
            get {
                if (WildcardOpt == null) CompilationContext.Throw(Keyword, ErrorKind.WildcardRequired);
                return WildcardOpt;
            }
        }
        protected override Identifier TryGetDefaultMemberNameId() { return new Identifier("Wildcard", Keyword.SourceSpan); }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            WildcardOpt = node.Member("Wildcard").ToWildcardOpt(Keyword, NamespaceAncestor);
        }
        internal override AttributeMember MergeTo(AttributeMember otherMember) {
            if (otherMember == null) return this;
            var other = otherMember as AttributesWildcard;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, GetType().Name, otherMember.GetType().Name);
            if (WildcardOpt != null) {
                if (other.WildcardOpt == null) other.WildcardOpt = WildcardOpt;
                else if (WildcardOpt != other.WildcardOpt) CompilationContext.Throw(WildcardOpt, ErrorKind.WildcardNotEqualTo, WildcardOpt, other.WildcardOpt);
            }
            return other;
        }
        protected override AttributeInfo CreateInfoCore(AttributeSetInfo setInfo) {
            if (setInfo.TempWildcardList == null) setInfo.TempWildcardList = new List<WildcardInfo>();
            setInfo.TempWildcardList.Add(Wildcard.Info);
            return null;
        }
        internal override void GenerateXsd(TextBuffer buf) {
            buf.Write("<xs:anyAttribute");
            Wildcard.GenerateXsd(buf);
            buf.WriteLine(" />");
        }
    }
    //
    //
    //
    public abstract class Child : AnnotatableObject, IMemberObject, IDisplayableObject {
        protected Child() { }
        public SimpleToken Keyword { get; private set; }
        internal Occurrence Occurrence { get; private set; }
        internal ulong MinOccurs { get { return Occurrence != null ? Occurrence.MinValue : 1; } }
        internal ulong MaxOccurs { get { return Occurrence != null ? Occurrence.MaxValue : 1; } }
        //internal bool IsRequired { get { return MinOccurs > 0; } }
        internal bool IsList { get { return MaxOccurs > 1; } }
        internal virtual bool IsMember { get { return true; } }
        internal Identifier MemberNameIdOpt { get; private set; }
        private Identifier _memberNameId;
        public Identifier MemberNameId {
            get {
                if (_memberNameId == null) {
                    if (!IsMember) throw new InvalidOperationException();
                    _memberNameId = MemberNameIdOpt ?? TryGetDefaultMemberNameId();
                    if (_memberNameId == null) CompilationContext.Throw(Keyword, ErrorKind.NameOrMemberNameRequired);
                }
                return _memberNameId;
            }
        }
        protected abstract Identifier TryGetDefaultMemberNameId();
        private string _displayName;
        public string DisplayName { get { return _displayName ?? (_displayName = GetAncestor<IDisplayableObject>().DisplayName + "." + GetThisDisplayName()); } }
        protected virtual string GetThisDisplayName() { return "'" + MemberNameId.PlainValue + "'"; }//only this part, not including dot
        internal CSClass CSClass { get; private set; }
        internal CSClass GetCSClass() { return CSClass.Clone(); }
        internal CSClass ListCSClass { get; private set; }
        internal CSClass GetListCSClass() { return ListCSClass.Clone(); }
        protected override bool InitializeAnnotations(string name, Node node) {
            switch (name) {
                case Occurrence.NodeLabel:
                    if (Occurrence != null) return false;
                    Occurrence = new Occurrence(node);
                    return true;
                case EX.MemberNameNodeLabel:
                    if (MemberNameIdOpt != null) return false;
                    MemberNameIdOpt = new Identifier(node.Singleton);
                    return true;
            }
            return base.InitializeAnnotations(name, node);
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            Keyword = new SimpleToken(node.Member("Keyword"));
            CSClass = new X.CSClass(this, node.Member("CSClass"), Keyword);
            ListCSClass = new X.CSClass(this, node.Member("ListCSClass"), Keyword);
        }
        internal virtual Child MergeTo(Child otherChild) {
            if (otherChild == null) return this;
            if (Occurrence != null) {
                if (otherChild.Occurrence == null) otherChild.Occurrence = Occurrence;
                else if (otherChild.Occurrence != Occurrence)
                    CompilationContext.Error(Occurrence, ErrorKind.OccursNotEqualTo, Occurrence, otherChild.Occurrence);
            }
            CompilationContext.ThrowIfHasErrors();
            CSClass.MergeTo(otherChild.CSClass);
            ListCSClass.MergeTo(otherChild.ListCSClass);
            return otherChild;
        }
        IMemberObject IMemberObject.MergeTo(IMemberObject other) { return MergeTo((Child)other); }
        private bool _isProcessing;
        internal ChildInfo CreateInfo(ChildStructInfo parentInfo, ChildStructInfo restrictedParentInfo, bool isMixed) {
            if (_isProcessing) CompilationContext.Throw(Keyword, ErrorKind.CircularReferenceDetected);
            _isProcessing = true;
            var info = CreateInfoCore(parentInfo, restrictedParentInfo, isMixed);
            _isProcessing = false;
            return info;
        }
        protected virtual ChildInfo CreateInfoCore(ChildStructInfo parentInfo, ChildStructInfo restrictedParentInfo, bool isMixed) {
            if (parentInfo != null && parentInfo.ContainsUnordered) CompilationContext.Throw(Keyword, ErrorKind.UnorderedChildStructMustBeTheOnlyMemberOfChildren);
            return null;
        }
        protected bool GetRestrictedInfo(ChildStructInfo parentInfo, ChildStructInfo restrictedParentInfo, Identifier memberNameId, ulong minOccurs, ulong maxOccurs,
            ref int order, ref bool isList, ref ChildListInfo restrictedListInfo, ref ChildInfo restrictedItemInfo) {
            if (parentInfo.ContainsMemberNameId(memberNameId)) CompilationContext.Throw(Keyword, ErrorKind.DuplicateMemberName, memberNameId);
            if (restrictedParentInfo == null) {
                if (parentInfo.IsRoot && !parentInfo.AllMemberNameIdSet.Add(memberNameId)) CompilationContext.Throw(Keyword, ErrorKind.DuplicateMemberName, memberNameId);
                order = parentInfo.MemberList.Count;
                return false;
            }
            else {
                var restrictedInfo = restrictedParentInfo.TryGetMember(memberNameId, out order);
                if (restrictedInfo == null)
                    CompilationContext.Throw(Keyword, ErrorKind.RestrictedChildNotFound, memberNameId);
                restrictedListInfo = restrictedInfo as ChildListInfo;
                if (restrictedListInfo != null) {
                    isList = true;
                    restrictedItemInfo = restrictedListInfo.Item;
                }
                else restrictedItemInfo = restrictedInfo;
                if (minOccurs < restrictedInfo.MinOccurs)
                    CompilationContext.Error(Keyword, ErrorKind.RestrictingChildMinOccursNotEqualToOrGreaterThanRestrictedChild, minOccurs, restrictedInfo.MinOccurs);
                if (maxOccurs > restrictedInfo.MaxOccurs)
                    CompilationContext.Error(Keyword, ErrorKind.RestrictingChildMaxOccursNotEqualToOrLessThanRestrictedChild, maxOccurs, restrictedInfo.MaxOccurs);
                CompilationContext.ThrowIfHasErrors();
                return true;
            }
        }
        protected ChildInfo TryCreateListInfo(ChildStructInfo parentInfo, bool isList, ulong minOccurs, ulong maxOccurs, int order, Identifier memberNameId,
            ChildListInfo restrictedListInfo, bool isMixed, ChildInfo itemInfo) {
            ChildListInfo listInfo = null;
            if (isList)
                listInfo = new ChildListInfo(Keyword, this, memberNameId.Value + "_Class", GetListCSClass(), minOccurs, maxOccurs, order, memberNameId,
        restrictedListInfo, isMixed, itemInfo) { IsCSClassOverride = restrictedListInfo != null };
            else if (!ListCSClass.IsGenerated) CompilationContext.Throw(ListCSClass, ErrorKind.ListCodeNotAllowedForNonListChild);
            var info = (ChildInfo)listInfo ?? itemInfo;
            if (parentInfo != null) parentInfo.AddMember(info, order);
            return info;
        }
        internal abstract void GenerateXsd(TextBuffer buf);
        protected virtual void GenerateXsdTagPart(TextBuffer buf) {
            if (Occurrence != null) {
                if (Occurrence.MinValueValue != null) buf.Write(" minOccurs='{0}'", Occurrence.MinValue.ToInvariantString());
                if (Occurrence.MaxValueValue != null) {
                    var maxValue = Occurrence.MaxValue;
                    buf.Write(" maxOccurs='{0}'", maxValue == ulong.MaxValue ? "unbounded" : maxValue.ToInvariantString());
                }
            }
        }
    }
    public sealed class Occurrence : ValueBase, IEquatable<Occurrence> {
        internal Occurrence(Node node) {
            base.Initialize(node);
            var tokenNode = node.Member("Token");
            if (tokenNode.IsNotNull) {
                switch (tokenNode.MemberCSTokenKind()) {
                    case SyntaxKind.QuestionToken:
                        MinValue = 0; MaxValue = 1;
                        break;
                    case SyntaxKind.AsteriskToken:
                        MinValue = 0; MaxValue = ulong.MaxValue;
                        break;
                    case SyntaxKind.PlusToken:
                        MinValue = 1; MaxValue = ulong.MaxValue;
                        break;
                    default: throw new InvalidOperationException();
                }
                MinValueValue = new UnsignedIntegerValue<ulong>(MinValue, tokenNode.SourceSpan);
                MaxValueValue = new UnsignedIntegerValue<ulong>(MaxValue, tokenNode.SourceSpan);
            }
            else {
                MinValueValue = new UnsignedIntegerValue<ulong>(node.Member("MinValue"), ErrorKind.InvalidInteger);
                MinValue = MinValueValue.Value;
                var maxValueNode = node.Member("MaxValue");
                if (maxValueNode.IsNull) MaxValueValue = new UnsignedIntegerValue<ulong>(ulong.MaxValue, node.Member("DotDotToken").SourceSpan);
                else MaxValueValue = new UnsignedIntegerValue<ulong>(maxValueNode, ErrorKind.InvalidInteger);
                MaxValue = MaxValueValue.Value;
                if (MaxValue == 0) CompilationContext.Throw(MaxValueValue, ErrorKind.MaxOccursMustGreaterThanZero);
                if (MinValue > MaxValue) CompilationContext.Throw(MaxValueValue, ErrorKind.MaxOccursMustGreaterThanOrEqualToMinOccurs, MaxValue, MinValue);
            }
        }
        internal const string NodeLabel = "Occurrence";
        internal readonly UnsignedIntegerValue<ulong> MinValueValue;
        internal readonly UnsignedIntegerValue<ulong> MaxValueValue;
        internal readonly ulong MinValue;
        internal readonly ulong MaxValue;
        public bool Equals(Occurrence other) {
            if (object.ReferenceEquals(this, other)) return true;
            if (object.ReferenceEquals(other, null)) return false;
            return other.MinValue == this.MinValue && other.MaxValue == this.MaxValue;
        }
        public override bool Equals(object obj) { return Equals(obj as Occurrence); }
        public override int GetHashCode() { return Extensions.CombineHash(MinValue.GetHashCode(), MaxValue.GetHashCode()); }
        public static bool operator ==(Occurrence left, Occurrence right) {
            if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
            return left.Equals(right);
        }
        public static bool operator !=(Occurrence left, Occurrence right) { return !(left == right); }
        private string _string;
        public override string ToString() {
            return _string ?? (_string = MinValue.ToInvariantString() + ".." + (MaxValue == ulong.MaxValue ? "*" : MaxValue.ToInvariantString()));
        }
    }
    public abstract class ChildStructBase : Child {
        protected ChildStructBase() { }
        internal abstract ChildContainerKind Kind { get; }
        internal abstract IReadOnlyList<Child> MemberList { get; }
        protected virtual string GetErrorMessageSuffix() { return null; }
        protected override sealed ChildInfo CreateInfoCore(ChildStructInfo parentInfo, ChildStructInfo restrictedParentInfo, bool isMixed) {
            base.CreateInfoCore(parentInfo, restrictedParentInfo, isMixed);
            var kind = Kind;
            if (kind == ChildContainerKind.Unordered) {
                if (parentInfo.MemberList.Any(i => i != null))
                    CompilationContext.Throw(Keyword, ErrorKind.UnorderedChildStructMustBeTheOnlyMemberOfChildren);
                parentInfo.ContainsUnordered = true;
            }
            var memberNameId = MemberNameId;
            var minOccurs = MinOccurs;
            var maxOccurs = MaxOccurs;
            var isList = maxOccurs > 1;
            var order = -1;
            ChildListInfo restrictedListInfo = null;
            ChildStructInfo restrictedStructInfo = null;
            ChildInfo restrictedItemInfo = null;
            if (GetRestrictedInfo(parentInfo, restrictedParentInfo, memberNameId, minOccurs, maxOccurs, ref order, ref isList, ref restrictedListInfo, ref restrictedItemInfo)) {
                restrictedStructInfo = restrictedItemInfo as ChildStructInfo;
                if (restrictedStructInfo == null)
                    CompilationContext.Throw(Keyword, ErrorKind.RestrictedChildIsNotStruct);
                if (kind != restrictedStructInfo.Kind)
                    CompilationContext.Throw(Keyword, ErrorKind.RestrictingChildStructKindNotEqualToRestricted, kind, restrictedStructInfo.Kind);
            }
            var structInfo = new ChildStructInfo(Keyword, this, isList ? "ItemClass" : memberNameId.Value + "_Class", GetCSClass(), minOccurs, maxOccurs, isList,
                isList ? -1 : order, isList ? null : memberNameId, restrictedStructInfo, kind, isMixed, null) { IsCSClassOverride = restrictedStructInfo != null };
            Error.PushMessageSuffix(GetErrorMessageSuffix());
            try {
                var isChoice = kind == ChildContainerKind.Choice;
                foreach (var member in MemberList) {
                    member.CreateInfo(structInfo, restrictedStructInfo, isMixed).IsChoiceMember = isChoice;
                }
            }
            finally { Error.PopMessageSuffix(); }
            CreateInfoPostCheck(structInfo, restrictedStructInfo);
            return TryCreateListInfo(parentInfo, isList, minOccurs, maxOccurs, order, memberNameId, restrictedListInfo, isMixed, structInfo);
        }
        internal void CreateInfoPostCheck(ChildStructInfo structInfo, ChildStructInfo restrictedStructInfo) {
            if (restrictedStructInfo != null) {
                if (structInfo.Kind == ChildContainerKind.Choice) {
                    if (!restrictedStructInfo.IsEffectiveOptional && !structInfo.NonNullMembers.Any())
                        CompilationContext.Throw(Keyword, ErrorKind.RestrictingChoiceMustHasMembersIfRestrictedChoiceNotEffectiveOptional);
                }
                else {
                    foreach (var restrictedMemberInfo in restrictedStructInfo.MemberList) {
                        if (restrictedMemberInfo != null) {
                            if (!restrictedMemberInfo.IsEffectiveOptional && !structInfo.ContainsMemberNameId(restrictedMemberInfo.MemberNameId))
                                CompilationContext.Error(Keyword, ErrorKind.RequiredChildNotRestricting, restrictedMemberInfo.MemberNameId);
                        }
                    }
                    CompilationContext.ThrowIfHasErrors();
                }
                while (structInfo.MemberList.Count < restrictedStructInfo.MemberList.Count) structInfo.MemberList.Add(null);
            }
        }
    }
    public abstract class ChildStruct : ChildStructBase {
        protected ChildStruct() { }
        internal const string NodeLabel = "ChildStruct";
        private ChildContainerKind _kind;
        internal override sealed ChildContainerKind Kind { get { return _kind; } }
        protected override sealed Identifier TryGetDefaultMemberNameId() { return new Identifier(_kind.ToString(), Keyword.SourceSpan); }
        private MemberObjectListEx<Child> _memberListEx;
        internal MemberObjectListEx<Child> MemberListEx { get { return _memberListEx ?? (_memberListEx = new MemberObjectListEx<Child>()); } }
        internal override sealed IReadOnlyList<Child> MemberList { get { return MemberListEx.GetResult(_kind == ChildContainerKind.Seq); } }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var isUnordered = false;
            switch (node.Member("Keyword").MemberXTokenKind()) {
                case XTokenKind.ChildrenKeyword:
                case XTokenKind.SeqKeyword: _kind = ChildContainerKind.Seq; break;
                case XTokenKind.ChoiceKeyword: _kind = ChildContainerKind.Choice; break;
                case XTokenKind.UnorderedKeyword: {
                        if (base.Parent is CommonChildStruct) CompilationContext.Throw(Keyword, ErrorKind.UnorderedChildStructMustBeDirectMemberOfChildren);
                        if (IsList) CompilationContext.Throw(Keyword, ErrorKind.UnorderedChildStructOrMemberMaxOccursMustBeOne);
                        _kind = ChildContainerKind.Unordered;
                        isUnordered = true;
                    }
                    break;
                default: throw new InvalidOperationException();
            }
            foreach (var memberNode in node.Member("Members").Items) {
                Child member;
                var memberNodeLabel = memberNode.Label;
                if (memberNodeLabel == Element.NodeLabel) member = new LocalElement(this, memberNode);
                else if (memberNodeLabel == ElementReference.NodeLabel) member = new ElementReference(this, memberNode);
                else {
                    if (isUnordered) CompilationContext.Throw(memberNode, ErrorKind.UnorderedChildStructMemberMustBeElementOrElementReference);
                    if (memberNodeLabel == ElementWildcard.NodeLabel) member = new ElementWildcard(this, memberNode);
                    else if (memberNodeLabel == ChildStruct.NodeLabel) member = new CommonChildStruct(this, memberNode);
                    else if (memberNodeLabel == ChildStructReference.NodeLabel) member = new ChildStructReference(this, memberNode);
                    else throw new InvalidOperationException();
                }
                if (isUnordered && member.IsList) CompilationContext.Throw(member.Keyword, ErrorKind.UnorderedChildStructOrMemberMaxOccursMustBeOne);
                MemberListEx.AddMember(member);
            }
        }
        internal override Child MergeTo(Child otherChild) {
            if (otherChild == null) return this;
            var other = otherChild as ChildStruct;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, GetType().Name, otherChild.GetType().Name);
            base.MergeTo(other);
            if (_kind != other._kind) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, _kind, other._kind);
            MemberListEx.MergeTo(other.MemberListEx);
            return other;
        }
        internal override void GenerateXsd(TextBuffer buf) {
            //if (IsGlobal) {
            //    buf.WriteLine("<xs:group name='{0}'>", Name);
            //    buf.PushIndent();
            //}
            //var tagName = _kind.ToXsd();
            //buf.Write("<xs:{0}" + tagName);
            //GenerateXsdTagPart(buf);
            //buf.WriteLine(">");
            //buf.PushIndent();
            //foreach (var member in MemberList) member.GenerateXsd(buf);
            //buf.PopIndent();
            //buf.WriteLine("</xs:{0}>", tagName);
            //if (IsGlobal) {
            //    buf.PopIndent();
            //    buf.WriteLine("</xs:group>");
            //}
        }
    }
    public sealed class CommonChildStruct : ChildStruct {
        internal CommonChildStruct(Object parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal Identifier NameId { get; private set; }//opt
        internal string Name { get { return NameId == null ? null : NameId.PlainValue; } }
        internal bool IsGlobal { get { return NameId != null; } }
        internal override bool IsMember { get { return !IsGlobal; } }
        protected override string GetThisDisplayName() {
            if (IsGlobal) return "ChildStruct'" + Name + "'";
            return base.GetThisDisplayName();
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            NameId = node.Member("Name").ToIdentifierOpt();
            if (base.Parent is Namespace) {
                if (NameId == null) CompilationContext.Error(Keyword, ErrorKind.NameRequiredForGlobalChildStruct);
                if (MemberNameIdOpt != null) CompilationContext.Error(MemberNameIdOpt, ErrorKind.MemberNameNotAllowedForGlobalChildStruct);
                if (Occurrence != null) CompilationContext.Error(Occurrence, ErrorKind.OccursNotAllowedForGlobalChildStruct);
                if (!CSClass.IsGenerated) CompilationContext.Error(CSClass, ErrorKind.CodeNotAllowedForGlobalChildStruct);
                if (!ListCSClass.IsGenerated) CompilationContext.Error(ListCSClass, ErrorKind.CodeNotAllowedForGlobalChildStruct);
                CompilationContext.ThrowIfHasErrors();
            }
            else if (NameId != null) CompilationContext.Throw(NameId, ErrorKind.NameNotAllowedForLocalChildStruct);
        }
    }
    public sealed class RootChildStruct : ChildStruct {
        internal RootChildStruct(UnresolvedType parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal CSClass RootCSClass { get; private set; }
        internal CSClass GetRootCSClass() { return RootCSClass.Clone(); }
        internal override bool IsMember { get { return false; } }
        protected override string GetThisDisplayName() { return "Children"; }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            RootCSClass = new CSClass(this, node.Member("RootCSClass"), Keyword);
        }
        internal override Child MergeTo(Child otherChild) {
            if (otherChild == null) return this;
            var other = otherChild as RootChildStruct;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, GetType().Name, otherChild.GetType().Name);
            base.MergeTo(other);
            RootCSClass.MergeTo(other.RootCSClass);
            return other;
        }
        internal ChildStructInfo CreateInfo(ChildStructInfo baseStructInfo, bool isExtension, bool isMixed) {
            var structInfo = new ChildStructInfo(Keyword, this, "ComplexChildClass", GetRootCSClass(), 1, 1, false, -1, null, null, ChildContainerKind.Seq, isMixed, null) {
                IsRoot = true,
                BaseChildStruct = baseStructInfo,
                IsExtension = isExtension,
                ContainsUnordered = isExtension && baseStructInfo != null ? baseStructInfo.ContainsUnordered : false
            };
            if (baseStructInfo != null) {
                structInfo.AllMemberNameIdSet.AddRange(baseStructInfo.AllMemberNameIdSet);
                if (isExtension) {
                    structInfo.MemberList.AddRange(baseStructInfo.MemberList);
                    structInfo.ThisStartIndex = structInfo.MemberList.Count;
                }
            }
            else if (!isExtension) throw new InvalidOperationException();
            var restrictedStructInfo = isExtension ? null : baseStructInfo;
            foreach (var member in MemberList) member.CreateInfo(structInfo, restrictedStructInfo, isMixed);
            CreateInfoPostCheck(structInfo, restrictedStructInfo);
            if (isExtension && !structInfo.ContainsUnordered) structInfo.CheckUpa();
            return structInfo;
        }
    }
    public sealed class ChildStructReference : ChildStructBase {
        internal ChildStructReference(ChildStruct parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal const string NodeLabel = "ChildStructReference";
        internal QualifiableNameEx QNameOpt { get; private set; }
        internal QualifiableNameEx QName {
            get {
                if (QNameOpt == null) CompilationContext.Throw(Keyword, ErrorKind.QualifiableNameRequired);
                return QNameOpt;
            }
        }
        private CommonChildStruct _value;
        internal CommonChildStruct Value { get { return _value ?? (_value = QName.Resolve<CommonChildStruct>(NameResolutionKind.ChildStruct)); } }
        internal override ChildContainerKind Kind { get { return Value.Kind; } }
        internal override IReadOnlyList<Child> MemberList { get { return Value.MemberList; } }
        protected override Identifier TryGetDefaultMemberNameId() { return QNameOpt != null ? QNameOpt.NameId : null; }
        private string _errorMessageSuffix;
        protected override string GetErrorMessageSuffix() { return _errorMessageSuffix ?? (_errorMessageSuffix = " ->at " + DisplayName); }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            QNameOpt = node.Member("QName").ToQualifiableNameExOpt(this);
        }
        internal override Child MergeTo(Child otherChild) {
            if (otherChild == null) return this;
            var other = otherChild as ChildStructReference;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, GetType().Name, otherChild.GetType().Name);
            base.MergeTo(other);
            if (QNameOpt != null) other.QNameOpt = QNameOpt.MergeTo(other.QNameOpt);
            return other;
        }
        internal override void GenerateXsd(TextBuffer buf) {
            buf.Write("<xs:group ref='{0}:{1}'", LogicalNamespaceAncestor.GetXsdPrefix(Value), Value.Name);
            GenerateXsdTagPart(buf);
            buf.WriteLine(" />");
        }
    }
    public abstract class ElementBase : Child {
        protected ElementBase() { }
        internal abstract EntityDeclarationKind Kind { get; }
        protected override sealed ChildInfo CreateInfoCore(ChildStructInfo parentInfo, ChildStructInfo restrictedParentInfo, bool isMixed) {
            base.CreateInfoCore(parentInfo, restrictedParentInfo, isMixed);
            var kind = Kind;
            var isGlobal = kind == EntityDeclarationKind.Global;
            var isRef = kind == EntityDeclarationKind.Reference;
            var memberNameId = isGlobal ? null : MemberNameId;
            var minOccurs = MinOccurs;
            var maxOccurs = MaxOccurs;
            var isList = maxOccurs > 1;
            var order = -1;
            var element = this as Element;
            var globalElement = this as GlobalElement;
            var elementRef = this as ElementReference;
            ElementInfo referentialElementInfo = isRef ? elementRef.ReferentialElementInfo : null;
            var fullName = isRef ? referentialElementInfo.Name : element.FullName;
            ChildListInfo restrictedListInfo = null;
            ElementBaseInfo restrictedElementBaseInfo = null;
            ElementInfo restrictedElementInfo = null;
            var isUnorderedMember = false;
            if (parentInfo != null) {//for global element, parentInfo is null
                if (parentInfo.Kind == ChildContainerKind.Unordered) {
                    isUnorderedMember = true;
                    if (restrictedParentInfo == null) {
                        foreach (ElementInfo ei in parentInfo.NonNullMembers)
                            if (ei.Name == fullName) CompilationContext.Throw(Keyword, ErrorKind.DuplicateElementFullNameInUnorderedChildStruct, fullName);
                    }
                }
                ChildInfo restrictedItemInfo = null;
                if (GetRestrictedInfo(parentInfo, restrictedParentInfo, memberNameId, minOccurs, maxOccurs, ref order, ref isList,
                    ref restrictedListInfo, ref restrictedItemInfo)) {
                    restrictedElementBaseInfo = restrictedItemInfo as ElementBaseInfo;
                    if (restrictedElementBaseInfo == null)
                        CompilationContext.Throw(Keyword, ErrorKind.RestrictedChildIsNotElementOrElementWildcard);
                    restrictedElementInfo = restrictedElementBaseInfo as ElementInfo;
                    if (restrictedElementInfo != null) {
                        if (!restrictedElementInfo.IsMatch(fullName))
                            CompilationContext.Throw(Keyword, ErrorKind.RestrictingElementFullNameNotMatchWithRestrictedElement, fullName);
                    }
                    else {
                        var restrictedElementWildcardInfo = (ElementWildcardInfo)restrictedElementBaseInfo;
                        if (!restrictedElementWildcardInfo.IsMatch(fullName.Namespace))
                            CompilationContext.Throw(Keyword, ErrorKind.RestrictingElementNamespaceNotMatchWithRestrictedElementWildcard, fullName.Namespace, restrictedElementWildcardInfo.Wildcard);
                    }
                }
            }
            var typeInfo = isRef ? referentialElementInfo.Type : element.CreateTypeInfo();
            if (restrictedElementInfo != null) {
                if (!typeInfo.IsEqualToOrRestrictedDeriveFrom(restrictedElementInfo.Type))
                    CompilationContext.Throw(Keyword, ErrorKind.RestrictingElementTypeNotEqualToOrRestrictedDeriveFromRestrictedElementType);
            }
            var restrictedDfValueInfo = restrictedElementInfo == null ? null : restrictedElementInfo.DefaultOrFixedValue;
            DefaultOrFixedValueInfo dfValueInfo = null;
            if (isRef) dfValueInfo = referentialElementInfo.DefaultOrFixedValue;
            else {
                var dfValue = element.DefaultOrFixedValue;
                if (dfValue != null) {
                    var simpleTypeInfo = typeInfo as ISimpleTypeInfo;
                    if (simpleTypeInfo == null) {
                        var complexTypeInfo = typeInfo as ComplexTypeInfo;
                        if (complexTypeInfo == null) CompilationContext.Throw(Keyword, ErrorKind.CannotSetDefaultOrFixedValueIfElementTypeIsSystemRootType);
                        simpleTypeInfo = complexTypeInfo.SimpleChild as ISimpleTypeInfo;
                        if (simpleTypeInfo == null) {
                            if (!complexTypeInfo.IsMixed) CompilationContext.Throw(Keyword, ErrorKind.CannotSetDefaultOrFixedValueIfElementComplexTypeIsNotMixed);
                            if (complexTypeInfo.ComplexChild == null) CompilationContext.Throw(Keyword, ErrorKind.CannotSetDefaultOrFixedValueIfElementTypeHasNoChildStruct);
                            if (!complexTypeInfo.ComplexChild.IsEffectiveOptional) CompilationContext.Throw(Keyword, ErrorKind.CannotSetDefaultOrFixedValueIfElementTypeChildStructIsNotEffectiveOptional);
                        }
                    }
                    dfValueInfo = dfValue.CreateInfo(simpleTypeInfo);
                }
            }
            if (dfValueInfo != null) {
                if (restrictedDfValueInfo != null && restrictedDfValueInfo.IsFixed) {
                    if (dfValueInfo.IsDefault) CompilationContext.Throw(Keyword, ErrorKind.RestrictedElementAndRestrictingElementMustBothHasFixedValueOrNeither);
                    if (!MX.SimpleType.ValueEquals(dfValueInfo.Value, restrictedDfValueInfo.Value))
                        CompilationContext.Throw(Keyword, ErrorKind.RestrictedElementFixedValueNotEqualToRestrictingElementFixedValue, dfValueInfo.ValueText, restrictedDfValueInfo.ValueText);
                }
            }
            else if (restrictedDfValueInfo != null && restrictedDfValueInfo.IsFixed)
                CompilationContext.Throw(Keyword, ErrorKind.RestrictedElementAndRestrictingElementMustBothHasFixedValueOrNeither);
            //
            ElementInfo elementInfo;
            var csName = isList ? "ItemClass" : (isGlobal ? fullName.LocalName + "_ElementClass" : memberNameId.Value + "_Class");
            if (isRef) {
                elementInfo = new ElementInfo(Keyword, this, csName, GetCSClass(), minOccurs, maxOccurs, isList, isList ? -1 : order, isList ? null : memberNameId,
                    restrictedElementBaseInfo, referentialElementInfo);
            }
            else {
                ElementInfo substitutedElementInfo = null;
                if (isGlobal) {
                    substitutedElementInfo = globalElement.CreateSubstitutedElementInfo();
                    if (substitutedElementInfo != null) {
                        if (!typeInfo.IsEqualToOrDeriveFrom(substitutedElementInfo.Type))
                            CompilationContext.Throw(Keyword, ErrorKind.SubstitutingElementTypeNotEqualToOrDeriveFromSubstitutedElementType);
                        if (typeInfo != substitutedElementInfo.Type) {
                            if (substitutedElementInfo.DerivationProhibition.IsSet(X.DerivationProhibition.Extension) && typeInfo.DerivationMethod == DerivationMethod.Extension)
                                CompilationContext.Throw(Keyword, ErrorKind.ExtensionDerivationProhibited);
                            if (substitutedElementInfo.DerivationProhibition.IsSet(X.DerivationProhibition.Restriction) && typeInfo.DerivationMethod == DerivationMethod.Restriction)
                                CompilationContext.Throw(Keyword, ErrorKind.RestrictionDerivationProhibited);
                        }
                    }
                }
                var identityConstraintInfoList = new List<IdentityConstraintInfo>();
                var identityConstraintList = element.IdentityConstraintList;
                foreach (var ic in identityConstraintList)
                    if (!ic.IsKeyRef)
                        identityConstraintInfoList.Add(ic.Info);
                foreach (var ids in identityConstraintList)
                    if (ids.IsKeyRef)
                        identityConstraintInfoList.Add(ids.Info);
                elementInfo = new ElementInfo(Keyword, this, csName, GetCSClass(), minOccurs, maxOccurs, isList, isList ? -1 : order, isList ? null : memberNameId,
                    restrictedElementBaseInfo, kind, fullName, element.IsAbstract, element.IsNullable, element.DerivationProhibition, element.InstanceProhibition,
                    dfValueInfo, typeInfo, substitutedElementInfo, globalElement, identityConstraintInfoList);
            }
            elementInfo.IsCSClassOverride = restrictedElementBaseInfo != null;
            elementInfo.IsUnorderedMember = isUnorderedMember;
            return TryCreateListInfo(parentInfo, isList, minOccurs, maxOccurs, order, memberNameId, restrictedListInfo, isMixed, elementInfo);
        }
    }
    public abstract class Element : ElementBase {
        protected Element() { }
        internal const string NodeLabel = "Element";
        internal Identifier NameIdOpt { get; private set; }
        internal Identifier NameId {
            get {
                if (NameIdOpt == null) CompilationContext.Throw(Keyword, ErrorKind.NameRequired);
                return NameIdOpt;
            }
        }
        internal string Name { get { return NameId.PlainValue; } }
        protected override sealed Identifier TryGetDefaultMemberNameId() { return NameIdOpt; }
        internal SimpleToken NullableKeyword { get; private set; }
        internal bool IsNullable { get { return NullableKeyword != null; } }
        internal InstanceProhibitionValue InstanceProhibitionValue { get; private set; }
        internal InstanceProhibition InstanceProhibition { get { return InstanceProhibitionValue != null ? InstanceProhibitionValue.Value : LogicalNamespaceAncestor.InstanceProhibition; } }
        internal DefaultOrFixedValue DefaultOrFixedValue { get; private set; }
        internal TypeOrReference TypeOrReferenceOpt { get; private set; }
        internal TypeOrReference TypeOrReference {
            get {
                if (TypeOrReferenceOpt == null) CompilationContext.Throw(Keyword, ErrorKind.TypeRequired);
                return TypeOrReferenceOpt;
            }
        }
        private List<IdentityConstraint> _identityConstraintList;
        internal List<IdentityConstraint> IdentityConstraintList { get { return _identityConstraintList ?? (_identityConstraintList = new List<IdentityConstraint>()); } }
        internal abstract XName FullName { get; }
        internal virtual bool IsAbstract { get { return false; } }
        internal virtual DerivationProhibition DerivationProhibition { get { return DerivationProhibition.None; } }
        internal TypeInfo CreateTypeInfo() { return TypeOrReference.CreateInfo("TypeClass"); }
        protected override bool InitializeAnnotations(string name, Node node) {
            switch (name) {
                case EX.NullableNodeLabel:
                    if (NullableKeyword != null) return false;
                    NullableKeyword = new SimpleToken(node);
                    return true;
                case InstanceProhibitionValue.NodeLabel:
                    if (InstanceProhibitionValue != null) return false;
                    InstanceProhibitionValue = new InstanceProhibitionValue(node.Singleton);
                    return true;
                case DefaultOrFixedValue.NodeLabel:
                    if (DefaultOrFixedValue != null) return false;
                    DefaultOrFixedValue = new DefaultOrFixedValue(NamespaceAncestor, node);
                    return true;
            }
            return base.InitializeAnnotations(name, node);
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            NameIdOpt = node.Member("Name").ToIdentifierOpt();
            TypeOrReferenceOpt = node.Member("TypeOrReference").ToTypeOrReferenceOpt(this);
            foreach (var identityConstraintNode in node.Member("IdentityConstraints").Items)
                IdentityConstraintList.Add(new IdentityConstraint(this, identityConstraintNode));
        }
        internal override Child MergeTo(Child otherChild) {
            if (otherChild == null) return this;
            var other = otherChild as Element;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, GetType().Name, otherChild.GetType().Name);
            base.MergeTo(other);
            if (NameIdOpt != null) {
                if (other.NameIdOpt == null) other.NameIdOpt = NameIdOpt;
                else if (NameIdOpt != other.NameIdOpt) CompilationContext.Error(NameIdOpt, ErrorKind.NameNotEqualTo, NameIdOpt, other.NameIdOpt);
            }
            if (NullableKeyword != null && other.NullableKeyword == null) other.NullableKeyword = NullableKeyword;
            if (InstanceProhibitionValue != null) {
                if (other.InstanceProhibitionValue == null) other.InstanceProhibitionValue = InstanceProhibitionValue;
                else if (InstanceProhibitionValue != other.InstanceProhibitionValue)
                    CompilationContext.Error(InstanceProhibitionValue, ErrorKind.InstanceProhibitionNotEqualTo, InstanceProhibitionValue, other.InstanceProhibitionValue);
            }
            if (DefaultOrFixedValue != null) {
                if (other.DefaultOrFixedValue == null) other.DefaultOrFixedValue = DefaultOrFixedValue;
                else if (!DefaultOrFixedValue.TextEquals(other.DefaultOrFixedValue))
                    CompilationContext.Throw(DefaultOrFixedValue, ErrorKind.DefaultOrFixedValueNotEqaulTo, DefaultOrFixedValue, other.DefaultOrFixedValue);
            }
            if (TypeOrReferenceOpt != null) other.TypeOrReferenceOpt = TypeOrReferenceOpt.MergeTo(other.TypeOrReferenceOpt);
            other.IdentityConstraintList.AddRange(IdentityConstraintList);
            CompilationContext.ThrowIfHasErrors();
            return other;
        }
        internal override sealed void GenerateXsd(TextBuffer buf) {
            buf.Write("<xs:element name='{0}'", Name);
            GenerateXsdTagPart(buf);
            if (IsNullable) buf.Write(" nillable='true'");
            if (InstanceProhibitionValue != null) buf.Write(" block='{0}'", InstanceProhibitionValue.Value.ToXsd());
            if (DefaultOrFixedValue != null) DefaultOrFixedValue.GenerateXsd(buf);
            var typeQName = TypeOrReference.XsdQName;
            if (typeQName != null) buf.Write(" type='{0}'", typeQName);
            buf.WriteLine(">");
            if (typeQName == null) {
                buf.PushIndent();
                TypeOrReference.Type.GenerateXsd(buf);
                buf.PopIndent();
            }
            buf.WriteLine("</xs:element>");
        }
    }
    public sealed class LocalElement : Element {
        internal LocalElement(ChildStruct parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal Qualification Qualification { get; private set; }
        internal bool IsQualified { get { return Qualification != null ? Qualification.Value : LogicalNamespaceAncestor.IsElementQualified; } }
        private XName _fullName;
        internal override XName FullName { get { return _fullName ?? (_fullName = IsQualified ? NamespaceAncestor.GetFullName(Name) : XName.Get(Name, "")); } }
        internal override EntityDeclarationKind Kind { get { return EntityDeclarationKind.Local; } }
        protected override bool InitializeAnnotations(string name, Node node) {
            switch (name) {
                case Qualification.NodeLabel:
                    if (Qualification != null) return false;
                    Qualification = new Qualification(node);
                    return true;
            }
            return base.InitializeAnnotations(name, node);
        }
        internal override Child MergeTo(Child otherChild) {
            if (otherChild == null) return this;
            var other = otherChild as LocalElement;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, GetType().Name, otherChild.GetType().Name);
            base.MergeTo(other);
            if (Qualification != null) {
                if (other.Qualification == null) other.Qualification = Qualification;
                else if (Qualification != other.Qualification)
                    CompilationContext.Error(Qualification, ErrorKind.ElementQualificationNotEqualTo, Qualification, other.Qualification);
            }
            CompilationContext.ThrowIfHasErrors();
            return other;
        }
        protected override void GenerateXsdTagPart(TextBuffer buf) {
            base.GenerateXsdTagPart(buf);
            if (Qualification != null) buf.Write(" form='{0}'", Qualification.Value.ToXsdQualification());
        }
    }
    public sealed class GlobalElement : Element {
        internal GlobalElement(Namespace parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal override bool IsMember { get { return false; } }
        protected override string GetThisDisplayName() { return "Element'" + Name + "'"; }
        internal SimpleToken AbstractKeyword { get; private set; }
        internal override bool IsAbstract { get { return AbstractKeyword != null; } }
        internal DerivationProhibitionValue DerivationProhibitionValue { get; private set; }
        internal override DerivationProhibition DerivationProhibition { get { return DerivationProhibitionValue != null ? DerivationProhibitionValue.Value : NamespaceAncestor.DerivationProhibition; } }
        internal QualifiableNameEx SubstitutedElementQName { get; private set; }//opt
        internal GlobalElement SubstitutedElement { get; private set; }//opt
        private List<GlobalElement> _directSubstitutingElementList;
        internal List<GlobalElement> DirectSubstitutingElementList { get { return _directSubstitutingElementList ?? (_directSubstitutingElementList = new List<GlobalElement>()); } }
        internal override EntityDeclarationKind Kind { get { return EntityDeclarationKind.Global; } }
        private XName _fullName;
        internal override XName FullName { get { return _fullName ?? (_fullName = NamespaceAncestor.GetFullName(Name)); } }
        internal GlobalElement TryGet(XName fullName) {
            if (FullName == fullName) return this;
            foreach (var i in DirectSubstitutingElementList) {
                var obj = i.TryGet(fullName);
                if (obj != null) return obj;
            }
            return null;
        }
        internal IEnumerable<XName> SelfAndSubstitutingElementNames {
            get {
                yield return FullName;
                foreach (var element in DirectSubstitutingElementList)
                    foreach (var i in element.SelfAndSubstitutingElementNames)
                        yield return i;
            }
        }
        internal ElementInfo CreateSubstitutedElementInfo() {
            if (SubstitutedElement != null)
                return AnalyzerAncestor.GetObjectInfo<ElementInfo>(SubstitutedElement, NameResolutionKind.Element);
            return null;
        }
        internal ElementInfo CreateInfo() { return (ElementInfo)CreateInfo(null, null, false); }
        protected override bool InitializeAnnotations(string name, Node node) {
            switch (name) {
                case EX.AbstractNodeLabel:
                    if (AbstractKeyword != null) return false;
                    AbstractKeyword = new SimpleToken(node);
                    return true;
                case DerivationProhibitionValue.NodeLabel:
                    if (DerivationProhibitionValue != null) return false;
                    DerivationProhibitionValue = new DerivationProhibitionValue(node.Singleton);
                    return true;
                case EX.SubstitutionNodeLabel:
                    if (SubstitutedElementQName != null) return false;
                    SubstitutedElementQName = new QualifiableNameEx(this, node.Singleton);
                    return true;
            }
            return base.InitializeAnnotations(name, node);
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var nameId = NameId;
            if (Occurrence != null) CompilationContext.Error(Occurrence, ErrorKind.OccursNotAllowedForGlobalElement);
            if (MemberNameIdOpt != null) CompilationContext.Error(MemberNameIdOpt, ErrorKind.MemberNameNotAllowedForGlobalAttribute);
            CompilationContext.ThrowIfHasErrors();
        }
        internal override Child MergeTo(Child otherChild) {
            if (otherChild == null) return this;
            var other = otherChild as GlobalElement;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, GetType().Name, otherChild.GetType().Name);
            base.MergeTo(other);
            if (AbstractKeyword != null && other.AbstractKeyword == null) other.AbstractKeyword = AbstractKeyword;
            if (DerivationProhibitionValue != null) {
                if (other.DerivationProhibitionValue == null) other.DerivationProhibitionValue = DerivationProhibitionValue;
                else if (DerivationProhibitionValue != other.DerivationProhibitionValue)
                    CompilationContext.Error(DerivationProhibitionValue, ErrorKind.DerivationProhibitionNotEqualTo, DerivationProhibitionValue, other.DerivationProhibitionValue);
            }
            if (SubstitutedElementQName != null) other.SubstitutedElementQName = SubstitutedElementQName.MergeTo(other.SubstitutedElementQName);
            CompilationContext.ThrowIfHasErrors();
            return other;
        }
        internal void ResolveSubstitution() {
            if (SubstitutedElementQName != null) {
                if (SubstitutedElement == null) {
                    SubstitutedElement = SubstitutedElementQName.Resolve<GlobalElement>(NameResolutionKind.Element);
                    SubstitutedElement.ResolveSubstitution();
                    for (var element = SubstitutedElement; element != null; element = element.SubstitutedElement)
                        if (object.ReferenceEquals(element, this))
                            CompilationContext.Throw(SubstitutedElementQName, ErrorKind.CircularReferenceDetected);
                    SubstitutedElement.DirectSubstitutingElementList.Add(this);
                }
            }
        }
        protected override void GenerateXsdTagPart(TextBuffer buf) {
            base.GenerateXsdTagPart(buf);
            if (IsAbstract) buf.Write(" abstract='true'");
            if (DerivationProhibitionValue != null) buf.Write(" final='{0}'", DerivationProhibitionValue.Value.ToXsd());
            if (SubstitutedElement != null) buf.Write(" substitutionGroup='{0}:{1}'", LogicalNamespaceAncestor.GetXsdPrefix(SubstitutedElement), SubstitutedElement.Name);
        }
    }
    public sealed class ElementReference : ElementBase {
        internal ElementReference(ChildStruct parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal const string NodeLabel = "ElementReference";
        internal QualifiableNameEx QNameOpt { get; private set; }
        internal QualifiableNameEx QName {
            get {
                if (QNameOpt == null) CompilationContext.Throw(Keyword, ErrorKind.QualifiableNameRequired);
                return QNameOpt;
            }
        }
        protected override Identifier TryGetDefaultMemberNameId() { return QNameOpt != null ? QNameOpt.NameId : null; }
        private GlobalElement _value;
        internal GlobalElement Value { get { return _value ?? (_value = QName.Resolve<GlobalElement>(NameResolutionKind.Element)); } }
        internal override EntityDeclarationKind Kind { get { return EntityDeclarationKind.Reference; } }
        internal ElementInfo ReferentialElementInfo {
            get { return AnalyzerAncestor.GetObjectInfo<ElementInfo>(Value, NameResolutionKind.Element); }
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            QNameOpt = node.Member("QName").ToQualifiableNameExOpt(this);
        }
        internal override Child MergeTo(Child otherChild) {
            if (otherChild == null) return this;
            var other = otherChild as ElementReference;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, GetType().Name, otherChild.GetType().Name);
            base.MergeTo(other);
            if (QNameOpt != null) other.QNameOpt = QNameOpt.MergeTo(other.QNameOpt);
            return other;
        }
        internal override void GenerateXsd(TextBuffer buf) {
            buf.Write("<xs:element ref='{0}:{1}'", LogicalNamespaceAncestor.GetXsdPrefix(Value), Value.Name);
            GenerateXsdTagPart(buf);
            buf.WriteLine(" />");
        }
    }
    public sealed class ElementWildcard : Child {
        internal ElementWildcard(ChildStruct parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal const string NodeLabel = "ElementWildcard";
        internal Wildcard WildcardOpt { get; private set; }
        internal Wildcard Wildcard {
            get {
                if (WildcardOpt == null) CompilationContext.Throw(Keyword, ErrorKind.WildcardRequired);
                return WildcardOpt;
            }
        }
        protected override Identifier TryGetDefaultMemberNameId() { return new Identifier("Wildcard", Keyword.SourceSpan); }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            WildcardOpt = node.Member("Wildcard").ToWildcardOpt(Keyword, NamespaceAncestor);
        }
        internal override Child MergeTo(Child otherChild) {
            if (otherChild == null) return this;
            var other = otherChild as ElementWildcard;
            if (other == null) CompilationContext.Throw(Keyword, ErrorKind.ObjectNotCompatibleWith, GetType().Name, otherChild.GetType().Name);
            base.MergeTo(other);
            if (WildcardOpt != null) {
                if (other.WildcardOpt == null) other.WildcardOpt = WildcardOpt;
                else if (WildcardOpt != other.WildcardOpt) CompilationContext.Throw(WildcardOpt, ErrorKind.WildcardNotEqualTo, WildcardOpt, other.WildcardOpt);
            }
            return other;
        }
        protected override ChildInfo CreateInfoCore(ChildStructInfo parentInfo, ChildStructInfo restrictedParentInfo, bool isMixed) {
            base.CreateInfoCore(parentInfo, restrictedParentInfo, isMixed);
            var memberNameId = MemberNameId;
            var minOccurs = MinOccurs;
            var maxOccurs = MaxOccurs;
            var isList = maxOccurs > 1;
            var order = -1;
            ChildListInfo restrictedListInfo = null;
            ElementWildcardInfo restrictedElementWildcardInfo = null;
            ChildInfo restrictedItemInfo = null;
            if (GetRestrictedInfo(parentInfo, restrictedParentInfo, memberNameId, minOccurs, maxOccurs, ref order, ref isList,
                ref restrictedListInfo, ref restrictedItemInfo)) {
                restrictedElementWildcardInfo = restrictedItemInfo as ElementWildcardInfo;
                if (restrictedElementWildcardInfo == null)
                    CompilationContext.Throw(Keyword, ErrorKind.RestrictedChildIsNotElementWildcard);
            }
            var wildcardInfo = Wildcard.Info;
            if (restrictedElementWildcardInfo != null && !wildcardInfo.IsEqualToOrRestrictedThan(restrictedElementWildcardInfo.Wildcard))
                CompilationContext.Throw(Keyword, ErrorKind.RestrictingWildcardNotEqualToOrRestrictedThanRestricted, wildcardInfo, restrictedElementWildcardInfo.Wildcard);
            var elementWildcardInfo = new ElementWildcardInfo(Keyword, this, isList ? "ItemClass" : memberNameId.Value + "_Class", GetCSClass(), minOccurs, maxOccurs, isList,
                isList ? -1 : order, isList ? null : memberNameId, restrictedElementWildcardInfo, wildcardInfo) { IsCSClassOverride = restrictedElementWildcardInfo != null };
            return TryCreateListInfo(parentInfo, isList, minOccurs, maxOccurs, order, memberNameId, restrictedListInfo, isMixed, elementWildcardInfo);
        }
        internal override void GenerateXsd(TextBuffer buf) {
            buf.Write("<xs:any");
            Wildcard.GenerateXsd(buf);
            buf.WriteLine(" />");
        }
    }
    //
    public sealed class IdentityConstraint : AnnotatableObject {
        internal IdentityConstraint(Element parent, Node node) {
            Parent = parent;
            base.Initialize(node);
            var keywordNode = node.Member("Keyword");
            Keyword = new SimpleToken(keywordNode);
            switch (keywordNode.MemberXTokenKind()) {
                case XTokenKind.KeyKeyword: Kind = IdentityConstraintKind.Key; break;
                case XTokenKind.UniqueKeyword: Kind = IdentityConstraintKind.Unique; break;
                case XTokenKind.KeyRefKeyword: Kind = IdentityConstraintKind.KeyRef; break;
                default: throw new InvalidOperationException();
            }
            NameId = new Identifier(node.Member("Name"));
            var nsObj = NamespaceAncestor;
            FullName = nsObj.GetFullName(NameId.PlainValue);
            if (IsKeyRef) QName = new QualifiableName(node.Member("QName"));
            IdentityPathExpression = new PathExpression(nsObj, node.Member("Identity"), true);
            foreach (var valueNode in node.Member("Values").Items)
                ValuePathExpressionList.Add(new PathExpression(nsObj, valueNode, false));
            if (IsSplitListValue && ValuePathExpressionList.Count > 1)
                CompilationContext.Throw(SplitListValueKeyword, ErrorKind.SplitListValueAnnotationRequiresSingleValuePathExpression);
            nsObj.IdentityConstraintList.Add(this);
        }
        protected override bool InitializeAnnotations(string name, Node node) {
            switch (name) {
                case EX.SplitListValueNodeLabel:
                    if (SplitListValueKeyword != null) return false;
                    SplitListValueKeyword = new SimpleToken(node);
                    return true;
            }
            return base.InitializeAnnotations(name, node);
        }
        internal readonly SimpleToken Keyword;
        internal readonly IdentityConstraintKind Kind;
        internal bool IsKeyRef { get { return Kind == IdentityConstraintKind.KeyRef; } }
        internal readonly Identifier NameId;
        internal readonly XName FullName;
        internal SimpleToken SplitListValueKeyword { get; private set; }
        internal bool IsSplitListValue { get { return SplitListValueKeyword != null; } }
        internal readonly QualifiableName QName;//for KeyRef
        internal IdentityConstraint Referential;//for KeyRef
        internal readonly PathExpression IdentityPathExpression;
        internal readonly List<PathExpression> ValuePathExpressionList = new List<PathExpression>();
        private IdentityConstraintInfo _info;
        internal IdentityConstraintInfo Info {
            get {
                return _info ?? (_info = new IdentityConstraintInfo(Kind, FullName, Referential == null ? null : Referential.FullName, IsSplitListValue,
                    IdentityPathExpression.Info, ValuePathExpressionList.Select(i => i.Info).ToArray()));
            }
        }
    }
    public sealed class PathExpression : ValueBase {
        internal PathExpression(Namespace nsObj, Node node, bool isIdentity) {
            base.Initialize(node);
            foreach (var pathNode in node.Items)
                PathList.Add(new Path(nsObj, pathNode, isIdentity));
        }
        internal readonly List<Path> PathList = new List<Path>();
        private PathExpressionInfo _info;
        internal PathExpressionInfo Info {
            get { return _info ?? (_info = new PathExpressionInfo(PathList.Select(i => i.Info).ToArray())); }
        }
    }
    public sealed class Path : ValueBase {
        internal Path(Namespace nsObj, Node node, bool isIdentity) {
            base.Initialize(node);
            foreach (var stepNode in node.Items)
                StepList.Add(new Step(nsObj, stepNode));
            var count = StepList.Count;
            for (var i = 0; i < count; i++) {
                var step = StepList[i];
                if (step.IsAttribute) {
                    if (isIdentity) CompilationContext.Throw(step.AtToken, ErrorKind.AttributeStepNotAllowedInIdentityPath);
                    if (i < count - 1) CompilationContext.Throw(step.AtToken, ErrorKind.InvalidAttributeStep);
                }
            }
        }
        internal readonly List<Step> StepList = new List<Step>();
        private PathInfo _info;
        internal PathInfo Info { get { return _info ?? (_info = new PathInfo(StepList.Select(i => i.Info).ToArray())); } }
    }
    public sealed class Step : ValueBase {
        internal Step(Namespace nsObj, Node node) {
            base.Initialize(node);
            DotToken = node.Member("DotToken").ToSimpleTokenOpt();
            if (DotToken != null) {
                AsteriskAsteriskToken = node.Member("AsteriskAsteriskToken").ToSimpleTokenOpt();
                if (AsteriskAsteriskToken != null) Kind = StepKind.SelfAndDescendants;
                else Kind = StepKind.Self;
            }
            else {
                AsteriskAsteriskToken = node.Member("AsteriskAsteriskToken").ToSimpleTokenOpt();
                if (AsteriskAsteriskToken != null) Kind = StepKind.Descendants;
                else {
                    AtToken = node.Member("AtToken").ToSimpleTokenOpt();
                    AsteriskToken = node.Member("AsteriskToken").ToSimpleTokenOpt();
                    if (AsteriskToken != null) Kind = StepKind.ChildrenOrAttributes;
                    else {
                        UriOrAlias = new UriOrAlias(nsObj, node.Member("UriOrAlias"));
                        Uri = UriOrAlias.Uri;
                        //if (!Uri.IsEmpty()) nsObj.AddXsdUri(Uri);
                        var localNameNode = node.Member("LocalName");
                        LocalNameToken = new SimpleToken(localNameNode);
                        if (localNameNode.MemberCSTokenKind() == SyntaxKind.AsteriskToken) {
                            //if (Uri.IsEmpty()) Errors.Throw(LocalNameToken, ErrorKind.WildcardLocalNameMustHasNonEmptyUri);
                            Kind = StepKind.Uri;
                        }
                        else {
                            Name = Uri.GetName(new Identifier(localNameNode).PlainValue);
                            Uri = null;
                            Kind = StepKind.Name;
                        }
                    }
                }
            }
        }
        internal readonly StepKind Kind;
        internal readonly SimpleToken DotToken;
        internal readonly SimpleToken AsteriskAsteriskToken;
        internal readonly SimpleToken AtToken;
        internal readonly SimpleToken AsteriskToken;
        internal readonly UriOrAlias UriOrAlias;
        internal readonly SimpleToken LocalNameToken;
        internal bool IsAttribute { get { return AtToken != null; } }
        internal readonly XNamespace Uri;
        internal readonly XName Name;
        private StepInfo _info;
        internal StepInfo Info { get { return _info ?? (_info = new StepInfo(Kind, IsAttribute, Uri, Name)); } }
    }
    //
    //
    internal static class EX {
        internal const string CSNamespaceNodeLabel = "CSNamespace";
        internal const string ElementQualificationNodeLabel = "ElementQualification";
        internal const string AttributeQualificationNodeLabel = "AttributeQualification";
        internal const string NullableNodeLabel = "Nullable";
        internal const string AbstractNodeLabel = "Abstract";
        internal const string MixedNodeLabel = "Mixed";
        internal const string FixedNodeLabel = "Fixed";
        internal const string OptionalNodeLabel = "Optional";
        internal const string SubstitutionNodeLabel = "Substitution";
        internal const string MemberNameNodeLabel = "MemberName";
        internal const string SplitListValueNodeLabel = "SplitListValue";

        //
        internal static QualifiableNameEx ToQualifiableNameExOpt(this Node node, Object parent) {
            if (node.IsNull) return null;
            return new QualifiableNameEx(parent, node);
        }
        internal static TypeOrReference ToTypeOrReferenceOpt(this Node node, Object parent) {
            if (node.IsNull) return null;
            return new TypeOrReference(parent, node);
        }
        internal static Wildcard ToWildcardOpt(this Node node, SimpleToken keyword, Namespace nsObj) {
            if (node.IsNull) return null;
            return new Wildcard(keyword, nsObj, node);
        }
        internal static bool IsSet(this DerivationProhibition value, DerivationProhibition flag) { return (value & flag) != 0; }
        //
        internal static string ToXsdQualification(this bool value) { return value ? "qualified" : "unqualified"; }
        internal static string ToXsd(this DerivationProhibition value) {
            if (value == DerivationProhibition.None) return "";
            if (value == DerivationProhibition.All) return "#all";
            var sb = new StringBuilder();
            if (value.IsSet(DerivationProhibition.Extension)) sb.Append(" extension");
            if (value.IsSet(DerivationProhibition.Restriction)) sb.Append(" restriction");
            if (value.IsSet(DerivationProhibition.List)) sb.Append(" list");
            if (value.IsSet(DerivationProhibition.Union)) sb.Append(" union");
            return sb.ToString();
        }
        internal static string ToXsd(this InstanceProhibition value) {
            if (value == InstanceProhibition.None) return "";
            if (value == InstanceProhibition.All) return "#all";
            var sb = new StringBuilder();
            if (value.IsSet(InstanceProhibition.Extension)) sb.Append(" extension");
            if (value.IsSet(InstanceProhibition.Restriction)) sb.Append(" restriction");
            if (value.IsSet(InstanceProhibition.Substitution)) sb.Append(" substitution");
            return sb.ToString();
        }
        internal static string ToXsd(this WhitespaceNormalization value) {
            switch (value) {
                case WhitespaceNormalization.Preserve: return "preserve";
                case WhitespaceNormalization.Replace: return "replace";
                case WhitespaceNormalization.Collapse: return "collapse";
                default: throw new InvalidOperationException();
            }
        }
        internal static string ToXsd(this ChildContainerKind value) {
            switch (value) {
                case ChildContainerKind.Seq: return "sequence";
                case ChildContainerKind.Choice: return "choice";
                case ChildContainerKind.Unordered: return "all";
                default: throw new InvalidOperationException();
            }
        }
        internal static string ToXsd(this WildcardUriKind value) {
            switch (value) {
                case WildcardUriKind.Any: return "##any";
                case WildcardUriKind.Other: return "##other";
                //case WildcardNamespaceKind.This: return "##targetNamespace";
                case WildcardUriKind.Unqualified: return "##local";
            }
            return null;
        }
        internal static string ToXsd(this WildcardValidation value) {
            switch (value) {
                case WildcardValidation.SkipValidate: return "skip";
                case WildcardValidation.MustValidate: return "strict";
                case WildcardValidation.TryValidate: return "lax";
                default: throw new InvalidOperationException();
            }
        }
    }
}
