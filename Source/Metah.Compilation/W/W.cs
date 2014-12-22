//#define DUMPMODEL
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Metah.Compilation.W {
    public abstract class Object : ObjectBase {
    }
    public sealed class Analyzer : Object {
        private readonly WAnalyzerInput _analyzerInput;
        private readonly List<CompilationUnit> _compilationUnitList = new List<CompilationUnit>();
        public IReadOnlyList<CompilationUnit> CompilationUnits { get { return _compilationUnitList; } }
        internal readonly HashSet<DottedName> ImportNameSet = new HashSet<DottedName>();
        private static readonly CSharpCompilationOptions _compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, warningLevel: 0);
        private CSharpCompilation CreateCompilationAndReport(bool forImports) {
            var compilation = CSharpCompilation.Create(
                assemblyName: "__TEMP__",
                options: _compilationOptions,
                syntaxTrees: _compilationUnitList.Select(cu => forImports ? cu.GetModelSyntaxTree(true) : cu.ModelSyntaxTree)
                    .Concat(_analyzerInput.CSharpItemList.Select(i => i.SyntaxTree)),
                references: _analyzerInput.CompilationInput.MetadataReferenceList);
#if DUMPMODEL
            foreach (var cu in CompilationUnitList) {
                if (forImports) {
                    System.IO.File.WriteAllText(cu.FilePath + ".importmodel.cs", cu.GetModelSyntaxTree(true).GetRoot().NormalizeWhitespace().ToString());
                }
                else {
                    System.IO.File.WriteAllText(cu.FilePath + ".model.cs", cu.ModelSyntaxTree.GetRoot().NormalizeWhitespace().ToString());
                }
            }
#endif
            CS.ReportDiagnostics(compilation.GetDiagnostics(), _analyzerInput.WItemList.Select(i => i.FilePath));
            CompilationContext.ThrowIfHasErrors();
            return compilation;
        }
        internal Analyzer(WAnalyzerInput analyzerInput) {
            if (analyzerInput == null) throw new ArgumentNullException("analyzerInput");
            _analyzerInput = analyzerInput;
            for (var i = 0; i < analyzerInput.WItemList.Count; i++) {
                _compilationUnitList.Add(new CompilationUnit(this, i, analyzerInput.WItemList[i]));
            }
            if (_compilationUnitList.Count == 0) throw new InvalidOperationException();
            CSharpCompilation compilation;
            if (ImportNameSet.Count > 0) {
                compilation = CreateCompilationAndReport(true);
                _compilationUnitList[0].ModelImportsClass = ImportsProcessor.Process(compilation, ImportNameSet);
            }
            compilation = CreateCompilationAndReport(false);
            foreach (var cu in _compilationUnitList) {
                cu.GetImpl(compilation);
            }
            CompilationContext.ThrowIfHasErrors();
        }
    }
    internal static class ImportsProcessor {
        internal static ClassDeclarationSyntax Process(CSharpCompilation compilation, HashSet<DottedName> importNameSet) {
            var nsSymbol = compilation.GlobalNamespace;
            var methodList = new List<MethodDeclarationSyntax>();
            foreach (var importName in importNameSet) {
                if (importName == null) {
                    AddMethods(nsSymbol, methodList);
                }
                else {
                    AddMethods(importName, nsSymbol, methodList);
                }
            }
            return CS.Class(null, CS.InternalStaticTokenList, "MetahWModelImportsExtensions", null, methodList);
        }
        private static void AddMethods(DottedName importName, INamespaceSymbol nsSymbol, List<MethodDeclarationSyntax> methodList) {
            foreach (var item in importName.ItemList) {
                nsSymbol = nsSymbol.GetMembers(item.PlainValue).FirstOrDefault() as INamespaceSymbol;
                if (nsSymbol == null) {
                    CompilationContext.Throw(item, ErrorKind.InvalidNamespaceName, item);
                }
            }
            AddMethods(nsSymbol, methodList);
        }
        private static void AddMethods(INamespaceSymbol nsSymbol, List<MethodDeclarationSyntax> methodList) {
            foreach (var typeSymbol in nsSymbol.GetTypeMembers()) {
                INamedTypeSymbol activitySymbol;
                TypeSyntax resultType = null;
                activitySymbol = typeSymbol.TryGetBaseTypeSymbol(CSEX.Activity1MetaNameParts);
                if (activitySymbol != null) {
                    resultType = ((INamedTypeSymbol)activitySymbol.TryGetPropertySymbol("Result").Type).TypeArguments[0].ToTypeSyntax();
                }
                else {
                    activitySymbol = typeSymbol.TryGetBaseTypeSymbol(CSEX.ActivityMetaNameParts);
                }
                if (activitySymbol != null) {
                    var isModelActivity = false;
                    foreach (var attData in typeSymbol.GetAttributes()) {
                        if (attData.AttributeClass.IsFullNameEquals(CSEX.ModelActivityAttributeStringNames)) {
                            isModelActivity = true;
                            break;
                        }
                    }
                    if (isModelActivity) continue;
                    var propDataList = new List<StoreData>();
                    foreach (var propSymbol in typeSymbol.GetPropertySymbolsAfter(activitySymbol)) {
                        AddParameter(propSymbol, propDataList);
                    }
                    var hasOutOrRef = false;
                    foreach (var propData in propDataList) {
                        if (propData.Kind != StoreKind.InParameter) {
                            hasOutOrRef = true;
                            break;
                        }
                    }
                    var parameterList = new List<ParameterSyntax>();
                    parameterList.Add(CS.ThisParameter(typeSymbol.ToTypeSyntax(), "__activity__"));
                    foreach (var propData in propDataList) {
                        parameterList.Add(CS.Parameter(
                            modifiers: propData.Kind == StoreKind.InParameter ? default(SyntaxTokenList) :
                                propData.Kind == StoreKind.OutParameter ? CS.OutTokenList : CS.RefTokenList,
                            type: propData.Type,
                            identifier: propData.Name,
                            @default: hasOutOrRef ? null : SyntaxFactory.DefaultExpression(propData.Type)
                            ));
                    }
                    List<TypeParameterConstraintClauseSyntax> typeConstraintClauseList;
                    var typeParameterList = CS.ToTypeParameterSyntaxList(typeSymbol.TypeParameters, out typeConstraintClauseList);
                    methodList.Add(CS.Method(
                        attributeLists: new[] { 
                            CSEX.ModelMethodAttributeList(resultType == null? ModelMethodKind.ActivityVoid: ModelMethodKind.ActivityNonVoid,
                                propDataList.Select(p => p.Name))
                        },
                        modifiers: CS.InternalStaticTokenList,
                        returnType: resultType == null ? CS.VoidType : resultType,
                        identifier: CS.Id("Invoke"),
                        typeParameters: typeParameterList,
                        parameters: parameterList,
                        constraintClauses: typeConstraintClauseList,
                        statements: new[] { CS.ThrowNotImplemented }
                        ));
                }
            }
        }
        private static void AddParameter(IPropertySymbol propSymbol, List<StoreData> propDataList) {
            if (!propSymbol.IsIndexer) {
                var propTypeSymbol = propSymbol.Type;
                var idx = propTypeSymbol.MatchFullName(CSEX.ArgumentMetaNames, CSEX.ActivitiesSystemMetaNameParts);
                if (idx >= 0) {
                    IMethodSymbol setMethodSymbol = propSymbol.SetMethod;
                    if (setMethodSymbol != null && setMethodSymbol.DeclaredAccessibility == Accessibility.Public) {
                        var name = propSymbol.Name.EscapeIdentifier();
                        var propData = new StoreData(name, (StoreKind)idx, ((INamedTypeSymbol)propTypeSymbol).TypeArguments[0].ToTypeSyntax());
                        for (var i = 0; i < propDataList.Count; i++) {
                            if (propDataList[i].Name == name) {
                                propDataList[i] = propData;
                                return;
                            }
                        }
                        propDataList.Add(propData);
                    }
                }
            }
        }
    }

    public abstract class NamespaceBase : Object {
        protected SyntaxList<ExternAliasDirectiveSyntax> ExternList { get; private set; }
        protected SyntaxList<UsingDirectiveSyntax> UsingList { get; private set; }
        private readonly List<MemberDeclarationSyntax> _memberList = new List<MemberDeclarationSyntax>();
        private readonly List<Activity> _activityList = new List<Activity>();
        private readonly List<Namespace> _namespaceList = new List<Namespace>();
        protected override void Initialize(Node node) {
            base.Initialize(node);
            ExternList = node.Member("Externs").ToSyntaxList<ExternAliasDirectiveSyntax>();
            UsingList = node.Member("Prologs").ToSyntaxList<UsingDirectiveSyntax>(true);
            var membersNode = node.Member("Members");
            CS.ToSyntaxNodeAndAdd(_memberList, membersNode, true);
            foreach (var memberNode in membersNode.Items.NonCSNodes()) {
                switch (memberNode.Label) {
                    case "Activity":
                        _activityList.Add(new Activity(this, memberNode));
                        break;
                    case "Namespace":
                        _namespaceList.Add(new Namespace(this, memberNode));
                        break;
                    default: throw new InvalidOperationException();
                }
            }
        }
        protected void GetModelMemberList(List<MemberDeclarationSyntax> memberList, bool forImports) {
            memberList.AddRange(_memberList);
            foreach (var activity in _activityList) {
                activity.GetModel(memberList, forImports);
            }
            foreach (var ns in _namespaceList) {
                ns.GetModel(memberList, forImports);
            }
        }
        protected void GetImplMemberList(CompilationUnitSyntax cuSyntax, SemanticModel semanticModel, List<MemberDeclarationSyntax> memberList) {
            memberList.AddRange(_memberList);
            foreach (var activity in _activityList) {
                activity.GetImpl(cuSyntax, semanticModel, memberList);
            }
            foreach (var ns in _namespaceList) {
                ns.GetImpl(cuSyntax, semanticModel, memberList);
            }
        }
    }
    public sealed class CompilationUnit : NamespaceBase {
        internal CompilationUnit(Analyzer parent, int index, NodeAnalyzerInputItem nodeItem) {
            Parent = parent;
            _index = index;
            _compilationInputFile = nodeItem.CompilationInputFile;
            Initialize(nodeItem.GetNodeOnce());
        }
        new public Analyzer Parent { get { return (Analyzer)base.Parent; } private set { base.Parent = value; } }
        private readonly int _index;
        private readonly CompilationInputFile _compilationInputFile;
        public string FilePath { get { return _compilationInputFile.FilePath; } }
        private SyntaxList<AttributeListSyntax> _attributeListList;
        internal ClassDeclarationSyntax ModelImportsClass { get; set; }//for index 0
        protected override void Initialize(Node node) {
            base.Initialize(node);
            foreach (var prologNode in node.Member("Prologs").Items.NonCSNodes()) {
                var importNameNode = prologNode.Member("Name");
                Parent.ImportNameSet.Add(importNameNode.IsNull ? null : new DottedName(importNameNode));
            }
            _attributeListList = node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>();
        }
        private CompilationUnitSyntax GetModel(bool forImports) {
            var memberList = new List<MemberDeclarationSyntax>();
            GetModelMemberList(memberList, forImports);
            if (_index == 0) {
                CSEX.AddModelGlobalMembers(memberList, forImports);
                if (ModelImportsClass != null) {
                    memberList.Add(ModelImportsClass);
                }
            }
            return SyntaxFactory.CompilationUnit(ExternList, UsingList, _attributeListList, SyntaxFactory.List(memberList));
        }
        internal SyntaxTree GetModelSyntaxTree(bool forImports) {
            return CSharpSyntaxTree.Create(GetModel(forImports), path: FilePath);
        }
        private SyntaxTree _modelSyntaxTree;
        internal SyntaxTree ModelSyntaxTree {
            get { return _modelSyntaxTree ?? (_modelSyntaxTree = GetModelSyntaxTree(false)); }
        }
        private CompilationUnitSyntax _implCUSyntax;
        internal CompilationUnitSyntax GetImpl(CSharpCompilation compilation) {
            var memberList = new List<MemberDeclarationSyntax>();
            GetImplMemberList((CompilationUnitSyntax)_modelSyntaxTree.GetRoot(), compilation.GetSemanticModel(_modelSyntaxTree), memberList);
            if (_index == 0) {
                CSEX.AddImplGlobalMembers(memberList);
            }
            return _implCUSyntax = SyntaxFactory.CompilationUnit(ExternList, UsingList, _attributeListList, SyntaxFactory.List(memberList));
        }
        private string _csText;
        public string CSText {
            get { return _csText ?? (_csText = Extensions.CSBanner + _implCUSyntax.NormalizeWhitespace().ToString()); }
        }
    }
    public sealed class Namespace : NamespaceBase {
        internal Namespace(NamespaceBase parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private NameSyntax _name;
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _name = (NameSyntax)node.Member("Name").ToSyntaxNode();
        }
        internal void GetModel(List<MemberDeclarationSyntax> memberList, bool forImports) {
            var nsMemberList = new List<MemberDeclarationSyntax>();
            GetModelMemberList(nsMemberList, forImports);
            memberList.Add(SyntaxFactory.NamespaceDeclaration(_name, ExternList, UsingList, SyntaxFactory.List(nsMemberList)));
        }
        internal void GetImpl(CompilationUnitSyntax cuSyntax, SemanticModel semanticModel, List<MemberDeclarationSyntax> memberList) {
            var nsMemberList = new List<MemberDeclarationSyntax>();
            GetImplMemberList(cuSyntax, semanticModel, nsMemberList);
            memberList.Add(SyntaxFactory.NamespaceDeclaration(_name, ExternList, UsingList, SyntaxFactory.List(nsMemberList)));
        }
    }
    public sealed class Activity : Object, IStoreHost {
        internal Activity(NamespaceBase parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private SyntaxList<AttributeListSyntax> _attributeListList;
        private SyntaxTokenList _modifierList;
        private SyntaxToken _identifier;
        private TypeParameterListSyntax _typeParameterList;
        private BaseListSyntax _baseList;
        private SyntaxList<TypeParameterConstraintClauseSyntax> _constraintClauseList;
        private readonly List<MemberDeclarationSyntax> _classMemberList = new List<MemberDeclarationSyntax>();
        //
        private SimpleToken _keyword;
        private ResultParameter _resultParameter; //opt
        private bool HasResult { get { return _resultParameter != null; } }
        private readonly List<Parameter> _parameterList = new List<Parameter>();
        private bool? _hasOutOrRefParameter;
        private bool HasOutOrRefParameter {
            get {
                if (_hasOutOrRefParameter == null) {
                    _hasOutOrRefParameter = _parameterList.Any(p => p.Kind != StoreKind.InParameter);
                }
                return _hasOutOrRefParameter.Value;
            }
        }
        Store IStoreHost.TryGetStore(string plainName) {
            if (_resultParameter != null && _resultParameter.Name.PlainValue == plainName) return _resultParameter;
            foreach (var p in _parameterList) if (p.Name.PlainValue == plainName) return p;
            return null;
        }
        internal Sequence Body { get; set; }
        private SyntaxAnnotation _modelBodyAnn;
        //
        private List<ServiceOperation> _serviceOperationList;
        internal List<ServiceOperation> ServiceOperationList { get { return _serviceOperationList ?? (_serviceOperationList = new List<ServiceOperation>()); } }
        //
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _keyword = new SimpleToken(node.Member("Keyword"));
            TypeSyntax baseType;
            var resultTypeNode = node.Member("ResultType");
            if (resultTypeNode.IsNotNull) {
                _resultParameter = new ResultParameter(this, resultTypeNode);
                baseType = CSEX.ActivityOf(_resultParameter.Type).SetSourceSpan(resultTypeNode.SourceSpan);
            }
            else {
                baseType = CSEX.ActivityName.SetSourceSpan(_keyword.SourceSpan);
            }
            foreach (var parameterNode in node.Member("Parameters").Items) {
                _parameterList.Add(new Parameter(this, parameterNode));
            }
            new Sequence(this, node.Member("Body"));
            if (_serviceOperationList != null) {
                foreach (var reply in _serviceOperationList.OfType<ReplyServiceOperation>()) {
                    reply.ResolveRefRequestCorr();
                }
                foreach (var request in _serviceOperationList.OfType<RequestServiceOperation>()) {
                    request.PostInitialize();
                }
            }
            //
            _attributeListList = node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>();
            _modifierList = node.Member("Modifiers").ToSyntaxTokenList();
            _identifier = node.Member("Identifier").ToSyntaxToken();
            _typeParameterList = (TypeParameterListSyntax)node.Member("TypeParameterList").ToSyntaxNode();
            var baseListNode = node.Member("BaseList");
            if (baseListNode.IsNotNull) {
                _baseList = SyntaxFactory.BaseList(baseListNode.Member("ColonToken").ToSyntaxToken(),
                    CS.ToSeparatedList<BaseTypeSyntax>(SyntaxFactory.SimpleBaseType(baseType), CS.CommaToken, baseListNode.Member("Types")));
            }
            else {
                _baseList = CS.BaseList(baseType);
            }
            _constraintClauseList = node.Member("ConstraintClauses").ToSyntaxList<TypeParameterConstraintClauseSyntax>();
            CS.ToSyntaxNodeAndAdd(_classMemberList, node.Member("ClassMembers"));
        }
        internal void GetModel(List<MemberDeclarationSyntax> memberList, bool forImports) {
            var clsMemberList = new List<MemberDeclarationSyntax>();
            clsMemberList.AddRange(_classMemberList);
            foreach (var parameter in _parameterList) {
                clsMemberList.Add(parameter.GetProperty());
            }
            if (!forImports) {
                //>internal RESULTPARATYPE Invoke(PARA1TYPE PARA1NAME, out PARA2TYPE PARA2NAME, ref PARA3TYPE PARA3NAME) {
                //>  var Result = default(RESTYPE);//opt
                //>  PARA2NAME = default(PARA2TYPE);
                //>  ...body...
                //>  throw new NotImplementedException();
                //>}
                var invokeStmList = new List<StatementSyntax>();
                if (_resultParameter != null) {
                    invokeStmList.Add(CS.LocalDeclStm(CS.VarIdName, _resultParameter.Name.CSToken,
                        SyntaxFactory.DefaultExpression(_resultParameter.Type)));
                }
                foreach (var parameter in _parameterList) {
                    if (parameter.IsOut) {
                        invokeStmList.Add(CS.AssignStm(parameter.Name.CSIdName, SyntaxFactory.DefaultExpression(parameter.Type)));
                    }
                }
                invokeStmList.Add(Body.GetModel().SetAnn(out _modelBodyAnn));
                invokeStmList.Add(CS.ThrowNotImplemented);
                clsMemberList.Add(CS.Method(
                    attributeLists: new[] { CSEX.ModelMethodAttributeList(_resultParameter != null? ModelMethodKind.ActivityNonVoid: ModelMethodKind.ActivityVoid, 
                        _parameterList.Select(p => p.Name.Value)) },
                    modifiers: CS.InternalTokenList,
                    returnType: _resultParameter != null ? _resultParameter.Type : CS.VoidType,
                    identifier: CS.Id("Invoke"),
                    parameters: _parameterList.Select(p => CS.Parameter(
                        modifiers: p.IsIn ? default(SyntaxTokenList) : p.IsOut ? CS.OutTokenList : CS.RefTokenList,
                        type: p.Type,
                        identifier: p.Name.CSToken,
                        @default: HasOutOrRefParameter ? null : SyntaxFactory.DefaultExpression(p.Type))),
                        statements: invokeStmList));
            }
            var attListList = _attributeListList;
            if (forImports) {
                attListList = attListList.Insert(0, CSEX.ModelActivityAttributeList);
            }
            memberList.Add(SyntaxFactory.ClassDeclaration(
                attributeLists: attListList,
                modifiers: _modifierList,
                identifier: _identifier,
                typeParameterList: _typeParameterList,
                //parameterList: null,
                baseList: _baseList,
                constraintClauses: _constraintClauseList,
                members: SyntaxFactory.List(clsMemberList)
                ));
        }
        internal void GetImpl(CompilationUnitSyntax cuSyntax, SemanticModel semanticModel, List<MemberDeclarationSyntax> memberList) {
            var clsMemberList = new List<MemberDeclarationSyntax>();
            clsMemberList.AddRange(_classMemberList);
            foreach (var parameter in _parameterList) {
                clsMemberList.Add(parameter.GetProperty());
            }
            //>private Activity __GetImplementation__() {
            //>  Activity __vroot__;
            //>  ...
            //>  return __vroot__;
            //>}
            var getImplStmList = new List<StatementSyntax>();
            getImplStmList.Add(CS.LocalDeclStm(CSEX.ActivityName, "__vroot__"));
            var ctx = new SemanticContext(cuSyntax.TryGetAnnedNode(_modelBodyAnn), semanticModel);
            if (_serviceOperationList != null) {
                foreach (var request in _serviceOperationList.OfType<RequestServiceOperation>()) {
                    request.PreGetImpl(ctx, getImplStmList);
                }
            }
            Body.GetImpl(ctx, getImplStmList, expr => CS.AssignStm(CS.IdName("__vroot__"), expr));
            getImplStmList.Add(CS.ReturnStm(CS.IdName("__vroot__")));
            clsMemberList.Add(CS.Method(CS.PrivateTokenList, CSEX.ActivityName, CS.Id("__GetImplementation__"), null, getImplStmList));
            //>private Func<Activity> __implementation__;
            clsMemberList.Add(CS.Field(CS.PrivateTokenList, CS.FuncOf(CSEX.ActivityName), "__implementation__"));
            //>protected override Func<Activity> Implementation {
            //>  get {
            //>    return __implementation__ ?? (__implementation__ = __GetImplementation__);
            //>  }
            //>  set { throw new NotSupportedException(); }
            //>}
            clsMemberList.Add(CS.Property(CS.ProtectedOverrideTokenList, CS.FuncOf(CSEX.ActivityName), "Implementation", false,
                default(SyntaxTokenList), new[] {
                    CS.ReturnStm(CS.CoalesceExpr(CS.IdName("__implementation__"),
                        CS.ParedExpr(CS.AssignExpr(CS.IdName("__implementation__"), CS.IdName("__GetImplementation__")))))
                },
                default(SyntaxTokenList), new[] { CS.ThrowNotSupported }));
            //>
            memberList.Add(SyntaxFactory.ClassDeclaration(
                attributeLists: _attributeListList,
                modifiers: _modifierList,
                identifier: _identifier,
                typeParameterList: _typeParameterList,
                //parameterList: null,
                baseList: _baseList,
                constraintClauses: _constraintClauseList,
                members: SyntaxFactory.List(clsMemberList)
                ));
        }
        //
        private int _modelVarNameIdx;
        internal void GetModelVarDecl(List<StatementSyntax> stmList, TypeSyntax type, ExpressionSyntax value) {
            //>TYPE __v__0 = VALUE;
            stmList.Add(CS.LocalDeclStm(type ?? CS.VarIdName, "__v__" + (_modelVarNameIdx++).ToInvariantString(), value));
        }
        internal IdentifierNameSyntax GetModelVarName(List<StatementSyntax> stmList, TypeSyntax type, ExpressionSyntax value) {
            //>TYPE __v__0 = VALUE;
            var id = CS.Id("__v__" + (_modelVarNameIdx++).ToInvariantString());
            stmList.Add(CS.LocalDeclStm(type ?? CS.VarIdName, id, value));
            return CS.IdName(id);
        }
        private int _implVarNameIdx;
        internal LocalDeclarationStatementSyntax GetImplVarDecl(ExpressionSyntax value, out IdentifierNameSyntax varName) {
            //>var __v__0 = VALUE;
            var id = CS.Id("__v__" + (_implVarNameIdx++).ToInvariantString());
            var stm = CS.LocalDeclStm(CS.VarIdName, id, value);
            varName = CS.IdName(id);
            return stm;
        }
        internal IdentifierNameSyntax GetImplVarName(List<StatementSyntax> stmList, TypeSyntax type, ExpressionSyntax value) {
            //>var __v__0 = VALUE;
            var id = CS.Id("__v__" + (_implVarNameIdx++).ToInvariantString());
            stmList.Add(CS.LocalDeclStm(type, id, value));
            return CS.IdName(id);
        }
        internal IdentifierNameSyntax GetImplVarName(List<StatementSyntax> stmList, ExpressionSyntax value) {
            return GetImplVarName(stmList, CS.VarIdName, value);
        }
        internal IdentifierNameSyntax GetImplVarNameNoInit(List<StatementSyntax> stmList, TypeSyntax type) {
            //>TYPE __v__0;
            var id = CS.Id("__v__" + (_implVarNameIdx++).ToInvariantString());
            stmList.Add(CS.LocalDeclStm(type, id));
            return CS.IdName(id);
        }
        private int _requestCorrVarNameAutoIdx;
        internal string GetRequestCorrAutoVarName() {
            return "__vreqcorr__" + (_requestCorrVarNameAutoIdx++).ToInvariantString();
        }

    }
    public interface IStoreHost {
        Store TryGetStore(string plainName);
    }
    public enum StoreKind {
        InParameter = 0,//DO NOT change the value
        OutParameter = 1,
        RefParameter = 2,
        Variable = 3,
    }
    public struct StoreData {
        internal StoreData(string name, StoreKind kind, TypeSyntax type) {
            Name = name;
            Kind = kind;
            Type = type;
        }
        internal readonly string Name;
        internal readonly StoreKind Kind;
        internal readonly TypeSyntax Type;
    }
    public abstract class Store : Object {
        public Identifier Name { get; private set; }
        public TypeSyntax Type { get; protected set; }
        public StoreKind Kind { get; protected set; }
        internal ExpressionSyntax RewrittenExpr { get; set; }//opt
        protected void SetName(Identifier name, bool checkDup = true) {
            if (checkDup) {
                if (GetAncestor<IStoreHost>().TryGetStore(name.PlainValue) != null) {
                    CompilationContext.Throw(name, ErrorKind.DuplicateActivityVariableOrParameterName, name);
                }
            }
            Name = name;
        }
    }
    public abstract class ParameterBase : Store {
        new public Activity Parent { get { return (Activity)base.Parent; } protected set { base.Parent = value; } }
        internal bool IsIn { get { return Kind == StoreKind.InParameter; } }
        internal bool IsOut { get { return Kind == StoreKind.OutParameter; } }
        internal bool IsRef { get { return Kind == StoreKind.RefParameter; } }
    }
    public sealed class ResultParameter : ParameterBase {
        internal ResultParameter(Activity parent, Node node) {
            Parent = parent;
            base.Initialize(node);
            SetName(new Identifier("Result", SourceSpan), false);
            Type = node.ToNonVarTypeSyntax();
            Kind = StoreKind.OutParameter;
        }
    }
    public sealed class Parameter : ParameterBase {
        internal Parameter(Activity parent, Node node) {
            Parent = parent;
            base.Initialize(node);
            SetName(new Identifier(node.Member("Name")));
            Type = node.Member("Type").ToNonVarTypeSyntax();
            var modifierNode = node.Member("Modifier");
            if (modifierNode.IsNotNull) {
                Kind = modifierNode.MemberCSTokenKind() == SyntaxKind.OutKeyword ? StoreKind.OutParameter : StoreKind.RefParameter;
            }
            _attributeListList = node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>();
        }
        private readonly SyntaxList<AttributeListSyntax> _attributeListList;
        internal PropertyDeclarationSyntax GetProperty() {
            return SyntaxFactory.PropertyDeclaration(
                attributeLists: _attributeListList,
                modifiers: CS.PublicTokenList,
                type: IsIn ? CSEX.InArgumentOf(Type) :
                    IsOut ? CSEX.OutArgumentOf(Type) : CSEX.InOutArgumentOf(Type),
                explicitInterfaceSpecifier: null,
                identifier: Name.CSToken,
                accessorList: CS.GetSetAccessorList,
                expressionBody: null,
                initializer: null
                );
        }
    }
    public sealed class Variable : Store {
        internal Variable(Object parent, Node typeNode, Node nameNode) {
            Parent = parent;
            base.Initialize(nameNode);
            SetName(new Identifier(nameNode));
            Type = typeNode.ToNonVarTypeSyntax();
            Kind = StoreKind.Variable;
        }
        internal Variable(Object parent, TypeSyntax type, Identifier name) {
            Parent = parent;
            SourceSpan = name.SourceSpan;
            SetName(name);
            Type = type;
            Kind = StoreKind.Variable;
        }
        internal LocalDeclarationStatementSyntax GetModel() {
            //>var name = default(type);
            return CS.LocalDeclStm(CS.VarIdName, Name.CSToken, SyntaxFactory.DefaultExpression(Type));
        }
        internal LocalDeclarationStatementSyntax GetImpl() {
            //>var name = new Variable<type>();
            return CS.LocalDeclStm(CS.VarIdName, Name.CSToken, CS.NewObjExpr(CSEX.VariableOf(Type)));
        }
    }
    public enum ModelMethodKind {
        ActivityVoid = 0,//return void
        ActivityNonVoid,
        Action,//return void
        Func,
    }
    internal sealed class ImplRewriter : CSharpSyntaxRewriter {
        private ImplRewriter() { }
        //[ThreadStatic]
        //private static ImplRewriter _instance;
        //private static ImplRewriter Instance { get { return _instance ?? (_instance = new ImplRewriter()); } }
        private static ImplRewriter Instance { get { return new ImplRewriter(); } }
        internal static SyntaxNode Rewrite(SyntaxNode node, SemanticModel semanticModel, Statement statement, bool isBlock, out bool hasInvoke) {
            var instance = Instance;
            var res = instance.RewriteCore(node, null, semanticModel, statement, false, isBlock ? 1 : 0);
            hasInvoke = instance._hasInvoke;
            return res;
        }
        internal static ExpressionSyntax RewriteExpr(ExpressionSyntax expr, TypeSyntax declaredType, SemanticModel semanticModel, Statement statement) {
            return (ExpressionSyntax)Instance.RewriteCore(expr, declaredType, semanticModel, statement, false, 0);
        }
        internal static ExpressionSyntax CheckCSExprOnly(ExpressionSyntax expr, SemanticModel semanticModel, Statement statement) {
            return (ExpressionSyntax)Instance.RewriteCore(expr, null, semanticModel, statement, true, 0);
        }
        private SyntaxNode RewriteCore(SyntaxNode node, TypeSyntax declaredType, SemanticModel semanticModel, Statement statement, bool csExprOnly, int lambdaExprLevel) {
            if (node == null) throw new ArgumentNullException("node");
            if (semanticModel == null) throw new ArgumentNullException("semanticModel");
            if (statement == null) throw new ArgumentNullException("statement");
            _semanticModel = semanticModel;
            _statement = statement;
            _csExprOnly = csExprOnly;
            _hasInvoke = false;
            _leftOpStore = null;
            _lambdaExprLevel = lambdaExprLevel;
            _seqStmList = null;
            _seqActCount = 0;
            //
            var newNode = Visit(node);
            if (declaredType != null) {
                //>new MetahWFuncActivity<declaredType>(...)
                var funcActivity = CSEX.NewMetahWFuncActivity(declaredType, (ExpressionSyntax)newNode);
                if (_seqStmList == null) return funcActivity;
                //>__activity__.Activities.Add();
                _seqStmList.Add(CS.AddInvoStm(CS.IdName("__activity__"), "Activities", funcActivity));
                //>new MetahWSequenceActivity<declaredType>(__activity__ => { STM1; STM2; })
                return CSEX.MetahWSequenceActivity(declaredType, _seqStmList);
            }
            else {
                if (_seqStmList == null) return newNode;
                if (newNode != null) {
                    _seqStmList.Add(CS.AddInvoStm(CS.IdName("__activity__"), "Activities",
                        CSEX.NewMetahWActionActivity(SyntaxFactory.Block(CS.ExprStm((ExpressionSyntax)newNode)))));
                }
                //>new MetahWSequenceActivity(__activity__ => { STM1; STM2; })
                return CSEX.MetahWSequenceActivity(_seqStmList);
            }
        }

        //input
        private SemanticModel _semanticModel;
        private Statement _statement;
        private bool _csExprOnly;
        //output
        private bool _hasInvoke;
        //impl
        private Store _leftOpStore;
        private int _lambdaExprLevel;
        private List<StatementSyntax> _seqStmList;
        private int _seqActCount;
        //
        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax idName) {
            var store = TryGetStore(idName);
            if (store != null) {
                if (store.RewrittenExpr != null) {
                    //>RewrittenExpr.Get(__ctx__)
                    return CSEX.StoreGet(store.RewrittenExpr);
                }
                return CSEX.StoreGet(store);
            }
            return base.VisitIdentifierName(idName);
        }
        private Store TryGetStore(IdentifierNameSyntax idName) {
            var symbol = _semanticModel.GetSymbolInfo(idName).Symbol;
            if (symbol != null) {
                var kind = symbol.Kind;
                if (kind == SymbolKind.Local || kind == SymbolKind.Parameter) {
                    var store = _statement.StoreHost.TryGetStore(idName.Identifier.ValueText);
                    if (store != null) {
                        if (_csExprOnly) {
                            CompilationContext.Throw(idName.Identifier.GetSourceSpan(), ErrorKind.ReferenceToActivityVariableOrParameterNotAllowed);
                        }
                        return store;
                    }
                }
            }
            return null;
        }
        public override SyntaxNode VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) {
            _lambdaExprLevel++;
            var newNode = base.VisitSimpleLambdaExpression(node);
            _lambdaExprLevel--;
            return newNode;
        }
        public override SyntaxNode VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) {
            _lambdaExprLevel++;
            var newNode = base.VisitParenthesizedLambdaExpression(node);
            _lambdaExprLevel--;
            return newNode;
        }
        public override SyntaxNode VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) {
            _lambdaExprLevel++;
            var newNode = base.VisitAnonymousMethodExpression(node);
            _lambdaExprLevel--;
            return newNode;
        }
        public override SyntaxNode VisitQueryBody(QueryBodySyntax node) {
            _lambdaExprLevel++;
            var newNode = base.VisitQueryBody(node);
            _lambdaExprLevel--;
            return newNode;
        }
        public override SyntaxNode VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax expr) {
            var kind = expr.CSharpKind();
            if (kind == SyntaxKind.PreIncrementExpression || kind == SyntaxKind.PreDecrementExpression) {
                var idName = expr.Operand.AsNonPareExpr<IdentifierNameSyntax>();
                if (idName != null) {
                    var store = TryGetStore(idName);
                    if (store != null) {
                        return CSEX.StoreSetEx(store, kind == SyntaxKind.PreIncrementExpression, false);
                    }
                }
            }
            return base.VisitPrefixUnaryExpression(expr);
        }
        public override SyntaxNode VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax expr) {
            var kind = expr.CSharpKind();
            if (kind == SyntaxKind.PostIncrementExpression || kind == SyntaxKind.PostDecrementExpression) {
                var idName = expr.Operand.AsNonPareExpr<IdentifierNameSyntax>();
                if (idName != null) {
                    var store = TryGetStore(idName);
                    if (store != null) {
                        return CSEX.StoreSetEx(store, kind == SyntaxKind.PostIncrementExpression, true);
                    }
                }
            }
            return base.VisitPostfixUnaryExpression(expr);
        }
        public override SyntaxNode VisitAssignmentExpression(AssignmentExpressionSyntax node) {
            var leftIdName = node.Left.AsNonPareExpr<IdentifierNameSyntax>();
            if (leftIdName != null) {
                var leftOpStore = TryGetStore(leftIdName);
                if (leftOpStore != null) {
                    var kind = node.CSharpKind();
                    var right = node.Right;
                    var setLeftStore = false;
                    if (kind == SyntaxKind.SimpleAssignmentExpression && right.AsNonPareExpr<InvocationExpressionSyntax>() != null) {
                        _leftOpStore = leftOpStore;
                        setLeftStore = true;
                    }
                    var genedRight = (ExpressionSyntax)Visit(right);
                    if (setLeftStore && _leftOpStore == null) {
                        return genedRight;
                    }
                    else {
                        if (setLeftStore) {
                            _leftOpStore = null;
                        }
                        switch (kind) {
                            case SyntaxKind.SimpleAssignmentExpression:
                                return CSEX.StoreSetEx(leftOpStore, genedRight);
                            case SyntaxKind.AddAssignmentExpression:
                                return CSEX.StoreSetEx(leftOpStore, CS.AddExpr(CSEX.StoreGet(leftOpStore), genedRight));
                            case SyntaxKind.SubtractAssignmentExpression:
                                return CSEX.StoreSetEx(leftOpStore, CS.SubtractExpr(CSEX.StoreGet(leftOpStore), genedRight));
                            case SyntaxKind.MultiplyAssignmentExpression:
                                return CSEX.StoreSetEx(leftOpStore, CS.MultiplyExpr(CSEX.StoreGet(leftOpStore), genedRight));
                            case SyntaxKind.DivideAssignmentExpression:
                                return CSEX.StoreSetEx(leftOpStore, CS.DivideExpr(CSEX.StoreGet(leftOpStore), genedRight));
                            case SyntaxKind.ModuloAssignmentExpression:
                                return CSEX.StoreSetEx(leftOpStore, CS.ModuloExpr(CSEX.StoreGet(leftOpStore), genedRight));
                            case SyntaxKind.AndAssignmentExpression:
                                return CSEX.StoreSetEx(leftOpStore, CS.BitwiseAndExpr(CSEX.StoreGet(leftOpStore), genedRight));
                            case SyntaxKind.OrAssignmentExpression:
                                return CSEX.StoreSetEx(leftOpStore, CS.BitwiseOrExpr(CSEX.StoreGet(leftOpStore), genedRight));
                            case SyntaxKind.ExclusiveOrAssignmentExpression:
                                return CSEX.StoreSetEx(leftOpStore, CS.ExclusiveOrExpr(CSEX.StoreGet(leftOpStore), genedRight));
                            case SyntaxKind.LeftShiftAssignmentExpression:
                                return CSEX.StoreSetEx(leftOpStore, CS.LeftShiftExpr(CSEX.StoreGet(leftOpStore), genedRight));
                            case SyntaxKind.RightShiftAssignmentExpression:
                                return CSEX.StoreSetEx(leftOpStore, CS.RightShiftExpr(CSEX.StoreGet(leftOpStore), genedRight));
                            default: throw new InvalidOperationException();
                        }
                    }
                }
            }
            return base.VisitAssignmentExpression(node);
        }
        public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node) {
            var kind = node.CSharpKind();
            if (kind == SyntaxKind.LogicalAndExpression || kind == SyntaxKind.LogicalOrExpression || kind == SyntaxKind.CoalesceExpression) {
                var left = (ExpressionSyntax)Visit(node.Left);
                var actIdx = _seqActCount;
                var stmIdx = _seqStmList == null ? 2 : _seqStmList.Count;
                var right = (ExpressionSyntax)Visit(node.Right);
                var actCount = _seqActCount - actIdx;
                if (actCount > 0) {
                    TypeSyntax exprType;
                    if (kind == SyntaxKind.CoalesceExpression) {
                        exprType = _semanticModel.GetTypeInfo(node).Type.ToTypeSyntax();
                    }
                    else {
                        exprType = CS.BoolType;
                    }
                    IdentifierNameSyntax exprVarName;
                    //>var __v__0 = new Variable<>();
                    _seqStmList.Insert(stmIdx++, _statement.ActivityAncestor.GetImplVarDecl(CSEX.NewVariable(exprType), out exprVarName));
                    //>__activity__.Variables.Add(exprVarName);
                    _seqStmList.Insert(stmIdx++, CS.AddInvoStm(CS.IdName("__activity__"), "Variables", exprVarName));
                    //>__activity__.Activities.Add(new MetahWActionActivity(__ctx__ => {
                    //  exprType __v__ = left;
                    //  exprVarName.Set(__ctx__, __v__);
                    //  if( __v__ != true/false/null) __seqidx__.Set(__ctx__, __seqidx__.Get(__ctx__) + (actCount + 1));
                    //}));
                    var tempStmList = new List<StatementSyntax>();
                    var tempName = _statement.ActivityAncestor.GetImplVarName(tempStmList, exprType, left);
                    tempStmList.Add(CS.ExprStm(CSEX.StoreSet(exprVarName, tempName)));
                    tempStmList.Add(SyntaxFactory.IfStatement(CS.NotEqualsExpr(tempName,
                        kind == SyntaxKind.LogicalAndExpression ? CS.TrueLiteral : (kind == SyntaxKind.LogicalOrExpression ? CS.FalseLiteral : CS.NullLiteral)),
                        CS.ExprStm(CSEX.StoreSet(CS.IdName("__seqidx__"),
                            CS.AddExpr(CSEX.StoreGet(CS.IdName("__seqidx__")), CS.Literal(actCount + 1))))));
                    _seqStmList.Insert(stmIdx++, CS.AddInvoStm(CS.IdName("__activity__"), "Activities", CSEX.NewMetahWActionActivity(tempStmList)));
                    _seqActCount++;
                    //>__activity__.Activities.Add(new MetahWActionActivity(__ctx__ => {
                    //  exprType __v__ = right;
                    //  if( __v__ != true/false/null) exprVarName.Set(__ctx__, __v__);
                    //}));
                    tempStmList.Clear();
                    tempName = _statement.ActivityAncestor.GetImplVarName(tempStmList, exprType, right);
                    tempStmList.Add(SyntaxFactory.IfStatement(CS.NotEqualsExpr(tempName,
                        kind == SyntaxKind.LogicalAndExpression ? CS.TrueLiteral : (kind == SyntaxKind.LogicalOrExpression ? CS.FalseLiteral : CS.NullLiteral)),
                        CS.ExprStm(CSEX.StoreSet(exprVarName, tempName))));
                    _seqStmList.Add(CS.AddInvoStm(CS.IdName("__activity__"), "Activities", CSEX.NewMetahWActionActivity(tempStmList)));
                    _seqActCount++;
                    //
                    return CSEX.StoreGet(exprVarName);
                }
                return node.Update(left, node.OperatorToken, right);
            }
            return base.VisitBinaryExpression(node);
        }
        public override SyntaxNode VisitConditionalExpression(ConditionalExpressionSyntax node) {
            //ExpressionSyntax condition = (ExpressionSyntax)this.Visit(node.Condition);
            //SyntaxToken questionToken = this.VisitToken(node.QuestionToken);
            //ExpressionSyntax whenTrue = (ExpressionSyntax)this.Visit(node.WhenTrue);
            //SyntaxToken colonToken = this.VisitToken(node.ColonToken);
            //ExpressionSyntax whenFalse = (ExpressionSyntax)this.Visit(node.WhenFalse);
            //return node.Update(condition, questionToken, whenTrue, colonToken, whenFalse);
            var condition = (ExpressionSyntax)Visit(node.Condition);
            var trueActIdx = _seqActCount;
            var trueStmIdx = _seqStmList == null ? 2 : _seqStmList.Count;
            var whenTrue = (ExpressionSyntax)Visit(node.WhenTrue);
            var trueActCount = _seqActCount - trueActIdx;
            var falseActIdx = _seqActCount;
            var falseStmIdx = _seqStmList == null ? 2 : _seqStmList.Count;
            var whenFalse = (ExpressionSyntax)Visit(node.WhenFalse);
            var falseActCount = _seqActCount - falseActIdx;
            if (trueActCount > 0 || falseActCount > 0) {
                var exprType = _semanticModel.GetTypeInfo(node).Type.ToTypeSyntax();
                IdentifierNameSyntax exprVarName;
                //>var __v__0 = new Variable<>();
                _seqStmList.Insert(trueStmIdx++, _statement.ActivityAncestor.GetImplVarDecl(CSEX.NewVariable(exprType), out exprVarName));
                falseStmIdx++;
                //>__activity__.Variables.Add(exprVarName);
                _seqStmList.Insert(trueStmIdx++, CS.AddInvoStm(CS.IdName("__activity__"), "Variables", exprVarName));
                falseStmIdx++;
                //>__activity__.Activities.Add(new MetahWActionActivity(__ctx__ => {
                //  if(condition == false) __seqidx__.Set(__ctx__, __seqidx__.Get(__ctx__) + (trueActCount + 1));
                //}));
                var tempStmList = new List<StatementSyntax>();
                tempStmList.Add(SyntaxFactory.IfStatement(CS.EqualsExpr(condition, CS.FalseLiteral),
                    CS.ExprStm(CSEX.StoreSet(CS.IdName("__seqidx__"),
                        CS.AddExpr(CSEX.StoreGet(CS.IdName("__seqidx__")), CS.Literal(trueActCount + 1))))));
                _seqStmList.Insert(trueStmIdx++, CS.AddInvoStm(CS.IdName("__activity__"), "Activities", CSEX.NewMetahWActionActivity(tempStmList)));
                falseStmIdx++;
                _seqActCount++;
                //>__activity__.Activities.Add(new MetahWActionActivity(__ctx__ => {
                //  exprVarName.Set(__ctx__, whenTrue);
                //  __seqidx__.Set(__ctx__, __seqidx__.Get(__ctx__) + (falseActCount + 1));
                //}));
                tempStmList.Clear();
                tempStmList.Add(CS.ExprStm(CSEX.StoreSet(exprVarName, whenTrue)));
                tempStmList.Add(CS.ExprStm(CSEX.StoreSet(CS.IdName("__seqidx__"),
                        CS.AddExpr(CSEX.StoreGet(CS.IdName("__seqidx__")), CS.Literal(falseActCount + 1)))));
                _seqStmList.Insert(falseStmIdx++, CS.AddInvoStm(CS.IdName("__activity__"), "Activities", CSEX.NewMetahWActionActivity(tempStmList)));
                _seqActCount++;
                //>__activity__.Activities.Add(new MetahWActionActivity(__ctx__ => {
                //  exprVarName.Set(__ctx__, whenFalse);
                //}));
                tempStmList.Clear();
                tempStmList.Add(CS.ExprStm(CSEX.StoreSet(exprVarName, whenFalse)));
                _seqStmList.Add(CS.AddInvoStm(CS.IdName("__activity__"), "Activities", CSEX.NewMetahWActionActivity(tempStmList)));
                _seqActCount++;
                //
                return CSEX.StoreGet(exprVarName);
            }
            return node.Update(condition, node.QuestionToken, whenTrue, node.ColonToken, whenFalse);
        }
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax invoExpr) {
            var methodSymbol = _semanticModel.GetSymbolInfo(invoExpr).Symbol as IMethodSymbol;
            if (methodSymbol != null) {
                if (methodSymbol.Name == "Invoke") {
                    var methodKind = methodSymbol.MethodKind;
                    if (methodKind == MethodKind.Ordinary || methodKind == MethodKind.ReducedExtension) {
                        var attributes = methodSymbol.GetAttributes();
                        if (attributes.Length == 1) {
                            var attribute = attributes[0];
                            if (attribute.AttributeClass.IsFullNameEquals(CSEX.ModelMethodAttributeStringNames)) {
                                if (_csExprOnly) {
                                    CompilationContext.Throw(invoExpr.ArgumentList.OpenParenToken.GetSourceSpan(), ErrorKind.ActivityInvokeNotAllowed);
                                }
                                if (_lambdaExprLevel > 0) {
                                    CompilationContext.Throw(invoExpr.ArgumentList.OpenParenToken.GetSourceSpan(), ErrorKind.ActivityInvokeCannotBeUsedInCSBlockStmOrLambdaExprEtc);
                                }
                                Store leftOpStore = null;
                                if (_leftOpStore != null) {
                                    leftOpStore = _leftOpStore;
                                    _leftOpStore = null;
                                }
                                var isStmInvo = false;
                                var parentNode = invoExpr.GetNonPareParent();
                                if (parentNode is ExpressionStatementSyntax) {
                                    isStmInvo = true;
                                }
                                else if (leftOpStore != null && parentNode.GetNonPareParent() is ExpressionStatementSyntax) {
                                    isStmInvo = true;
                                }
                                else {
                                    if (_seqStmList == null) {
                                        _seqStmList = new List<StatementSyntax>();
                                        //>var __seqidx__ = new Variable<int>();
                                        //>__activity__.Variables.Add(__seqidx__);
                                        _seqStmList.Add(CS.LocalDeclStm(CS.VarIdName, "__seqidx__", CSEX.NewVariable(CS.IntType)));
                                        _seqStmList.Add(CS.AddInvoStm(CS.IdName("__activity__"), "Variables", CS.IdName("__seqidx__")));
                                    }
                                }
                                _hasInvoke = true;
                                var attConstructorArguments = attribute.ConstructorArguments;
                                var modelMethodKind = (ModelMethodKind)attConstructorArguments[0].Value;
                                string[] parameterNames = null;
                                if (modelMethodKind == ModelMethodKind.ActivityVoid || modelMethodKind == ModelMethodKind.ActivityNonVoid) {
                                    parameterNames = attConstructorArguments[1].Values.Select(i => (string)i.Value).ToArray();
                                }
                                return VisitModelInvoke(invoExpr, modelMethodKind, parameterNames,
                                    (int)attConstructorArguments[2].Value, leftOpStore, isStmInvo);
                            }
                        }
                    }
                }
                return VisitCommonInvoke(invoExpr);
            }
            return base.VisitInvocationExpression(invoExpr);
        }
        private ExpressionSyntax VisitModelInvoke(InvocationExpressionSyntax invoExpr,
            ModelMethodKind modelMethodKind, string[] parameterNames, int parameterCount, Store leftOpStore, bool isStmInvo) {
            //ExpressionSyntax expression = (ExpressionSyntax)this.Visit(node.Expression);
            //ArgumentListSyntax argumentList = (ArgumentListSyntax)this.Visit(node.ArgumentList);
            //return node.Update(expression, argumentList);
            ExpressionSyntax rawExpr = null;
            var memberAccExpr = invoExpr.Expression as MemberAccessExpressionSyntax;
            if (memberAccExpr != null) {
                rawExpr = memberAccExpr.Expression;
            }
            if (rawExpr == null) {
                CompilationContext.Throw(invoExpr.Expression.GetAnySourceSpan(), ErrorKind.InvalidActivityInvokeSyntax);
            }
            rawExpr = (ExpressionSyntax)Visit(rawExpr);
            var arguments = invoExpr.ArgumentList.Arguments;
            var newArguments = ((ArgumentListSyntax)Visit(invoExpr.ArgumentList)).Arguments;
            TypeSyntax retArgType = null;
            IdentifierNameSyntax retArgName = null;
            if ((modelMethodKind == ModelMethodKind.ActivityNonVoid || modelMethodKind == ModelMethodKind.Func) && (_seqStmList != null || leftOpStore != null)) {
                retArgType = _semanticModel.GetTypeInfo(invoExpr).Type.ToTypeSyntax();
                if (leftOpStore != null) {
                    retArgName = leftOpStore.Name.CSIdName;
                }
                else {
                    //>var __v__0 = new Variable<>();
                    retArgName = _statement.ActivityAncestor.GetImplVarName(_seqStmList, CSEX.NewVariable(retArgType));
                    //>__activity__.Variables.Add(retOutArgName);
                    _seqStmList.Add(CS.AddInvoStm(CS.IdName("__activity__"), "Variables", retArgName));
                }
            }
            ExpressionSyntax resultExpr;
            if (modelMethodKind == ModelMethodKind.ActivityVoid || modelMethodKind == ModelMethodKind.ActivityNonVoid) {
                //>expr.Initialize(__activity2__ => {
                //>  __activity2__.Arg1 = new InArgument<TYPE>(...);
                //>  __activity2__.Arg2 = new InOutArgument<TYPE>(...);
                //>  __activity2__.Result = new OutArgument<TYPE>(...);
                //>})
                var initStmList = new List<StatementSyntax>();
                for (var i = 0; i < arguments.Count; i++) {
                    var arg = arguments[i];
                    string paraName;
                    var nameColon = arg.NameColon;
                    if (nameColon != null) {
                        paraName = nameColon.Name.Identifier.Text;
                    }
                    else {
                        paraName = parameterNames[i];
                    }
                    var refOrOutKind = arg.RefOrOutKeyword.CSharpKind();
                    var argExpr = arg.Expression;
                    ExpressionSyntax genedArgExpr;
                    if (refOrOutKind == SyntaxKind.None) {
                        genedArgExpr = CSEX.NewInArgumentWithFuncActivity(_semanticModel.GetTypeInfo(argExpr).ConvertedType.ToTypeSyntax(), newArguments[i].Expression);
                    }
                    else {
                        var idName = argExpr.AsNonPareExpr<IdentifierNameSyntax>();
                        if (idName == null) {
                            CompilationContext.Throw(argExpr.GetAnySourceSpan(), ErrorKind.InvalidActivityInvokeOutOrRefArg);
                        }
                        var store = TryGetStore(idName);
                        if (store == null) {
                            CompilationContext.Throw(argExpr.GetAnySourceSpan(), ErrorKind.InvalidActivityInvokeOutOrRefArg);
                        }
                        genedArgExpr = CSEX.NewOutOrRefArgument(_semanticModel.GetTypeInfo(argExpr).ConvertedType.ToTypeSyntax(),
                            refOrOutKind == SyntaxKind.OutKeyword, store);
                    }
                    initStmList.Add(CS.AssignStm(CS.MemberAccessExpr(CS.IdName("__activity2__"), paraName), genedArgExpr));
                }
                if (retArgType != null) {
                    initStmList.Add(CS.AssignStm(CS.MemberAccessExpr(CS.IdName("__activity2__"), "Result"), CSEX.NewOutArgument(retArgType, retArgName)));
                }
                resultExpr = CS.InvoWithLambdaArgExpr(rawExpr, "Initialize", "__activity2__", initStmList);
            }
            else {
                //>expr.Initialize(__delegate__ =>
                //>    new InvokeFunc<TYPE1, TYPE2, TYPE3> {
                //>        Func = __delegate__,
                //>        Argument1 = new InArgument<TYPE1>(...),
                //>        Argument2 = new InArgument<TYPE2>(...),
                //>        Result = new OutArgument<TYPE3>(...),
                //>    }
                //>)
                var isFunc = modelMethodKind == ModelMethodKind.Func;
                var typeArray = new TypeSyntax[parameterCount + (isFunc ? 1 : 0)];
                var initExprList = new List<ExpressionSyntax>();
                for (var i = 0; i < arguments.Count; i++) {
                    int idx;
                    var arg = arguments[i];
                    var nameColon = arg.NameColon;
                    if (nameColon != null) {
                        idx = nameColon.Name.Identifier.ValueText.Substring(3).ToInt32() - 1;//eg: "arg1"
                    }
                    else idx = i;
                    var argExpr = arg.Expression;
                    var type = _semanticModel.GetTypeInfo(argExpr).ConvertedType.ToTypeSyntax();
                    typeArray[idx] = type;
                    initExprList.Add(CS.AssignExpr(CS.IdName(parameterCount > 1 ? "Argument" + (idx + 1).ToInvariantString() : "Argument"),
                        CSEX.NewInArgumentWithFuncActivity(type, newArguments[i].Expression)));
                }
                if (isFunc) {
                    typeArray[typeArray.Length - 1] = retArgType ?? _semanticModel.GetTypeInfo(invoExpr).Type.ToTypeSyntax();
                    if (retArgType != null) {
                        initExprList.Add(CS.AssignExpr(CS.IdName("Result"), CSEX.NewOutArgument(retArgType, retArgName)));
                    }
                }
                initExprList.Add(CS.AssignExpr(CS.IdName(isFunc ? "Func" : "Action"), CS.IdName("__delegate__")));
                resultExpr = CS.InvoWithLambdaArgExpr(rawExpr, "Initialize", "__delegate__",
                    CS.NewObjExpr(isFunc ? CSEX.InvokeFuncOf(typeArray) : (parameterCount == 0 ? CSEX.InvokeActionName : CSEX.InvokeActionOf(typeArray)),
                        null, initExprList));
            }
            if (_seqStmList == null) return resultExpr;
            //>__activity__.Activities.Add(resultExpr);
            _seqStmList.Add(CS.AddInvoStm(CS.IdName("__activity__"), "Activities", resultExpr));
            _seqActCount++;
            if (retArgName != null && !isStmInvo) {
                //>retOutArgName.Get(__ctx__);
                return CSEX.StoreGet(retArgName);
            }
            return null;
        }
        private SyntaxNode VisitCommonInvoke(InvocationExpressionSyntax invoExpr) {
            List<StatementSyntax> preStmList = null;
            List<StatementSyntax> postStmList = null;
            ArgumentSyntax[] argArray = null;
            var arguments = invoExpr.ArgumentList.Arguments;
            var argCount = arguments.Count;
            for (var i = 0; i < argCount; i++) {
                var arg = arguments[i];
                var refOrOutKind = arg.RefOrOutKeyword.CSharpKind();
                if (refOrOutKind == SyntaxKind.OutKeyword || refOrOutKind == SyntaxKind.RefKeyword) {
                    var argIdName = arg.Expression.AsNonPareExpr<IdentifierNameSyntax>();
                    if (argIdName != null) {
                        var store = TryGetStore(argIdName);
                        if (store != null) {
                            if (preStmList == null) {
                                preStmList = new List<StatementSyntax>();
                                postStmList = new List<StatementSyntax>();
                                argArray = new ArgumentSyntax[argCount];
                            }
                            IdentifierNameSyntax idName;
                            if (refOrOutKind == SyntaxKind.OutKeyword) {
                                idName = _statement.ActivityAncestor.GetImplVarNameNoInit(preStmList, store.Type);
                            }
                            else {
                                idName = _statement.ActivityAncestor.GetImplVarName(preStmList, CSEX.StoreGet(store));
                            }
                            postStmList.Add(CS.ExprStm(CSEX.StoreSetEx(store, idName)));
                            argArray[i] = SyntaxFactory.Argument(arg.NameColon, arg.RefOrOutKeyword, idName);
                        }
                    }
                }
            }
            if (preStmList != null) {
                for (var i = 0; i < argCount; i++) {
                    if (argArray[i] == null) {
                        argArray[i] = (ArgumentSyntax)Visit(arguments[i]);
                    }
                }
                var newInvoExpr = CS.InvoExpr((ExpressionSyntax)Visit(invoExpr.Expression), argArray);
                TypeSyntax lambdaType;
                var retTypeSymbol = _semanticModel.GetTypeInfo(invoExpr).Type;
                if (retTypeSymbol.SpecialType == SpecialType.System_Void) {
                    //>((Action)(() => { ...; }))();
                    lambdaType = CS.ActionName;
                    preStmList.Add(CS.ExprStm(newInvoExpr));
                    preStmList.AddRange(postStmList);
                }
                else {
                    //>((Func<string>)(() => { ...; return ""; }))();
                    lambdaType = CS.FuncOf(retTypeSymbol.ToTypeSyntax());
                    var retIdName = _statement.ActivityAncestor.GetImplVarName(preStmList, newInvoExpr);
                    preStmList.AddRange(postStmList);
                    preStmList.Add(CS.ReturnStm(retIdName));
                }
                return CS.InvoLambdaExpr(lambdaType, SyntaxFactory.Block(preStmList));
            }
            return base.VisitInvocationExpression(invoExpr);
        }

    }
    public struct SemanticContext {
        internal SemanticContext(SyntaxNode ancestor, SemanticModel semanticModel) {
            if (ancestor == null) throw new ArgumentNullException("ancestor");
            if (semanticModel == null) throw new ArgumentNullException("semanticModel");
            Ancestor = ancestor;
            SemanticModel = semanticModel;
        }
        internal readonly SyntaxNode Ancestor;
        internal readonly SemanticModel SemanticModel;
        internal ExpressionSyntax GetExpr(SyntaxAnnotation ann) {
            return Ancestor.TryGetAnnedNode<ExpressionSyntax>(ann);
        }
        internal SyntaxNode GetNode(SyntaxAnnotation ann) {
            return Ancestor.TryGetAnnedNode(ann);
        }
    }
    public delegate StatementSyntax ExprToStm(ExpressionSyntax expr);
    public abstract class Statement : Object {
        private Activity _activityAncestor;
        internal Activity ActivityAncestor { get { return _activityAncestor ?? (_activityAncestor = GetAncestor<Activity>()); } }
        private IStoreHost _storeHost;
        internal IStoreHost StoreHost { get { return _storeHost ?? (_storeHost = GetAncestor<IStoreHost>(false, true)); } }
        protected SimpleToken Keyword { get; private set; }//opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var keywordNode = node.Members.TryGetValue("Keyword");
            if (keywordNode != null) {
                Keyword = new SimpleToken(keywordNode);
            }
        }
        internal abstract void GetModel(List<StatementSyntax> stmList);
        internal abstract void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent);
        protected static Statement Create(Statement parent, Node node) {
            switch (node.Label) {
                case "CSharpExpression":
                case "CSharpBlock": return new CSharpStatement(parent, node);
                case "Empty": return new EmptyStatement(parent, node);
                case "Sequence": return new Sequence(parent, node);
                case "If": return new If(parent, node);
                case "While":
                case "DoWhile": return new WhileOrDoWhile(parent, node);
                case "Switch": return new Switch(parent, node);
                case "Throw": return new Throw(parent, node);
                case "Try": return new Try(parent, node);
                case "ForEach": return new ForEach(parent, node);
                case "ParallelForEach": return new ParallelForEach(parent, node);
                case "Delay": return new Delay(parent, node);
                case "Parallel": return new Parallel(parent, node);
                case "Pick": return new Pick(parent, node);
                case "StateMachine": return new StateMachine(parent, node);
                case "Flow": return new Flow(parent, node);
                case "Transacted": return new Transacted(parent, node);
                case "Cancellable": return new Cancellable(parent, node);
                case "Compensable": return new Compensable(parent, node);
                case "Confirm": return new Confirm(parent, node);
                case "Compensate": return new Compensate(parent, node);
                case "Persist": return new Persist(parent, node);
                case "NoPersist": return new NoPersist(parent, node);
                case "Terminate": return new Terminate(parent, node);
                case "Receive": return new Receive(parent, node);
                case "SendReply": return new SendReply(parent, node);
                case "Send": return new Send(parent, node);
                case "ReceiveReply": return new ReceiveReply(parent, node);
                case "ContentCorr": return new ContentCorr(parent, node);
                case "TransactedReceive": return new TransactedReceive(parent, node);

                default: throw new InvalidOperationException();
            }
        }
    }
    public sealed class CSharpStatement : Statement {
        internal CSharpStatement(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal CSharpStatement Next { get; set; }//opt
        private StatementSyntax _statement;
        private SyntaxAnnotation _ann;
        private bool _isBlock;
        protected override void Initialize(Node node) {
            base.Initialize(node);
            if (node.Label == "CSharpBlock") {
                _statement = ((BlockSyntax)node.Member("Block").ToSyntaxNode()).SetAnn(out _ann);
                _isBlock = true;
            }
            else {
                _statement = CS.ExprStm(node.Member("Expression").ToExpressionSyntax().SetAnn(out _ann));
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            stmList.Add(_statement);
            if (Next != null) Next.GetModel(stmList);
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var actExprList = new List<ExpressionSyntax>();
            List<StatementSyntax> lambdaStmList = null;
            GetImplCore(ctx, actExprList, ref lambdaStmList);
            foreach (var actExpr in actExprList) {
                stmList.Add(addToParent(actExpr));
            }
        }
        internal void GetImplAsSeqSingleMember(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var actExprList = new List<ExpressionSyntax>();
            List<StatementSyntax> lambdaStmList = null;
            GetImplCore(ctx, actExprList, ref lambdaStmList);
            if (actExprList.Count == 1) {
                stmList.Add(addToParent(actExprList[0]));
            }
            else {
                var seqName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.SequenceName));
                foreach (var actExpr in actExprList) {
                    stmList.Add(CS.AddInvoStm(seqName, "Activities", actExpr));
                }
                stmList.Add(addToParent(seqName));
            }
        }
        private void GetImplCore(SemanticContext ctx, List<ExpressionSyntax> actExprList, ref List<StatementSyntax> lambdaStmList) {
            bool hasInvoke;
            var resNode = ImplRewriter.Rewrite(ctx.GetNode(_ann), ctx.SemanticModel, this, _isBlock, out hasInvoke);
            if (hasInvoke) {
                if (lambdaStmList != null && lambdaStmList.Count > 0) {
                    CreateActivity(actExprList, lambdaStmList);
                }
                actExprList.Add((ExpressionSyntax)resNode);
                if (Next != null) {
                    Next.GetImplCore(ctx, actExprList, ref lambdaStmList);
                }
            }
            else {
                var resStm = resNode as StatementSyntax;
                if (resStm == null) {
                    resStm = CS.ExprStm((ExpressionSyntax)resNode);
                }
                Extensions.CreateAndAdd(ref lambdaStmList, resStm);
                if (Next != null) {
                    Next.GetImplCore(ctx, actExprList, ref lambdaStmList);
                }
                else {
                    CreateActivity(actExprList, lambdaStmList);
                }
            }
        }
        private void CreateActivity(List<ExpressionSyntax> actExprList, List<StatementSyntax> lambdaStmList) {
            //>new MetahWActionActivity(__ctx__ => { STM1; STM2; });
            actExprList.Add(CSEX.NewMetahWActionActivity(lambdaStmList));
            lambdaStmList.Clear();
        }
    }
    public sealed class EmptyStatement : Statement {
        internal EmptyStatement(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal override void GetModel(List<StatementSyntax> stmList) { }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            stmList.Add(addToParent(CS.NewObjExpr(CSEX.MetahWActionActivityName, CS.NullLiteral)));
        }
    }
    public abstract class StatementWithVariables : Statement, IStoreHost {
        internal readonly List<Variable> VariableList = new List<Variable>();
        private IStoreHost _ancestorStoreHost;
        private IStoreHost AncestorStoreHost { get { return _ancestorStoreHost ?? (_ancestorStoreHost = GetAncestor<IStoreHost>()); } }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            foreach (var varNode in node.Member("Variables").Items) {
                var typeNode = varNode.Member("Type");
                foreach (var declaratorNode in varNode.Member("Declarators").Items) {
                    VariableList.Add(new Variable(this, typeNode, declaratorNode.Member("Name")));
                }
            }
        }
        internal Variable AddVariable(TypeSyntax type, Identifier name) {
            var var = new Variable(this, type, name);
            VariableList.Add(var);
            return var;
        }
        Store IStoreHost.TryGetStore(string plainName) {
            foreach (var var in VariableList) if (var.Name.PlainValue == plainName) return var;
            return AncestorStoreHost.TryGetStore(plainName);
        }
        internal override sealed void GetModel(List<StatementSyntax> stmList) {
            var blkStmList = new List<StatementSyntax>();
            GetModelBlockMembers(blkStmList);
            stmList.Add(SyntaxFactory.Block(blkStmList));
        }
        protected virtual void GetModelBlockMembers(List<StatementSyntax> blkStmList) {
            foreach (var var in VariableList) {
                blkStmList.Add(var.GetModel());
            }
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var blkStmList = new List<StatementSyntax>();
            GetImplBlockMembers(ctx, blkStmList, addToParent);
            stmList.Add(SyntaxFactory.Block(blkStmList));
        }
        protected abstract void GetImplBlockMembers(SemanticContext ctx, List<StatementSyntax> blkStmList, ExprToStm addToParent);
        internal void GetVariablesImpl(List<StatementSyntax> stmList, ExprToStm addToParent) {
            foreach (var var in VariableList) {
                stmList.Add(var.GetImpl());
                stmList.Add(addToParent(var.Name.CSIdName));
            }
        }
    }
    public sealed class Sequence : StatementWithVariables {
        internal Sequence(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal Sequence(Activity parent, Node node) {
            Parent = parent;
            parent.Body = this;
            Initialize(node);
        }
        internal bool IsRoot { get { return Parent is Activity; } }
        internal Activity ActivityParent { get { return Parent as Activity; } }
        internal readonly List<Statement> MemberList = new List<Statement>();
        protected override void Initialize(Node node) {
            base.Initialize(node);
            Create(this, node.Member("Members"), MemberList);
        }
        private static void Create(Statement parent, Node listNode, List<Statement> memberList) {
            CSharpStatement lastCSStm = null;
            foreach (var memberNode in listNode.Items) {
                var stm = Create(parent, memberNode);
                var csStm = stm as CSharpStatement;
                if (csStm != null) {
                    if (lastCSStm != null) {
                        lastCSStm.Next = csStm;
                    }
                    else {
                        memberList.Add(csStm);
                    }
                    lastCSStm = csStm;
                }
                else {
                    lastCSStm = null;
                    memberList.Add(stm);
                }
            }
        }
        internal StatementSyntax GetModel() {
            var blkStmList = new List<StatementSyntax>();
            GetModelBlockMembers(blkStmList);
            return SyntaxFactory.Block(blkStmList);
        }
        protected override void GetModelBlockMembers(List<StatementSyntax> blkStmList) {
            base.GetModelBlockMembers(blkStmList);
            foreach (var member in MemberList) member.GetModel(blkStmList);
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            if (VariableList.Count == 0 && MemberList.Count == 1) {
                var csStm = MemberList[0] as CSharpStatement;
                if (csStm != null) {
                    csStm.GetImplAsSeqSingleMember(ctx, stmList, addToParent);
                }
                else {
                    MemberList[0].GetImpl(ctx, stmList, addToParent);
                }
            }
            else if (MemberList.Count > 0) {
                base.GetImpl(ctx, stmList, addToParent);
            }
            else {
                stmList.Add(addToParent(CS.NewObjExpr(CSEX.MetahWActionActivityName, CS.NullLiteral)));
            }
        }
        protected override void GetImplBlockMembers(SemanticContext ctx, List<StatementSyntax> blkStmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(blkStmList, CS.NewObjExpr(CSEX.SequenceName));
            GetVariablesImpl(blkStmList, expr => CS.AddInvoStm(varName, "Variables", expr));
            foreach (var member in MemberList) {
                member.GetImpl(ctx, blkStmList, expr => CS.AddInvoStm(varName, "Activities", expr));
            }
            blkStmList.Add(addToParent(varName));
        }
        internal void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent, int memberStartIndex) {
            var count = MemberList.Count - memberStartIndex;
            if (count == 1) {
                var csStm = MemberList[memberStartIndex] as CSharpStatement;
                if (csStm != null) {
                    csStm.GetImplAsSeqSingleMember(ctx, stmList, addToParent);
                }
                else {
                    MemberList[memberStartIndex].GetImpl(ctx, stmList, addToParent);
                }
            }
            else if (count > 0) {
                var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.SequenceName));
                for (var i = memberStartIndex; i < MemberList.Count; i++) {
                    MemberList[i].GetImpl(ctx, stmList, expr => CS.AddInvoStm(varName, "Activities", expr));
                }
                stmList.Add(addToParent(varName));
            }
            else {
                stmList.Add(addToParent(CS.NewObjExpr(CSEX.MetahWActionActivityName, CS.NullLiteral)));
            }
        }
    }
    public sealed class If : Statement {
        internal If(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private ExpressionSyntax _conditionExpr;
        private SyntaxAnnotation _conditionAnn;
        private Statement _then;
        private Statement _else; //opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _conditionExpr = node.Member("Condition").ToExpressionSyntax().SetAnn(out _conditionAnn);
            _then = Create(this, node.Member("Then"));
            var elseNode = node.Member("Else");
            if (elseNode.IsNotNull) {
                _else = Create(this, elseNode);
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            ActivityAncestor.GetModelVarDecl(stmList, CS.BoolType, _conditionExpr);
            _then.GetModel(stmList);
            if (_else != null) {
                _else.GetModel(stmList);
            }
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.IfName));
            //>varName.Condition = new InArgument<bool>(...);
            stmList.Add(CS.AssignStm(varName, "Condition", CSEX.NewInArgument(CS.BoolType,
                ImplRewriter.RewriteExpr(ctx.GetExpr(_conditionAnn), CS.BoolType, ctx.SemanticModel, this))));
            //>varName.Then = ...;
            _then.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "Then", expr));
            if (_else != null) {
                //>varName.Else = ...;
                _else.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "Else", expr));
            }
            stmList.Add(addToParent(varName));
        }
    }
    public sealed class WhileOrDoWhile : Statement {
        internal WhileOrDoWhile(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal bool IsWhile { get; private set; }
        private ExpressionSyntax _conditionExpr;
        private SyntaxAnnotation _conditionAnn;
        private Statement _body;
        protected override void Initialize(Node node) {
            base.Initialize(node);
            IsWhile = node.Label == "While";
            _conditionExpr = node.Member("Condition").ToExpressionSyntax().SetAnn(out _conditionAnn);
            _body = Create(this, node.Member("Body"));
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            ActivityAncestor.GetModelVarDecl(stmList, CS.BoolType, _conditionExpr);
            _body.GetModel(stmList);
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(IsWhile ? CSEX.WhileName : CSEX.DoWhileName));
            //>varName.Condition = new MetahWXXActivity<bool>(...);
            stmList.Add(CS.AssignStm(varName, "Condition",
                ImplRewriter.RewriteExpr(ctx.GetExpr(_conditionAnn), CS.BoolType, ctx.SemanticModel, this)));
            //>varName.Body = ...;
            _body.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "Body", expr));
            stmList.Add(addToParent(varName));
        }
    }
    #region Switch
    public sealed class Switch : Statement {
        internal Switch(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private ExpressionSyntax _valueExpr;
        private SyntaxAnnotation _valueAnn;
        private readonly List<SwitchCase> _caseList = new List<SwitchCase>();
        private SwitchDefault _default; //opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _valueExpr = node.Member("Value").ToExpressionSyntax().SetAnn(out _valueAnn);
            foreach (var caseNode in node.Member("Cases").Items) {
                _caseList.Add(new SwitchCase(this, caseNode));
            }
            var defaultNode = node.Member("Default");
            if (defaultNode.IsNotNull) {
                _default = new SwitchDefault(this, defaultNode);
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            var valueName = ActivityAncestor.GetModelVarName(stmList, null, _valueExpr);
            foreach (var @case in _caseList) {
                @case.GetModel(stmList, valueName);
            }
            if (_default != null && _default.Body != null) {
                _default.Body.GetModel(stmList);
            }
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var valueExpr = ctx.GetExpr(_valueAnn);
            var valueType = ctx.SemanticModel.GetTypeInfo(valueExpr).Type.ToTypeSyntax();
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.SwitchOf(valueType)));
            //>varName.Expression = new InArgument<TYPE>(...);
            stmList.Add(CS.AssignStm(varName, "Expression",
                CSEX.NewInArgument(valueType, ImplRewriter.RewriteExpr(valueExpr, valueType, ctx.SemanticModel, this))));
            foreach (var @case in _caseList) {
                @case.GetImpl(ctx, stmList, varName);
            }
            if (_default != null && _default.Body != null) {
                _default.Body.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "Default", expr));
            }
            stmList.Add(addToParent(varName));
        }
    }
    public abstract class SwitchMember : Statement {
        internal Statement Body { get; private set; }//opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var bodyNode = node.Member("Body");
            if (bodyNode.IsNotNull) Body = Create(this, bodyNode);
        }
        internal override sealed void GetModel(List<StatementSyntax> stmList) { throw new NotImplementedException(); }
        internal override sealed void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) { throw new NotImplementedException(); }
    }
    public sealed class SwitchDefault : SwitchMember {
        internal SwitchDefault(Switch parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
    }
    public sealed class SwitchCase : SwitchMember {
        internal SwitchCase(Switch parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private ExpressionSyntax _valueExpr;
        private SyntaxAnnotation _valueAnn;
        //private SimpleToken _valueToken;
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var valueNode = node.Member("Value");
            _valueExpr = valueNode.ToExpressionSyntax().SetAnn(out _valueAnn);
            //_valueToken = new SimpleToken(valueNode);
        }
        internal void GetModel(List<StatementSyntax> stmList, IdentifierNameSyntax switchValueName) {
            //>switchValueName = value;
            stmList.Add(CS.AssignStm(switchValueName, _valueExpr));
            if (Body != null) Body.GetModel(stmList);
        }
        internal void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, IdentifierNameSyntax switchVarName) {
            //>switchName.Cases.Add(value, activity);
            var genedValueExpr = ImplRewriter.CheckCSExprOnly(ctx.GetExpr(_valueAnn), ctx.SemanticModel, this);
            if (Body != null) {
                Body.GetImpl(ctx, stmList, expr => CS.AddInvoStm(switchVarName, "Cases", genedValueExpr, expr));
            }
            else {
                stmList.Add(CS.AddInvoStm(switchVarName, "Cases", genedValueExpr, CS.NullLiteral));
            }
        }
    }
    #endregion Switch
    public sealed class Throw : Statement {
        internal Throw(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private ExpressionSyntax _exceptionExpr;//opt
        private SyntaxAnnotation _exceptionAnn;//opt
        internal bool HasException { get { return _exceptionExpr != null; } }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var exceptionNode = node.Member("Exception");
            if (exceptionNode.IsNotNull) {
                _exceptionExpr = exceptionNode.ToExpressionSyntax().SetAnn(out _exceptionAnn);
            }
            else {
                if (GetAncestor<Catch>(true) == null) {
                    CompilationContext.Throw(Keyword, ErrorKind.RethrowMustBeInCatch);
                }
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            if (_exceptionExpr != null) {
                ActivityAncestor.GetModelVarDecl(stmList, CS.ExceptionName, _exceptionExpr);
            }
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(HasException ? CSEX.ThrowName : CSEX.RethrowName));
            if (HasException) {
                //>varName.Exception = new InArgument<Exception>(...);
                stmList.Add(CS.AssignStm(varName, "Exception", CSEX.NewInArgument(CS.ExceptionName,
                    ImplRewriter.RewriteExpr(ctx.GetExpr(_exceptionAnn), CS.ExceptionName, ctx.SemanticModel, this))));
            }
            stmList.Add(addToParent(varName));
        }
    }
    #region Try
    public sealed class Try : Statement {
        internal Try(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private Sequence _body;
        private readonly List<Catch> _catchList = new List<Catch>();
        private Sequence _finally; //opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _body = new Sequence(this, node.Member("Body"));
            foreach (var catchNode in node.Member("Catches").Items) {
                _catchList.Add(new Catch(this, catchNode));
            }
            var finallyNode = node.Member("Finally");
            if (finallyNode.IsNotNull) {
                _finally = new Sequence(this, finallyNode.Member("Body"));
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            _body.GetModel(stmList);
            foreach (var @catch in _catchList) {
                @catch.GetModel(stmList);
            }
            if (_finally != null) {
                _finally.GetModel(stmList);
            }
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.TryCatchName));
            _body.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "Try", expr));
            foreach (var @catch in _catchList) {
                @catch.GetImpl(ctx, stmList, varName);
            }
            if (_finally != null) {
                _finally.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "Finally", expr));
            }
            stmList.Add(addToParent(varName));
        }
    }
    public sealed class Catch : StatementWithConstVariable {
        internal Catch(Try parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private Sequence _body;
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _body = new Sequence(this, node.Member("Body"));
        }
        protected override void GetModelBlockMembers(List<StatementSyntax> blkStmList) {
            base.GetModelBlockMembers(blkStmList);
            ActivityAncestor.GetModelVarDecl(blkStmList, CS.ExceptionName, Variable.Name.CSIdName);
            _body.GetModel(blkStmList);
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) { throw new NotImplementedException(); }
        internal void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, IdentifierNameSyntax tryCatchImplVarName) {
            var type = Variable.Type;
            var wrapperVarName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.MetahWWrapperActivityOf(type)));
            Variable.RewrittenExpr = CS.MemberAccessExpr(wrapperVarName, "Argument");
            _body.GetImpl(ctx, stmList, expr => CS.AssignStm(wrapperVarName, "Child", expr));
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.CatchOf(type)));
            //>varName.Action = wrapperVarName.ToAction();
            stmList.Add(CS.AssignStm(varName, "Action", CS.InvoExpr(CS.MemberAccessExpr(wrapperVarName, "ToAction"))));
            //>tryCatchImplVarName.Catches.Add(varName);
            stmList.Add(CS.AddInvoStm(tryCatchImplVarName, "Catches", varName));
        }
    }
    #endregion Try
    public abstract class StatementWithConstVariable : Statement, IStoreHost {
        protected Variable Variable { get; private set; }
        private IStoreHost _ancestorVariableHost;
        private IStoreHost AncestorVariableHost { get { return _ancestorVariableHost ?? (_ancestorVariableHost = GetAncestor<IStoreHost>()); } }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            Variable = new Variable(this, node.Member("Type"), node.Member("Name"));
        }
        Store IStoreHost.TryGetStore(string plainName) {
            if (Variable != null && Variable.Name.PlainValue == plainName) return Variable;
            return AncestorVariableHost.TryGetStore(plainName);
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            var blkStmList = new List<StatementSyntax>();
            GetModelBlockMembers(blkStmList);
            stmList.Add(SyntaxFactory.Block(blkStmList));
        }
        protected virtual void GetModelBlockMembers(List<StatementSyntax> stmList) {
            //>const TYPE NAME = default(TYPE);
            stmList.Add(CS.LocalConstDeclStm(Variable.Type, Variable.Name.CSToken, SyntaxFactory.DefaultExpression(Variable.Type)));
        }
    }
    public abstract class ForEachBase : StatementWithConstVariable {
        private ExpressionSyntax _valuesExpr;
        private SyntaxAnnotation _valuesAnn;
        private Statement _body;
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _valuesExpr = node.Member("Values").ToExpressionSyntax().SetAnn(out _valuesAnn);
            _body = Create(this, node.Member("Body"));
        }
        protected override void GetModelBlockMembers(List<StatementSyntax> blkStmList) {
            ActivityAncestor.GetModelVarDecl(blkStmList, CS.IEnumerableOf(Variable.Type), _valuesExpr);
            base.GetModelBlockMembers(blkStmList);
            _body.GetModel(blkStmList);
        }
        protected void GetImplCore(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent,
            bool isForEach, ExpressionSyntax conditionExpr) {
            var itemType = Variable.Type;
            var wrapperVarName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.MetahWWrapperActivityOf(itemType)));
            Variable.RewrittenExpr = CS.MemberAccessExpr(wrapperVarName, "Argument");
            _body.GetImpl(ctx, stmList, expr => CS.AssignStm(wrapperVarName, "Child", expr));
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(isForEach ? CSEX.ForEachOf(itemType) : CSEX.ParallelForEachOf(itemType)));
            //>varName.Body = wrapperVarName.ToAction();
            stmList.Add(CS.AssignStm(varName, "Body", CS.InvoExpr(CS.MemberAccessExpr(wrapperVarName, "ToAction"))));
            //>varName.Values = new InArgument<IEnumerable<TYPE>>(...);
            stmList.Add(CS.AssignStm(varName, "Values", CSEX.NewInArgument(CS.IEnumerableOf(itemType),
                ImplRewriter.RewriteExpr(ctx.GetExpr(_valuesAnn), CS.IEnumerableOf(itemType), ctx.SemanticModel, this))));
            if (conditionExpr != null) {
                //>varName.CompletionCondition = new MetahWXXActivity<bool>(...);
                stmList.Add(CS.AssignStm(varName, "CompletionCondition", conditionExpr));
            }
            stmList.Add(addToParent(varName));
        }
    }
    public sealed class ForEach : ForEachBase {
        internal ForEach(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            GetImplCore(ctx, stmList, addToParent, true, null);
        }
    }
    public sealed class ParallelForEach : ForEachBase {
        internal ParallelForEach(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private ExpressionSyntax _conditionExpr;
        private SyntaxAnnotation _conditionAnn;
        //internal bool HasCondition { get { return _conditionExpr != null; } }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var conditionNode = node.Member("Condition");
            if (conditionNode.IsNotNull) {
                _conditionExpr = conditionNode.ToExpressionSyntax().SetAnn(out _conditionAnn);
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            base.GetModel(stmList);
            if (_conditionExpr != null) {
                ActivityAncestor.GetModelVarDecl(stmList, CS.BoolType, _conditionExpr);
            }
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            GetImplCore(ctx, stmList, addToParent, false, _conditionExpr == null ? null :
                ImplRewriter.RewriteExpr(ctx.GetExpr(_conditionAnn), CS.BoolType, ctx.SemanticModel, this));
        }
    }

    public sealed class Delay : Statement {
        internal Delay(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private ExpressionSyntax _durationExpr;
        private SyntaxAnnotation _durationAnn;
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _durationExpr = node.Member("Duration").ToExpressionSyntax().SetAnn(out _durationAnn);
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            ActivityAncestor.GetModelVarDecl(stmList, CS.TimeSpanName, _durationExpr);
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.DelayName));
            //>varName.Duration = new InArgument<TimeSpan>(...);
            stmList.Add(CS.AssignStm(varName, "Duration", CSEX.NewInArgument(CS.TimeSpanName,
                ImplRewriter.RewriteExpr(ctx.GetExpr(_durationAnn), CS.TimeSpanName, ctx.SemanticModel, this))));
            stmList.Add(addToParent(varName));
        }
    }

    public sealed class Parallel : StatementWithVariables {
        internal Parallel(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private readonly List<Statement> _memberList = new List<Statement>();
        private ExpressionSyntax _conditionExpr;//opt
        private SyntaxAnnotation _conditionAnn;//opt
        //internal bool HasCondition { get { return _conditionExpr != null; } }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            foreach (var memberNode in node.Member("Members").Items) {
                _memberList.Add(Create(this, memberNode));
            }
            var conditionNode = node.Member("Condition");
            if (conditionNode.IsNotNull) {
                _conditionExpr = conditionNode.ToExpressionSyntax().SetAnn(out _conditionAnn);
            }
        }
        protected override void GetModelBlockMembers(List<StatementSyntax> blkStmList) {
            base.GetModelBlockMembers(blkStmList);
            foreach (var member in _memberList) {
                member.GetModel(blkStmList);
            }
            if (_conditionExpr != null) {
                ActivityAncestor.GetModelVarDecl(blkStmList, CS.BoolType, _conditionExpr);
            }
        }
        protected override void GetImplBlockMembers(SemanticContext ctx, List<StatementSyntax> blkStmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(blkStmList, CS.NewObjExpr(CSEX.ParallelName));
            GetVariablesImpl(blkStmList, expr => CS.AddInvoStm(varName, "Variables", expr));
            foreach (var member in _memberList) {
                member.GetImpl(ctx, blkStmList, expr => CS.AddInvoStm(varName, "Branches", expr));
            }
            if (_conditionExpr != null) {
                //>varName.CompletionCondition = new MetahWXXActivity<bool>(...);
                blkStmList.Add(CS.AssignStm(varName, "CompletionCondition",
                    ImplRewriter.RewriteExpr(ctx.GetExpr(_conditionAnn), CS.BoolType, ctx.SemanticModel, this)));
            }
            blkStmList.Add(addToParent(varName));
        }
    }
    public sealed class Pick : Statement {
        internal Pick(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private readonly List<PickBranch> _branchList = new List<PickBranch>();
        protected override void Initialize(Node node) {
            base.Initialize(node);
            foreach (var branchNode in node.Member("Branches").Items) {
                _branchList.Add(new PickBranch(this, branchNode));
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            foreach (var branch in _branchList) {
                branch.GetModel(stmList);
            }
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.PickName));
            foreach (var branch in _branchList) {
                branch.GetImpl(ctx, stmList, expr => CS.AddInvoStm(varName, "Branches", expr));
            }
            stmList.Add(addToParent(varName));
        }
    }
    public sealed class PickBranch : StatementWithVariables {
        internal PickBranch(Pick parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private Statement _trigger;
        private Statement _action; //opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _trigger = Create(this, node.Member("Trigger"));
            var actionNode = node.Member("Action");
            if (actionNode.IsNotNull) {
                _action = Create(this, actionNode);
            }
        }
        protected override void GetModelBlockMembers(List<StatementSyntax> blkStmList) {
            base.GetModelBlockMembers(blkStmList);
            _trigger.GetModel(blkStmList);
            if (_action != null) {
                _action.GetModel(blkStmList);
            }
        }
        protected override void GetImplBlockMembers(SemanticContext ctx, List<StatementSyntax> blkStmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(blkStmList, CS.NewObjExpr(CSEX.PickBranchName));
            GetVariablesImpl(blkStmList, expr => CS.AddInvoStm(varName, "Variables", expr));
            _trigger.GetImpl(ctx, blkStmList, expr => CS.AssignStm(varName, "Trigger", expr));
            if (_action != null) {
                _action.GetImpl(ctx, blkStmList, expr => CS.AssignStm(varName, "Action", expr));
            }
            blkStmList.Add(addToParent(varName));
        }
    }
    #region StateMachine
    public sealed class StateMachine : StatementWithVariables {
        internal StateMachine(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private readonly List<StateMachineNode> _nodeList = new List<StateMachineNode>();
        internal StateMachineNode TryGetNode(Identifier name) {
            foreach (var node in _nodeList) if (node.Name == name) return node;
            return null;
        }
        private Identifier _startNodeName; //opt
        private StateMachineNode _startNode;
        protected override void Initialize(Node pnode) {
            base.Initialize(pnode);
            foreach (var nodeNode in pnode.Member("Nodes").Items) {
                StateMachineNode node;
                switch (nodeNode.Label) {
                    case "CommonNode": node = new StateMachineCommonNode(this, nodeNode); break;
                    case "FinalNode": node = new StateMachineFinalNode(this, nodeNode); break;
                    default: throw new InvalidOperationException();
                }
                _nodeList.Add(node);
            }
            _startNodeName = pnode.Member("StartNodeName").ToIdentifierOpt();
            if (_startNodeName != null) {
                _startNode = TryGetNode(_startNodeName);
                if (_startNode == null) {
                    CompilationContext.Throw(_startNodeName, ErrorKind.InvalidStateMachineNodeReference, _startNodeName);
                }
                if (_startNode.IsFinal) {
                    CompilationContext.Throw(_startNodeName, ErrorKind.StartNodeCannotBeFinal, _startNodeName);
                }
            }
            else {
                foreach (var node in _nodeList) {
                    if (!node.IsFinal) {
                        _startNode = node;
                        break;
                    }
                }
                if (_startNode == null) {
                    CompilationContext.Throw(Keyword, ErrorKind.StateMachineMustHaveOneCommonNode);
                }
            }
            if (!_nodeList.Any(i => i.IsFinal)) {
                CompilationContext.Throw(Keyword, ErrorKind.StateMachineMustHaveOneFinalNode);
            }
            foreach (var node in _nodeList) {
                node.ResolveNodeReferences();
            }
        }
        protected override void GetModelBlockMembers(List<StatementSyntax> blkStmList) {
            base.GetModelBlockMembers(blkStmList);
            foreach (var node in _nodeList) {
                node.GetModel(blkStmList);
            }
        }
        protected override void GetImplBlockMembers(SemanticContext ctx, List<StatementSyntax> blkStmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(blkStmList, CS.NewObjExpr(CSEX.StateMachineName));
            GetVariablesImpl(blkStmList, expr => CS.AddInvoStm(varName, "Variables", expr));
            foreach (var node in _nodeList) {
                node.PreGetImpl(blkStmList);
            }
            foreach (var node in _nodeList) {
                node.GetImpl(ctx, blkStmList, expr => CS.AddInvoStm(varName, "States", expr));
            }
            //>varName.InitialState = startName;
            blkStmList.Add(CS.AssignStm(varName, "InitialState", _startNode.ImplVarName));
            foreach (var node in _nodeList) {
                node.PostGetImpl(blkStmList);
            }
            blkStmList.Add(addToParent(varName));
        }
    }
    public abstract class StateMachineNode : StatementWithVariables {
        new public StateMachine Parent { get { return (StateMachine)base.Parent; } protected set { base.Parent = value; } }
        internal Identifier Name { get; private set; }
        public bool IsFinal { get; protected set; }
        private Statement _entry; //opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            Name = new Identifier(node.Member("Name"));
            if (Parent.TryGetNode(Name) != null) {
                CompilationContext.Throw(Name, ErrorKind.DuplicateStateMachineNodeName, Name);
            }
            var entryNode = node.Member("Entry");
            if (entryNode.IsNotNull) {
                _entry = Create(this, entryNode);
            }
        }
        internal virtual void ResolveNodeReferences() { }
        protected override void GetModelBlockMembers(List<StatementSyntax> blkStmList) {
            base.GetModelBlockMembers(blkStmList);
            if (_entry != null) {
                _entry.GetModel(blkStmList);
            }
        }
        internal IdentifierNameSyntax ImplVarName { get; private set; }
        internal virtual void PreGetImpl(List<StatementSyntax> smStmList) {
            ImplVarName = ActivityAncestor.GetImplVarName(smStmList, CS.NewObjExpr(CSEX.StateName));
        }
        protected override void GetImplBlockMembers(SemanticContext ctx, List<StatementSyntax> blkStmList, ExprToStm addToParent) {
            var varName = ImplVarName;
            blkStmList.Add(addToParent(varName));
            GetVariablesImpl(blkStmList, expr => CS.AddInvoStm(varName, "Variables", expr));
            if (IsFinal) {
                //>varName.IsFinal = true;
                blkStmList.Add(CS.AssignStm(varName, "IsFinal", CS.TrueLiteral));
            }
            if (_entry != null) {
                _entry.GetImpl(ctx, blkStmList, expr => CS.AssignStm(varName, "Entry", expr));
            }
        }
        internal virtual void PostGetImpl(List<StatementSyntax> smStmList) { }
    }
    public sealed class StateMachineFinalNode : StateMachineNode {
        internal StateMachineFinalNode(StateMachine parent, Node node) {
            Parent = parent;
            IsFinal = true;
            Initialize(node);
        }
    }
    public sealed class StateMachineCommonNode : StateMachineNode {
        internal StateMachineCommonNode(StateMachine parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private Statement _exit; //opt
        private readonly List<StateMachineTransition> _transitionList = new List<StateMachineTransition>();
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var exitNode = node.Member("Exit");
            if (exitNode.IsNotNull) _exit = Create(this, exitNode);
            foreach (var tranNode in node.Member("Transitions").Items) {
                _transitionList.Add(new StateMachineTransition(this, tranNode));
            }
            if (_transitionList.Count > 1 && _transitionList.Count(t => !t.HasTrigger) > 1) {
                foreach (var tran in _transitionList) {
                    if (!tran.HasTrigger && !tran.BodyList[0].HasCondition) {
                        CompilationContext.Error(tran.BodyList[0].GotoNodeName, ErrorKind.TransitionConditionRequired);
                    }
                }
                CompilationContext.ThrowIfHasErrors();
            }
        }
        internal override void ResolveNodeReferences() {
            foreach (var tran in _transitionList) {
                tran.ResolveNodeReferences();
            }
        }
        protected override void GetModelBlockMembers(List<StatementSyntax> blkStmList) {
            base.GetModelBlockMembers(blkStmList);
            if (_exit != null) {
                _exit.GetModel(blkStmList);
            }
            foreach (var tran in _transitionList) {
                tran.GetModel(blkStmList);
            }
        }
        internal override void PreGetImpl(List<StatementSyntax> smStmList) {
            base.PreGetImpl(smStmList);
            foreach (var tran in _transitionList) {
                tran.PreGetImpl(smStmList);
            }
        }
        protected override void GetImplBlockMembers(SemanticContext ctx, List<StatementSyntax> blkStmList, ExprToStm addToParent) {
            base.GetImplBlockMembers(ctx, blkStmList, addToParent);
            var varName = ImplVarName;
            if (_exit != null) {
                _exit.GetImpl(ctx, blkStmList, expr => CS.AssignStm(varName, "Exit", expr));
            }
            foreach (var tran in _transitionList) {
                tran.GetImpl(ctx, blkStmList, expr => CS.AddInvoStm(varName, "Transitions", expr));
            }
        }
        internal override void PostGetImpl(List<StatementSyntax> smStmList) {
            foreach (var tran in _transitionList) {
                tran.PostGetImpl(smStmList);
            }
        }
    }
    public sealed class StateMachineTransition : Statement {
        internal StateMachineTransition(StateMachineCommonNode parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        new public StateMachineCommonNode Parent { get { return (StateMachineCommonNode)base.Parent; } private set { base.Parent = value; } }
        private Statement _trigger; //opt
        internal bool HasTrigger { get { return _trigger != null; } }
        internal readonly List<StateMachineTransitionBody> BodyList = new List<StateMachineTransitionBody>();
        protected override void Initialize(Node node) {
            base.Initialize(node);
            if (node.Label == "TransitionWithTrigger") {
                _trigger = Create(this, node.Member("Trigger"));
                var singleBodyNode = node.Member("Body");
                if (singleBodyNode.IsNotNull) {
                    BodyList.Add(new StateMachineTransitionBody(this, singleBodyNode));
                }
                else {
                    foreach (var bodyNode in node.Member("Bodies").Items) {
                        BodyList.Add(new StateMachineTransitionBody(this, bodyNode));
                    }
                }
                if (BodyList.Count > 1) {
                    foreach (var body in BodyList) {
                        if (!body.HasCondition) {
                            CompilationContext.Error(body.GotoNodeName, ErrorKind.TransitionConditionRequired);
                        }
                    }
                    CompilationContext.ThrowIfHasErrors();
                }
            }
            else {
                BodyList.Add(new StateMachineTransitionBody(this, node));
            }
        }
        internal void ResolveNodeReferences() {
            foreach (var body in BodyList) {
                body.ResolveNodeReferences();
            }
        }
        internal override sealed void GetModel(List<StatementSyntax> stmList) {
            if (_trigger != null) {
                _trigger.GetModel(stmList);
            }
            foreach (var body in BodyList) {
                body.GetModel(stmList);
            }
        }
        internal void PreGetImpl(List<StatementSyntax> smStmList) {
            foreach (var body in BodyList) {
                body.ImplVarName = ActivityAncestor.GetImplVarName(smStmList, CS.NewObjExpr(CSEX.TransitionName));
            }
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            for (var i = 0; i < BodyList.Count; i++) {
                var body = BodyList[i];
                var varName = body.ImplVarName;
                if (i == 0 && _trigger != null) {
                    _trigger.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "Trigger", expr));
                }
                else if (i > 0) {
                    //>varName.Trigger = <member 0>.Trigger;
                    stmList.Add(CS.AssignStm(varName, "Trigger", CS.MemberAccessExpr(BodyList[0].ImplVarName, "Trigger")));
                }
                body.GetImpl(ctx, stmList, addToParent);
                stmList.Add(addToParent(varName));
            }
        }
        internal void PostGetImpl(List<StatementSyntax> smStmList) {
            foreach (var body in BodyList) {
                body.PostGetImpl(smStmList);
            }
        }
    }
    public sealed class StateMachineTransitionBody : Statement {
        internal StateMachineTransitionBody(StateMachineTransition parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        new public StateMachineTransition Parent { get { return (StateMachineTransition)base.Parent; } private set { base.Parent = value; } }
        internal bool HasCondition { get { return _conditionExpr != null; } }
        private ExpressionSyntax _conditionExpr;//opt for common
        private SyntaxAnnotation _conditionAnn;//opt for common
        private Statement _action; //opt
        internal Identifier GotoNodeName { get; private set; }
        private StateMachineNode _gotoNode;
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var conditionNode = node.Member("Condition");
            if (conditionNode.IsNotNull) {
                _conditionExpr = conditionNode.ToExpressionSyntax().SetAnn(out _conditionAnn);
            }
            var actionNode = node.Member("Action");
            if (actionNode.IsNotNull) {
                _action = Create(this, actionNode);
            }
            GotoNodeName = new Identifier(node.Member("NodeName"));
        }
        internal void ResolveNodeReferences() {
            _gotoNode = Parent.Parent.Parent.TryGetNode(GotoNodeName);
            if (_gotoNode == null) {
                CompilationContext.Throw(GotoNodeName, ErrorKind.InvalidStateMachineNodeReference, GotoNodeName);
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            if (_conditionExpr != null) {
                ActivityAncestor.GetModelVarDecl(stmList, CS.BoolType, _conditionExpr);
            }
            if (_action != null) {
                _action.GetModel(stmList);
            }
        }
        internal IdentifierNameSyntax ImplVarName { get; set; }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            if (_conditionExpr != null) {
                //>varName.Condition = new MetahWXXActivity<bool>(...);
                stmList.Add(CS.AssignStm(ImplVarName, "Condition",
                    ImplRewriter.RewriteExpr(ctx.GetExpr(_conditionAnn), CS.BoolType, ctx.SemanticModel, this)));
            }
            if (_action != null) {
                _action.GetImpl(ctx, stmList, expr => CS.AssignStm(ImplVarName, "Action", expr));
            }
        }
        internal void PostGetImpl(List<StatementSyntax> smStmList) {
            //>varName.To = ...;
            smStmList.Add(CS.AssignStm(ImplVarName, "To", _gotoNode.ImplVarName));
        }
    }
    #endregion StateMachine
    #region Flow
    public sealed class Flow : StatementWithVariables {
        internal Flow(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private readonly List<FlowNode> _nodeList = new List<FlowNode>();
        internal FlowNode TryGetNode(Identifier name) {
            foreach (var node in _nodeList) if (node.Name == name) return node;
            return null;
        }
        private Identifier _startNodeName; //opt
        private FlowNode _startNode;
        protected override void Initialize(Node pnode) {
            base.Initialize(pnode);
            foreach (var nodeNode in pnode.Member("Nodes").Items) {
                FlowNode node;
                switch (nodeNode.Label) {
                    case "FlowStep": node = new FlowStep(this, nodeNode); break;
                    case "FlowIf": node = new FlowIf(this, nodeNode); break;
                    case "FlowSwitch": node = new FlowSwitch(this, nodeNode); break;
                    default: throw new InvalidOperationException();
                }
                _nodeList.Add(node);
            }
            _startNodeName = pnode.Member("StartNodeName").ToIdentifierOpt();
            if (_startNodeName != null) {
                _startNode = TryGetNode(_startNodeName);
                if (_startNode == null) {
                    CompilationContext.Throw(_startNodeName, ErrorKind.InvalidFlowNodeReference, _startNodeName);
                }
            }
            else {
                _startNode = _nodeList[0];
            }
            foreach (var node in _nodeList) {
                node.ResolveNodeReferences();
            }
        }
        protected override void GetModelBlockMembers(List<StatementSyntax> blkStmList) {
            base.GetModelBlockMembers(blkStmList);
            foreach (var node in _nodeList) {
                node.GetModel(blkStmList);
            }
        }
        protected override void GetImplBlockMembers(SemanticContext ctx, List<StatementSyntax> blkStmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(blkStmList, CS.NewObjExpr(CSEX.FlowchartName));
            GetVariablesImpl(blkStmList, expr => CS.AddInvoStm(varName, "Variables", expr));
            foreach (var node in _nodeList) {
                node.GetImpl(ctx, blkStmList, expr => CS.AddInvoStm(varName, "Nodes", expr));
            }
            //>varName.StartNode = startName;
            blkStmList.Add(CS.AssignStm(varName, "StartNode", _startNode.ImplVarName));
            foreach (var node in _nodeList) {
                node.PostGetImpl(ctx, blkStmList);
            }
            blkStmList.Add(addToParent(varName));
        }
    }
    public abstract class FlowNode : Statement {
        new public Flow Parent { get { return (Flow)base.Parent; } protected set { base.Parent = value; } }
        internal Identifier Name { get; private set; }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            Name = new Identifier(node.Member("Name"));
            if (Parent.TryGetNode(Name) != null) {
                CompilationContext.Throw(Name, ErrorKind.DuplicateFlowNodeName, Name);
            }
        }
        internal abstract void ResolveNodeReferences();
        internal static Identifier CreateNodeName(Node jumpNode) {
            if (jumpNode.Label == "FlowGoto") return new Identifier(jumpNode.Member("Name"));
            return null;
        }
        internal FlowNode GetNode(Identifier name) {
            if (name != null) {
                var node = Parent.TryGetNode(name);
                if (node == null) {
                    CompilationContext.Throw(name, ErrorKind.InvalidFlowNodeReference, name);
                }
                return node;
            }
            return null;
        }
        internal IdentifierNameSyntax ImplVarName { get; private set; }
        protected IdentifierNameSyntax GetImplVarName(List<StatementSyntax> stmList, ExpressionSyntax initExpr) {
            ImplVarName = ActivityAncestor.GetImplVarName(stmList, initExpr);
            return ImplVarName;
        }
        internal abstract void PostGetImpl(SemanticContext ctx, List<StatementSyntax> stmList);
    }
    public sealed class FlowStep : FlowNode {
        internal FlowStep(Flow parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private Statement _action;
        private Identifier _gotoNodeName; //opt
        private FlowNode _gotoNode; //opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _action = Create(this, node.Member("Action"));
            _gotoNodeName = CreateNodeName(node.Member("Jump"));
        }
        internal override void ResolveNodeReferences() {
            _gotoNode = GetNode(_gotoNodeName);
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            _action.GetModel(stmList);
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = GetImplVarName(stmList, CS.NewObjExpr(CSEX.FlowStepName));
            _action.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "Action", expr));
            stmList.Add(addToParent(varName));
        }
        internal override void PostGetImpl(SemanticContext ctx, List<StatementSyntax> stmList) {
            if (_gotoNode != null) {
                //>varName.Next = ..;
                stmList.Add(CS.AssignStm(ImplVarName, "Next", _gotoNode.ImplVarName));
            }
        }
    }
    public sealed class FlowIf : FlowNode {
        internal FlowIf(Flow parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private ExpressionSyntax _conditionExpr;
        private SyntaxAnnotation _conditionAnn;
        private Identifier _thenGotoNodeName; //opt
        private FlowNode _thenGotoNode; //opt
        private Identifier _elseGotoNodeName; //opt
        private FlowNode _elseGotoNode; //opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _conditionExpr = node.Member("Condition").ToExpressionSyntax().SetAnn(out _conditionAnn);
            _thenGotoNodeName = CreateNodeName(node.Member("ThenJump"));
            var elseNode = node.Member("ElseJump");
            if (elseNode.IsNotNull) {
                _elseGotoNodeName = CreateNodeName(elseNode);
            }
        }
        internal override void ResolveNodeReferences() {
            _thenGotoNode = GetNode(_thenGotoNodeName);
            _elseGotoNode = GetNode(_elseGotoNodeName);
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            ActivityAncestor.GetModelVarDecl(stmList, CS.BoolType, _conditionExpr);
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = GetImplVarName(stmList, CS.NewObjExpr(CSEX.FlowDecisionName));
            //>varName.Condition = new MetahWXXActivity<bool>(...);
            stmList.Add(CS.AssignStm(varName, "Condition",
                ImplRewriter.RewriteExpr(ctx.GetExpr(_conditionAnn), CS.BoolType, ctx.SemanticModel, this)));
            stmList.Add(addToParent(varName));
        }
        internal override void PostGetImpl(SemanticContext ctx, List<StatementSyntax> stmList) {
            if (_thenGotoNode != null) {
                //>varName.True = ..;
                stmList.Add(CS.AssignStm(ImplVarName, "True", _thenGotoNode.ImplVarName));
            }
            if (_elseGotoNode != null) {
                //>varName.False = ..;
                stmList.Add(CS.AssignStm(ImplVarName, "False", _elseGotoNode.ImplVarName));
            }
        }
    }
    public sealed class FlowSwitch : FlowNode {
        internal FlowSwitch(Flow parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private ExpressionSyntax _valueExpr;
        private SyntaxAnnotation _valueAnn;
        private readonly List<FlowSwitchCase> _caseList = new List<FlowSwitchCase>();
        private FlowSwitchDefault _default; //opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _valueExpr = node.Member("Value").ToExpressionSyntax().SetAnn(out _valueAnn);
            foreach (var caseNode in node.Member("Cases").Items) {
                _caseList.Add(new FlowSwitchCase(this, caseNode));
            }
            var defaultNode = node.Member("Default");
            if (defaultNode.IsNotNull) {
                _default = new FlowSwitchDefault(this, defaultNode);
            }
        }
        internal override void ResolveNodeReferences() {
            foreach (var @case in _caseList) {
                @case.ResolveNodeReferences();
            }
            if (_default != null) {
                _default.ResolveNodeReferences();
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            var valueName = ActivityAncestor.GetModelVarName(stmList, null, _valueExpr);
            foreach (var @case in _caseList) {
                @case.GetModel(stmList, valueName);
            }
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var valueExpr = ctx.GetExpr(_valueAnn);
            var valueType = ctx.SemanticModel.GetTypeInfo(valueExpr).Type.ToTypeSyntax();
            var varName = GetImplVarName(stmList, CS.NewObjExpr(CSEX.FlowSwitchOf(valueType)));
            //>varName.Expression = new MetahWXXActivity<TYPE>(...);
            stmList.Add(CS.AssignStm(varName, "Expression",
                ImplRewriter.RewriteExpr(valueExpr, valueType, ctx.SemanticModel, this)));
            stmList.Add(addToParent(varName));
        }
        internal override void PostGetImpl(SemanticContext ctx, List<StatementSyntax> stmList) {
            foreach (var @case in _caseList) {
                @case.GetImpl(ctx, stmList, ImplVarName);
            }
            if (_default != null && _default.GotoNode != null) {
                //>varName.Default = ...;
                stmList.Add(CS.AssignStm(ImplVarName, "Default", _default.GotoNode.ImplVarName));
            }
        }
    }
    public abstract class FlowSwitchMember : Statement {
        new public FlowSwitch Parent { get { return (FlowSwitch)base.Parent; } protected set { base.Parent = value; } }
        private Identifier _gotoNodeName; //opt
        internal FlowNode GotoNode { get; private set; }//opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _gotoNodeName = FlowNode.CreateNodeName(node.Member("Jump"));
        }
        internal void ResolveNodeReferences() {
            GotoNode = Parent.GetNode(_gotoNodeName);
        }
        internal override sealed void GetModel(List<StatementSyntax> stmList) { throw new NotImplementedException(); }
        internal override sealed void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) { throw new NotImplementedException(); }
    }
    public sealed class FlowSwitchDefault : FlowSwitchMember {
        internal FlowSwitchDefault(FlowSwitch parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
    }
    public sealed class FlowSwitchCase : FlowSwitchMember {
        internal FlowSwitchCase(FlowSwitch parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private ExpressionSyntax _valueExpr;
        private SyntaxAnnotation _valueAnn;
        //private SimpleToken _valueToken;
        protected override void Initialize(Node node) {
            base.Initialize(node);
            base.Initialize(node);
            var valueNode = node.Member("Value");
            _valueExpr = valueNode.ToExpressionSyntax().SetAnn(out _valueAnn);
            //_valueToken = new SimpleToken(valueNode);
        }
        internal void GetModel(List<StatementSyntax> stmList, IdentifierNameSyntax switchValueName) {
            //>switchValueName = value;
            stmList.Add(CS.AssignStm(switchValueName, _valueExpr));
        }
        internal void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, IdentifierNameSyntax switchVarName) {
            //>switchImplName.Cases.Add(value, flowNode);
            stmList.Add(CS.AddInvoStm(switchVarName, "Cases", ImplRewriter.CheckCSExprOnly(ctx.GetExpr(_valueAnn), ctx.SemanticModel, this),
                GotoNode == null ? (ExpressionSyntax)CS.NullLiteral : GotoNode.ImplVarName));
        }
    }
    #endregion Flow
    public sealed class Transacted : Statement {
        internal Transacted(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private Sequence _body;
        private ExpressionSyntax _timeoutExpr;//opt
        private SyntaxAnnotation _timeoutAnn;//opt
        private ExpressionSyntax _initializerExpr;//opt
        private SyntaxAnnotation _initializerAnn;//opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _body = new Sequence(this, node.Member("Body"));
            var timeoutNode = node.Member("Timeout");
            if (timeoutNode.IsNotNull) {
                _timeoutExpr = timeoutNode.ToExpressionSyntax().SetAnn(out _timeoutAnn);
            }
            var initializerNode = node.Member("Initializer");
            if (initializerNode.IsNotNull) {
                _initializerExpr = initializerNode.ToExpressionSyntax().SetAnn(out _initializerAnn);
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            _body.GetModel(stmList);
            if (_timeoutExpr != null) {
                ActivityAncestor.GetModelVarDecl(stmList, CS.TimeSpanName, _timeoutExpr);
            }
            if (_initializerExpr != null) {
                ActivityAncestor.GetModelVarDecl(stmList, CS.ActionOf(CSEX.TransactionScopeName), _initializerExpr);
            }
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.TransactionScopeName));
            _body.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "Body", expr));
            if (_timeoutExpr != null) {
                //>varName.Timeout = new InArgument<>();
                stmList.Add(CS.AssignStm(varName, "Timeout", CSEX.NewInArgument(CS.TimeSpanName,
                    ImplRewriter.RewriteExpr(ctx.GetExpr(_timeoutAnn), CS.TimeSpanName, ctx.SemanticModel, this))));
            }
            if (_initializerExpr != null) {
                //>varName.Initialize(initializer);
                stmList.Add(CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(varName, "Initialize"),
                    ImplRewriter.CheckCSExprOnly(ctx.GetExpr(_initializerAnn), ctx.SemanticModel, this))));
            }
            stmList.Add(addToParent(varName));
        }
    }
    public sealed class Cancellable : Statement {
        internal Cancellable(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private Sequence _body;
        private Sequence _handler;
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _body = new Sequence(this, node.Member("Body"));
            _handler = new Sequence(this, node.Member("Handler"));
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            _body.GetModel(stmList);
            _handler.GetModel(stmList);
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.CancellationScopeName));
            _body.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "Body", expr));
            _handler.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "CancellationHandler", expr));
            stmList.Add(addToParent(varName));
        }
    }
    public sealed class Compensable : Statement {
        internal Compensable(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private Identifier _name; //opt
        private SimpleToken _nameExplicit; //opt
        private Store _nameStore; //opt
        private Sequence _body;
        private Sequence _confirmHandler; //opt
        private Sequence _compensateHandler; //opt
        private Sequence _cancelHandler; //opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _name = node.Member("Name").ToIdentifierOpt();
            if (_name != null) {
                _nameExplicit = node.Member("NameExplicit").ToSimpleTokenOpt();
                if (_nameExplicit == null) {
                    _nameStore = ActivityAncestor.Body.AddVariable(CSEX.CompensationTokenName, _name);
                }
            }
            _body = new Sequence(this, node.Member("Body"));
            var confirmHandlerNode = node.Member("ConfirmHandler");
            if (confirmHandlerNode.IsNotNull) {
                _confirmHandler = new Sequence(this, confirmHandlerNode);
            }
            var compensateHandlerNode = node.Member("CompensateHandler");
            if (compensateHandlerNode.IsNotNull) {
                _compensateHandler = new Sequence(this, compensateHandlerNode);
            }
            var cancelHandlerNode = node.Member("CancelHandler");
            if (cancelHandlerNode.IsNotNull) {
                _cancelHandler = new Sequence(this, cancelHandlerNode);
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            if (_nameExplicit != null) {
                _nameStore = StoreHost.TryGetStore(_name.PlainValue);
                if (_nameStore == null) {
                    CompilationContext.Throw(_name, ErrorKind.InvalidCompensationTokenReference, _name);
                }
                //>CompensationToken __v__ = Name;
                ActivityAncestor.GetModelVarDecl(stmList, CSEX.CompensationTokenName, CS.IdName(_name.CSToken));
            }
            _body.GetModel(stmList);
            if (_confirmHandler != null) {
                _confirmHandler.GetModel(stmList);
            }
            if (_compensateHandler != null) {
                _compensateHandler.GetModel(stmList);
            }
            if (_cancelHandler != null) {
                _cancelHandler.GetModel(stmList);
            }
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.CompensableActivityName));
            if (_name != null) {
                //>varName.Result = new OutArgument<CompensationToken>(Name);
                stmList.Add(CS.AssignStm(varName, "Result", CSEX.NewOutArgument(CSEX.CompensationTokenName, _nameStore)));
            }
            _body.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "Body", expr));
            if (_confirmHandler != null) {
                _confirmHandler.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "ConfirmationHandler", expr));
            }
            if (_compensateHandler != null) {
                _compensateHandler.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "CompensationHandler", expr));
            }
            if (_cancelHandler != null) {
                _cancelHandler.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "CancellationHandler", expr));
            }
            stmList.Add(addToParent(varName));
        }
    }
    public abstract class ConfirmOrCompensate : Statement {
        public bool IsConfirm { get; protected set; }
        private ExpressionSyntax _targetExpr;//opt
        private SyntaxAnnotation _targetAnn;//opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var targetNode = node.Member("Target");
            if (targetNode.IsNotNull) {
                _targetExpr = targetNode.ToExpressionSyntax().SetAnn(out _targetAnn);
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            if (_targetExpr != null) {
                //>CompensationToken __v__ = RefName;
                ActivityAncestor.GetModelVarDecl(stmList, CSEX.CompensationTokenName, _targetExpr);
            }
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(IsConfirm ? CSEX.ConfirmName : CSEX.CompensateName));
            if (_targetExpr != null) {
                //>varName.Target = new InArgument<CompensationToken>(...);
                stmList.Add(CS.AssignStm(varName, "Target", CSEX.NewInArgument(CSEX.CompensationTokenName,
                ImplRewriter.RewriteExpr(ctx.GetExpr(_targetAnn), CSEX.CompensationTokenName, ctx.SemanticModel, this))));
            }
            stmList.Add(addToParent(varName));
        }
    }
    public sealed class Confirm : ConfirmOrCompensate {
        internal Confirm(Statement parent, Node node) {
            Parent = parent;
            IsConfirm = true;
            Initialize(node);
        }
    }
    public sealed class Compensate : ConfirmOrCompensate {
        internal Compensate(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
    }
    public sealed class Persist : Statement {
        internal Persist(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            if (GetAncestor<NoPersist>(true) != null) {
                CompilationContext.Throw(Keyword, ErrorKind.PersistCannotBeInNoPersist);
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) { }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            stmList.Add(addToParent(CS.NewObjExpr(CSEX.PersistName)));
        }
    }
    public sealed class NoPersist : Statement {
        internal NoPersist(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private Sequence _body;
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _body = new Sequence(this, node.Member("Body"));
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            _body.GetModel(stmList);
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.NoPersistScopeName));
            _body.GetImpl(ctx, stmList, expr => CS.AssignStm(varName, "Body", expr));
            stmList.Add(addToParent(varName));
        }
    }
    public sealed class Terminate : Statement {
        internal Terminate(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private ExpressionSyntax _reasonExpr;
        private SyntaxAnnotation _reasonAnn;
        private ExpressionSyntax _exceptionExpr;
        private SyntaxAnnotation _exceptionAnn;
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var reasonNode = node.Member("Reason");
            if (reasonNode.IsNotNull) {
                _reasonExpr = reasonNode.ToExpressionSyntax().SetAnn(out _reasonAnn);
            }
            var exceptionNode = node.Member("Exception");
            if (exceptionNode.IsNotNull) {
                _exceptionExpr = exceptionNode.ToExpressionSyntax().SetAnn(out _exceptionAnn);
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            if (_reasonExpr != null) {
                ActivityAncestor.GetModelVarDecl(stmList, CS.StringType, _reasonExpr);
            }
            if (_exceptionExpr != null) {
                ActivityAncestor.GetModelVarDecl(stmList, CS.ExceptionName, _exceptionExpr);
            }
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.TerminateWorkflowName));
            if (_reasonExpr != null) {
                //>varName.Reason = new InArgument<string>(...);
                stmList.Add(CS.AssignStm(varName, "Reason", CSEX.NewInArgument(CS.StringType,
                    ImplRewriter.RewriteExpr(ctx.GetExpr(_reasonAnn), CS.StringType, ctx.SemanticModel, this))));
            }
            if (_exceptionExpr != null) {
                //>varName.Exception = new InArgument<Exception>(...);
                stmList.Add(CS.AssignStm(varName, "Exception", CSEX.NewInArgument(CS.ExceptionName,
                    ImplRewriter.RewriteExpr(ctx.GetExpr(_exceptionAnn), CS.ExceptionName, ctx.SemanticModel, this))));
            }
            stmList.Add(addToParent(varName));
        }
    }
    #region Services
    public enum ServiceOperationKind { Receive, SendReply, Send, ReceiveReply }
    public abstract class ServiceOperation : Statement {
        private ServiceOperationKind _kind;
        public ServiceOperationKind Kind { get { return _kind; } protected set { _kind = value; } }
        internal bool IsReceive { get { return _kind == ServiceOperationKind.Receive; } }
        internal bool IsSendReply { get { return _kind == ServiceOperationKind.SendReply; } }
        internal bool IsSend { get { return _kind == ServiceOperationKind.Send; } }
        internal bool IsReceiveReply { get { return _kind == ServiceOperationKind.ReceiveReply; } }
        internal bool IsRequest { get { return _kind == ServiceOperationKind.Receive || _kind == ServiceOperationKind.Send; } }
        internal bool IsReply { get { return !IsRequest; } }
        internal bool IsServiceSide { get { return _kind == ServiceOperationKind.Receive || _kind == ServiceOperationKind.SendReply; } }
        internal bool IsClientSide { get { return !IsServiceSide; } }
        protected QualifiedNameSyntax ImplTypeName {
            get {
                return IsReceive ? CSEX.ReceiveName : IsSendReply ? CSEX.SendReplyName : IsSend ? CSEX.SendName : CSEX.ReceiveReplyName;
            }
        }
        //
        protected ServiceOperationContent Content { get; private set; }
        //
        private ExpressionSyntax _initializerExpr;//opt
        private SyntaxAnnotation _initializerAnn;//opt
        //
        protected override void Initialize(Node node) {
            ActivityAncestor.ServiceOperationList.Add(this);
            base.Initialize(node);
            Content = ServiceOperationContent.Create(this, node.Member("Content"));
            var initializerNode = node.Member("Initializer");
            if (initializerNode.IsNotNull) {
                _initializerExpr = initializerNode.ToExpressionSyntax().SetAnn(out _initializerAnn);
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            Content.GetModel(stmList);
            if (_initializerExpr != null) {
                ActivityAncestor.GetModelVarDecl(stmList, CS.ActionOf(ImplTypeName), _initializerExpr);
            }
        }
        protected static void GetCorrIniterImpl(List<StatementSyntax> stmList, IdentifierNameSyntax varName, TypeSyntax corrIniterType,
            object corrNameorHandle, ExpressionSyntax otherInitExpr = null) {
            //>varName.CorrelationInitializers.Add(new XXCorrelationInitializer() {
            //>    CorrelationHandle = new InArgument<CorrelationHandle>(corrHandle)
            //>});
            var handleExpr = CS.AssignExpr(CS.IdName("CorrelationHandle"), CSEX.NewInArgument(CSEX.CorrelationHandleName, corrNameorHandle));
            stmList.Add(CS.AddInvoStm(varName, "CorrelationInitializers", CS.NewObjExpr(corrIniterType, null,
                otherInitExpr != null ? new ExpressionSyntax[] { handleExpr, otherInitExpr } : new ExpressionSyntax[] { handleExpr })));
        }
        protected void GetIniterImpl(SemanticContext ctx, List<StatementSyntax> stmList, IdentifierNameSyntax varName) {
            if (_initializerExpr != null) {
                //>varName.Initialize(initializer);
                stmList.Add(CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(varName, "Initialize"),
                    ImplRewriter.CheckCSExprOnly(ctx.GetExpr(_initializerAnn), ctx.SemanticModel, this))));
            }
        }
    }
    public abstract class RequestServiceOperation : ServiceOperation {
        private ExpressionSyntax _contractNameExpr;
        private SyntaxAnnotation _contractNameAnn;
        private ExpressionSyntax _nameExpr;
        private SyntaxAnnotation _nameAnn;
        //
        internal Identifier RequestCorrName { get; private set; }//opt
        private Store _requestCorrNameStore;//opt
        internal ReplyServiceOperation ReplyCorr { get; set; }//opt
        //
        private Identifier _callbackCorrName; //opt
        private Store _callbackCorrNameStore;//opt
        private ExpressionSyntax _callbackCorrHandleExpr;//opt
        private SyntaxAnnotation _callbackCorrHandleAnn;//opt
        //
        private ExpressionSyntax _refContextCorrHandleExpr;//opt
        private SyntaxAnnotation _refContextCorrHandleAnn;//opt
        //
        private ExpressionSyntax _refCallbackCorrHandleExpr;//opt
        private SyntaxAnnotation _refCallbackCorrHandleAnn;//opt
        //
        private ExpressionSyntax _refContentCorrHandleExpr;//opt
        private SyntaxAnnotation _refContentCorrHandleAnn;//opt
        private ExpressionSyntax _refContentCorrMsgQrySetExpr;//opt
        private SyntaxAnnotation _refContentCorrMsgQrySetAnn;//opt

        protected override void Initialize(Node node) {
            base.Initialize(node);
            _contractNameExpr = node.Member("ContractName").ToExpressionSyntax().SetAnn(out _contractNameAnn);
            _nameExpr = node.Member("Name").ToExpressionSyntax().SetAnn(out _nameAnn);
            //
            RequestCorrName = node.Member("RequestCorrName").ToIdentifierOpt();
            //
            var callbackCorrNameNode = node.Member("CallbackCorrName");
            if (callbackCorrNameNode.IsNotNull) {
                _callbackCorrName = new Identifier(callbackCorrNameNode);
                _callbackCorrNameStore = ActivityAncestor.Body.AddVariable(CSEX.CorrelationHandleName, _callbackCorrName);
            }
            else {
                var callbackCorrHandleNode = node.Member("CallbackCorrHandle");
                if (callbackCorrHandleNode.IsNotNull) {
                    _callbackCorrHandleExpr = callbackCorrHandleNode.ToExpressionSyntax().SetAnn(out _callbackCorrHandleAnn);
                }
            }
            //
            var refContextCorrHandleNode = node.Member("RefContextCorrHandle");
            if (refContextCorrHandleNode.IsNotNull) {
                _refContextCorrHandleExpr = refContextCorrHandleNode.ToExpressionSyntax().SetAnn(out _refContextCorrHandleAnn);
            }
            var refCallbackCorrHandleNode = node.Member("RefCallbackCorrHandle");
            if (refCallbackCorrHandleNode.IsNotNull) {
                _refCallbackCorrHandleExpr = refCallbackCorrHandleNode.ToExpressionSyntax().SetAnn(out _refCallbackCorrHandleAnn);
            }
            var refContentCorrHandleNode = node.Members.TryGetValue("RefContentCorrHandle");
            if (refContentCorrHandleNode != null && refContentCorrHandleNode.IsNotNull) {
                _refContentCorrHandleExpr = refContentCorrHandleNode.ToExpressionSyntax().SetAnn(out _refContentCorrHandleAnn);
                _refContentCorrMsgQrySetExpr = node.Member("RefContentCorrMsgQrySet").ToExpressionSyntax().SetAnn(out _refContentCorrMsgQrySetAnn);
            }

        }
        internal void PostInitialize() {
            if (ReplyCorr == null) {
                if (RequestCorrName != null) {
                    CompilationContext.Throw(RequestCorrName, ErrorKind.RequestWithoutReplyCannotSetRequestCorr, RequestCorrName);
                }
            }
            else {
                _requestCorrNameStore = ActivityAncestor.Body.AddVariable(CSEX.CorrelationHandleName,
                    RequestCorrName ?? new Identifier(ActivityAncestor.GetRequestCorrAutoVarName(), Keyword.SourceSpan));
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            base.GetModel(stmList);
            ActivityAncestor.GetModelVarDecl(stmList, CS.XNameName, _contractNameExpr);
            ActivityAncestor.GetModelVarDecl(stmList, CS.StringType, _nameExpr);
            if (_callbackCorrHandleExpr != null) {
                ActivityAncestor.GetModelVarDecl(stmList, CSEX.CorrelationHandleName, _callbackCorrHandleExpr);
            }
            if (_refContextCorrHandleExpr != null) {
                //>CorrelationHandle __v__ = RefHandle;
                ActivityAncestor.GetModelVarDecl(stmList, CSEX.CorrelationHandleName, _refContextCorrHandleExpr);
            }
            if (_refCallbackCorrHandleExpr != null) {
                //>CorrelationHandle __v__ = RefHandle;
                ActivityAncestor.GetModelVarDecl(stmList, CSEX.CorrelationHandleName, _refCallbackCorrHandleExpr);
            }
            if (_refContentCorrHandleExpr != null) {
                //>CorrelationHandle __v__ = RefHandle;
                ActivityAncestor.GetModelVarDecl(stmList, CSEX.CorrelationHandleName, _refContentCorrHandleExpr);
                //>MessageQuerySet __v__ = expr;
                ActivityAncestor.GetModelVarDecl(stmList, CSEX.MessageQuerySetName, _refContentCorrMsgQrySetExpr);
            }
        }
        internal IdentifierNameSyntax ImplVarName { get; private set; }
        internal void PreGetImpl(SemanticContext ctx, List<StatementSyntax> rootStmList) {
            ImplVarName = ActivityAncestor.GetImplVarName(rootStmList, CS.NewObjExpr(ImplTypeName));
        }
        internal override sealed void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = ImplVarName;
            //>varName.ServiceContractName = ;
            stmList.Add(CS.AssignStm(varName, "ServiceContractName", ImplRewriter.CheckCSExprOnly(ctx.GetExpr(_contractNameAnn), ctx.SemanticModel, this)));
            //>varName.OperationName = ;
            stmList.Add(CS.AssignStm(varName, "OperationName", ImplRewriter.CheckCSExprOnly(ctx.GetExpr(_nameAnn), ctx.SemanticModel, this)));
            Content.GetImpl(ctx, stmList, varName);
            if (_requestCorrNameStore != null) {
                GetCorrIniterImpl(stmList, varName, CSEX.RequestReplyCorrelationInitializerName, _requestCorrNameStore);
            }
            if (_callbackCorrNameStore != null || _callbackCorrHandleExpr != null) {
                GetCorrIniterImpl(stmList, varName, CSEX.CallbackCorrelationInitializerName,
                    (object)_callbackCorrNameStore ?? ImplRewriter.RewriteExpr(ctx.GetExpr(_callbackCorrHandleAnn), CSEX.CorrelationHandleName, ctx.SemanticModel, this));
            }
            if (_refContextCorrHandleExpr != null) {
                //>varName.CorrelatesWith = new InArgument<CorrelationHandle>(...);
                stmList.Add(CS.AssignStm(varName, "CorrelatesWith", CSEX.NewInArgument(CSEX.CorrelationHandleName,
                    ImplRewriter.RewriteExpr(ctx.GetExpr(_refContextCorrHandleAnn), CSEX.CorrelationHandleName, ctx.SemanticModel, this))));
            }
            else if (_refCallbackCorrHandleExpr != null) {
                //>varName.CorrelatesWith = new InArgument<CorrelationHandle>(...);
                stmList.Add(CS.AssignStm(varName, "CorrelatesWith", CSEX.NewInArgument(CSEX.CorrelationHandleName,
                    ImplRewriter.RewriteExpr(ctx.GetExpr(_refCallbackCorrHandleAnn), CSEX.CorrelationHandleName, ctx.SemanticModel, this))));
            }
            else if (_refContentCorrHandleExpr != null) {
                //>varName.CorrelatesWith = new InArgument<CorrelationHandle>(...);
                stmList.Add(CS.AssignStm(varName, "CorrelatesWith", CSEX.NewInArgument(CSEX.CorrelationHandleName,
                    ImplRewriter.RewriteExpr(ctx.GetExpr(_refContentCorrHandleAnn), CSEX.CorrelationHandleName, ctx.SemanticModel, this))));
                //>varName.CorrelatesOn = 
                stmList.Add(CS.AssignStm(varName, "CorrelatesOn",
                    ImplRewriter.CheckCSExprOnly(ctx.GetExpr(_refContentCorrMsgQrySetAnn), ctx.SemanticModel, this)));
            }
            PostGetImpl(ctx, stmList);
            GetIniterImpl(ctx, stmList, varName);
            stmList.Add(addToParent(varName));
        }
        protected virtual void PostGetImpl(SemanticContext ctx, List<StatementSyntax> stmList) { }
    }
    public abstract class ReplyServiceOperation : ServiceOperation {
        private Identifier _contextCorrName; //opt
        private Store _contextCorrNameStore;//opt
        private ExpressionSyntax _contextCorrHandleExpr;//opt
        private SyntaxAnnotation _contextCorrHandleAnn;//opt
        //
        private Identifier _contentCorrName; //opt
        private Store _contentCorrNameStore;//opt
        private ExpressionSyntax _contentCorrHandleExpr;//opt
        private SyntaxAnnotation _contentCorrHandleAnn;//opt
        private ExpressionSyntax _contentCorrMsgQrySetExpr;//opt
        private SyntaxAnnotation _contentCorrMsgQrySetAnn;//opt
        //
        private Identifier _refRequestCorrName;//opt
        private RequestServiceOperation _requestCorr;//opt
        //
        protected override void Initialize(Node node) {
            base.Initialize(node);
            //
            var contextCorrNameNode = node.Member("ContextCorrName");
            if (contextCorrNameNode.IsNotNull) {
                _contextCorrName = new Identifier(contextCorrNameNode);
                _contextCorrNameStore = ActivityAncestor.Body.AddVariable(CSEX.CorrelationHandleName, _contextCorrName);
            }
            else {
                var contextCorrHandleNode = node.Member("ContextCorrHandle");
                if (contextCorrHandleNode.IsNotNull) {
                    _contextCorrHandleExpr = contextCorrHandleNode.ToExpressionSyntax().SetAnn(out _contextCorrHandleAnn);
                }
            }
            //
            var contentCorrNameNode = node.Members.TryGetValue("ContentCorrName");
            if (contentCorrNameNode != null && contentCorrNameNode.IsNotNull) {
                _contentCorrName = new Identifier(contentCorrNameNode);
                _contentCorrNameStore = ActivityAncestor.Body.AddVariable(CSEX.CorrelationHandleName, _contentCorrName);
            }
            else {
                var contentCorrHandleNode = node.Members.TryGetValue("ContentCorrHandle");
                if (contentCorrHandleNode != null && contentCorrHandleNode.IsNotNull) {
                    _contentCorrHandleExpr = contentCorrHandleNode.ToExpressionSyntax().SetAnn(out _contentCorrHandleAnn);
                }
            }
            if (_contentCorrName != null || _contentCorrHandleExpr != null) {
                _contentCorrMsgQrySetExpr = node.Member("ContentCorrMsgQrySet").ToExpressionSyntax().SetAnn(out _contentCorrMsgQrySetAnn);
            }
            //
            _refRequestCorrName = node.Member("RefRequestCorrName").ToIdentifierOpt();
        }
        internal void ResolveRefRequestCorr() {
            var isServiceSide = IsServiceSide;
            var soList = ActivityAncestor.ServiceOperationList;
            if (_refRequestCorrName != null) {
                foreach (var request in soList.OfType<RequestServiceOperation>()) {
                    if (request.RequestCorrName == _refRequestCorrName && request.IsServiceSide == isServiceSide) {
                        _requestCorr = request;
                        break;
                    }
                }
                if (_requestCorr == null) {
                    CompilationContext.Throw(_refRequestCorrName, ErrorKind.InvalidRequestCorrReference, _refRequestCorrName);
                }
            }
            else {
                var idx = soList.IndexOf(this);
                while (--idx >= 0) {
                    var request = soList[idx] as RequestServiceOperation;
                    if (request != null && request.IsServiceSide == isServiceSide) {
                        _requestCorr = request;
                        break;
                    }
                }
                if (_requestCorr == null) {
                    CompilationContext.Throw(Keyword, ErrorKind.CannotFindRequest);
                }
            }
            if (_requestCorr.ReplyCorr != null) {
                CompilationContext.Throw((ValueBase)_refRequestCorrName ?? Keyword, ErrorKind.RequestAlreadyReferencedByAnotherReply);
            }
            _requestCorr.ReplyCorr = this;
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            base.GetModel(stmList);
            if (_contextCorrHandleExpr != null) {
                ActivityAncestor.GetModelVarDecl(stmList, CSEX.CorrelationHandleName, _contextCorrHandleExpr);
            }
            if (_contentCorrHandleExpr != null) {
                //>CorrelationHandle __v__ = RefHandle;
                ActivityAncestor.GetModelVarDecl(stmList, CSEX.CorrelationHandleName, _contentCorrHandleExpr);
            }
            if (_contentCorrMsgQrySetExpr != null) {
                //>MessageQuerySet __v__ = expr;
                ActivityAncestor.GetModelVarDecl(stmList, CSEX.MessageQuerySetName, _contentCorrMsgQrySetExpr);
            }
        }
        internal override sealed void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(ImplTypeName));
            //>varName.Request = ...;
            stmList.Add(CS.AssignStm(varName, "Request", _requestCorr.ImplVarName));
            Content.GetImpl(ctx, stmList, varName);
            if (_contextCorrNameStore != null || _contextCorrHandleExpr != null) {
                GetCorrIniterImpl(stmList, varName, CSEX.ContextCorrelationInitializerName,
                    (object)_contextCorrNameStore ?? ImplRewriter.RewriteExpr(ctx.GetExpr(_contextCorrHandleAnn), CSEX.CorrelationHandleName, ctx.SemanticModel, this));
            }
            if (_contentCorrNameStore != null || _contentCorrHandleExpr != null) {
                GetCorrIniterImpl(stmList, varName, CSEX.QueryCorrelationInitializerName,
                    (object)_contentCorrNameStore ?? ImplRewriter.RewriteExpr(ctx.GetExpr(_contentCorrHandleAnn), CSEX.CorrelationHandleName, ctx.SemanticModel, this),
                    CS.AssignExpr(CS.IdName("MessageQuerySet"), ImplRewriter.CheckCSExprOnly(ctx.GetExpr(_contentCorrMsgQrySetAnn), ctx.SemanticModel, this)));
            }
            GetIniterImpl(ctx, stmList, varName);
            stmList.Add(addToParent(varName));
        }
    }
    public sealed class Receive : RequestServiceOperation {
        internal Receive(Statement parent, Node node) {
            Parent = parent;
            Kind = ServiceOperationKind.Receive;
            Initialize(node);
        }
    }
    public sealed class SendReply : ReplyServiceOperation {
        internal SendReply(Statement parent, Node node) {
            Parent = parent;
            Kind = ServiceOperationKind.SendReply;
            Initialize(node);
        }
    }
    public sealed class Send : RequestServiceOperation {
        internal Send(Statement parent, Node node) {
            Parent = parent;
            Kind = ServiceOperationKind.Send;
            Initialize(node);
        }
        private ExpressionSyntax _endpointAddressExpr;//opt
        private SyntaxAnnotation _endpointAddressAnn;//opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var endpointAddressNode = node.Member("EndpointAddress");
            if (endpointAddressNode.IsNotNull) {
                _endpointAddressExpr = endpointAddressNode.ToExpressionSyntax().SetAnn(out _endpointAddressAnn);
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            base.GetModel(stmList);
            if (_endpointAddressExpr != null) {
                ActivityAncestor.GetModelVarDecl(stmList, CS.UriName, _endpointAddressExpr);
            }
        }
        protected override void PostGetImpl(SemanticContext ctx, List<StatementSyntax> stmList) {
            if (_endpointAddressExpr != null) {
                //>varName.EndpointAddress = new InArguemnt<Uri>(...);
                stmList.Add(CS.AssignStm(ImplVarName, "EndpointAddress", CSEX.NewInArgument(CS.UriName,
                    ImplRewriter.RewriteExpr(ctx.GetExpr(_endpointAddressAnn), CS.UriName, ctx.SemanticModel, this))));
            }
        }
    }
    public sealed class ReceiveReply : ReplyServiceOperation {
        internal ReceiveReply(Statement parent, Node node) {
            Parent = parent;
            Kind = ServiceOperationKind.ReceiveReply;
            Initialize(node);
        }
    }
    public abstract class ServiceOperationContent : Statement {
        //new public ServiceOperation Parent { get { return (ServiceOperation)base.Parent; } private set { base.Parent = value; } }
        internal override sealed void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) { throw new NotImplementedException(); }
        internal abstract void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, IdentifierNameSyntax opImplVarName);
        internal static ServiceOperationContent Create(ServiceOperation parent, Node node) {
            if (node.Label == "ReceiveContent") return new ServiceOperationReceiveContent(parent, node);
            return new ServiceOperationSendContent(parent, node);
        }
    }
    public sealed class ServiceOperationReceiveContent : ServiceOperationContent {
        internal ServiceOperationReceiveContent(ServiceOperation parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private readonly List<ServiceOperationReceiveParameter> _parameterList = new List<ServiceOperationReceiveParameter>();
        private Identifier _storeName; //opt
        private Store _store; //opt
        //internal bool IsMessage { get { return StoreName != null; } }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _storeName = node.Member("StoreName").ToIdentifierOpt();
            if (_storeName != null) {
                _store = StoreHost.TryGetStore(_storeName.PlainValue);
                if (_store == null) {
                    CompilationContext.Throw(_storeName, ErrorKind.InvalidActivityVariableOrParameterReference, _storeName);
                }
            }
            else {
                foreach (var paraNode in node.Member("Parameters").Items) {
                    _parameterList.Add(new ServiceOperationReceiveParameter(this, paraNode));
                }
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            if (_store != null) {
                stmList.Add(CS.AssignStm(CS.IdName(_store.Name.CSToken), SyntaxFactory.DefaultExpression(_store.Type)));
            }
            else {
                foreach (var p in _parameterList) {
                    p.GetModel(stmList);
                }
            }
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, IdentifierNameSyntax opImplVarName) {
            ExpressionSyntax expr = null;
            if (_store != null) {
                //>new ReceiveMessageContent(new OutArgument<TYPE>(new MetahLocationActivity(v1)))
                expr = CS.NewObjExpr(CSEX.ReceiveMessageContentName, CSEX.NewOutArgument(_store.Type, _store));
            }
            else if (_parameterList.Count > 0) {
                //>new ReceiveParametersContent(new Dictionary<string, OutArgument>{ ... })
                expr = CS.NewObjExpr(CSEX.ReceiveParametersContentName, CS.NewObjWithCollInitExpr(CS.DictionaryOf(CS.StringType, CSEX.OutArgumentName),
                    _parameterList.Select(p => new ExpressionSyntax[] { p.GetNameExpr(ctx),
                                CSEX.NewOutArgument(p.Store.Type, p.Store) })));
            }
            if (expr != null) {
                //>opImplVarName.Content = expr;
                stmList.Add(CS.AssignStm(opImplVarName, "Content", expr));
            }
        }
    }
    public sealed class ServiceOperationSendContent : ServiceOperationContent {
        internal ServiceOperationSendContent(ServiceOperation parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private readonly List<ServiceOperationSendParameter> _parameterList = new List<ServiceOperationSendParameter>();
        private ExpressionSyntax _valueExpr;
        private SyntaxAnnotation _valueAnn;
        //internal bool IsMessage { get { return _valueExpr != null; } }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var valueNode = node.Member("Value");
            if (valueNode.IsNotNull) {
                _valueExpr = valueNode.ToExpressionSyntax().SetAnn(out _valueAnn);
            }
            else {
                foreach (var paraNode in node.Member("Parameters").Items) {
                    _parameterList.Add(new ServiceOperationSendParameter(this, paraNode));
                }
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            if (_valueExpr != null) {
                ActivityAncestor.GetModelVarDecl(stmList, null, _valueExpr);
            }
            else {
                foreach (var p in _parameterList) {
                    p.GetModel(stmList);
                }
            }
        }
        internal static ExpressionSyntax GetValueExpr(SemanticContext ctx, SyntaxAnnotation valueAnn, Statement statement) {
            var valueExpr = ctx.GetExpr(valueAnn);
            var valueType = ctx.SemanticModel.GetTypeInfo(valueExpr).Type.ToTypeSyntax();
            return CSEX.NewInArgument(valueType,
                ImplRewriter.RewriteExpr(valueExpr, valueType, ctx.SemanticModel, statement));
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, IdentifierNameSyntax opImplVarName) {
            ExpressionSyntax expr = null;
            if (_valueExpr != null) {
                //>new SendMessageContent(new InArgument<TYPE>(__ctx__ => ...))
                expr = CS.NewObjExpr(CSEX.SendMessageContentName, GetValueExpr(ctx, _valueAnn, this));
            }
            else if (_parameterList.Count > 0) {
                //>new SendParametersContent(new Dictionary<string, InArgument>{ ... })
                expr = CS.NewObjExpr(CSEX.SendParametersContentName, CS.NewObjWithCollInitExpr(CS.DictionaryOf(CS.StringType, CSEX.InArgumentName),
                    _parameterList.Select(p => new ExpressionSyntax[] { p.GetNameExpr(ctx), p.GetValueExpr(ctx) })));
            }
            if (expr != null) {
                //>opImplVarName.Content = expr;
                stmList.Add(CS.AssignStm(opImplVarName, "Content", expr));
            }
        }
    }
    public abstract class ServiceOperationParameter : Statement {
        //new public ServiceOperationContent Parent { get { return (ServiceOperationContent)base.Parent; } private set { base.Parent = value; } }
        private ExpressionSyntax _nameExpr;
        private SyntaxAnnotation _nameAnn;
        internal ExpressionSyntax GetNameExpr(SemanticContext ctx) {
            return ImplRewriter.CheckCSExprOnly(ctx.GetExpr(_nameAnn), ctx.SemanticModel, this);
        }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _nameExpr = node.Member("Name").ToExpressionSyntax().SetAnn(out _nameAnn);
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            ActivityAncestor.GetModelVarDecl(stmList, CS.StringType, _nameExpr);
        }
        internal override sealed void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) { throw new NotImplementedException(); }
    }
    public sealed class ServiceOperationReceiveParameter : ServiceOperationParameter {
        internal ServiceOperationReceiveParameter(ServiceOperationReceiveContent parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private Identifier _storeName;
        internal Store Store { get; private set; }
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _storeName = new Identifier(node.Member("StoreName"));
            Store = StoreHost.TryGetStore(_storeName.PlainValue);
            if (Store == null) {
                CompilationContext.Throw(_storeName, ErrorKind.InvalidActivityVariableOrParameterReference, _storeName);
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            base.GetModel(stmList);
            stmList.Add(CS.AssignStm(CS.IdName(Store.Name.CSToken), SyntaxFactory.DefaultExpression(Store.Type)));
        }
    }
    public sealed class ServiceOperationSendParameter : ServiceOperationParameter {
        internal ServiceOperationSendParameter(ServiceOperationSendContent parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private ExpressionSyntax _valueExpr;
        private SyntaxAnnotation _valueAnn;
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _valueExpr = node.Member("Value").ToExpressionSyntax().SetAnn(out _valueAnn);
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            base.GetModel(stmList);
            ActivityAncestor.GetModelVarDecl(stmList, null, _valueExpr);
        }
        internal ExpressionSyntax GetValueExpr(SemanticContext ctx) {
            return ServiceOperationSendContent.GetValueExpr(ctx, _valueAnn, this);
        }
    }
    public sealed class ContentCorr : Statement {
        internal ContentCorr(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private struct Data {
            internal Data(ExpressionSyntax keyExpr, SyntaxAnnotation keyAnn, ExpressionSyntax valueExpr, SyntaxAnnotation valueAnn) {
                KeyExpr = keyExpr;
                KeyAnn = keyAnn;
                ValueExpr = valueExpr;
                ValueAnn = valueAnn;
            }
            internal readonly ExpressionSyntax KeyExpr;
            internal readonly SyntaxAnnotation KeyAnn;
            internal readonly ExpressionSyntax ValueExpr;
            internal readonly SyntaxAnnotation ValueAnn;
        }
        private readonly List<Data> _dataList = new List<Data>();
        private Identifier _contentCorrName; //opt
        private Store _contentCorrNameStore;//opt
        private ExpressionSyntax _contentCorrHandleExpr;//opt
        private SyntaxAnnotation _contentCorrHandleAnn;//opt
        protected override void Initialize(Node node) {
            base.Initialize(node);
            var contentCorrNameNode = node.Members.TryGetValue("ContentCorrName");
            if (contentCorrNameNode != null && contentCorrNameNode.IsNotNull) {
                _contentCorrName = new Identifier(contentCorrNameNode);
                _contentCorrNameStore = ActivityAncestor.Body.AddVariable(CSEX.CorrelationHandleName, _contentCorrName);
            }
            else {
                var contentCorrHandleNode = node.Members.TryGetValue("ContentCorrHandle");
                if (contentCorrHandleNode != null && contentCorrHandleNode.IsNotNull) {
                    _contentCorrHandleExpr = contentCorrHandleNode.ToExpressionSyntax().SetAnn(out _contentCorrHandleAnn);
                }
            }
            foreach (var dataNode in node.Member("DataList").Items) {
                SyntaxAnnotation keyAnn;
                var keyExpr = dataNode.Member("Key").ToExpressionSyntax().SetAnn(out keyAnn);
                SyntaxAnnotation valueAnn;
                var valueExpr = dataNode.Member("Value").ToExpressionSyntax().SetAnn(out valueAnn);
                _dataList.Add(new Data(keyExpr, keyAnn, valueExpr, valueAnn));
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            //base.GetModel(stmList);
            if (_contentCorrHandleExpr != null) {
                //>CorrelationHandle __v__ = RefHandle;
                ActivityAncestor.GetModelVarDecl(stmList, CSEX.CorrelationHandleName, _contentCorrHandleExpr);
            }
            foreach (var data in _dataList) {
                ActivityAncestor.GetModelVarDecl(stmList, CS.StringType, data.KeyExpr);
                ActivityAncestor.GetModelVarDecl(stmList, CS.StringType, data.ValueExpr);
            }
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(stmList, CS.NewObjExpr(CSEX.InitializeCorrelationName));
            //>varName.Correlation = new InArgument<CorrelationHandle>(...);
            stmList.Add(CS.AssignStm(varName, "Correlation", CSEX.NewInArgument(CSEX.CorrelationHandleName,
                (object)_contentCorrNameStore ?? ImplRewriter.RewriteExpr(ctx.GetExpr(_contentCorrHandleAnn), CSEX.CorrelationHandleName, ctx.SemanticModel, this))));
            foreach (var data in _dataList) {
                //>varName.CorrelationData.Add(key, value);
                stmList.Add(CS.AddInvoStm(varName, "CorrelationData",
                    ImplRewriter.CheckCSExprOnly(ctx.GetExpr(data.KeyAnn), ctx.SemanticModel, this),
                    CSEX.NewInArgument(CS.StringType, ImplRewriter.RewriteExpr(ctx.GetExpr(data.ValueAnn), CS.StringType, ctx.SemanticModel, this))));
            }
            stmList.Add(addToParent(varName));
        }
    }
    public sealed class TransactedReceive : Statement {
        internal TransactedReceive(Statement parent, Node node) {
            Parent = parent;
            Initialize(node);
        }
        private Sequence _body;
        protected override void Initialize(Node node) {
            base.Initialize(node);
            _body = new Sequence(this, node.Member("Body"));
            if (_body.MemberList.Count == 0) {
                CompilationContext.Throw(_body, ErrorKind.FirstMemberOfTransactedReceiveMustBeReceive);
            }
            if (!(_body.MemberList[0] is Receive)) {
                CompilationContext.Throw(_body.MemberList[0], ErrorKind.FirstMemberOfTransactedReceiveMustBeReceive);
            }
        }
        internal override void GetModel(List<StatementSyntax> stmList) {
            _body.GetModel(stmList);
        }
        internal override void GetImpl(SemanticContext ctx, List<StatementSyntax> stmList, ExprToStm addToParent) {
            var blkStmList = new List<StatementSyntax>();
            GetImplBlockMembers(ctx, blkStmList, addToParent);
            stmList.Add(SyntaxFactory.Block(blkStmList));
        }
        private void GetImplBlockMembers(SemanticContext ctx, List<StatementSyntax> blkStmList, ExprToStm addToParent) {
            var varName = ActivityAncestor.GetImplVarName(blkStmList, CS.NewObjExpr(CSEX.TransactedReceiveScopeName));
            _body.GetVariablesImpl(blkStmList, expr => CS.AddInvoStm(varName, "Variables", expr));
            _body.MemberList[0].GetImpl(ctx, blkStmList, expr => CS.AssignStm(varName, "Request", expr));
            _body.GetImpl(ctx, blkStmList, expr => CS.AssignStm(varName, "Body", expr), 1);
            blkStmList.Add(addToParent(varName));
        }
    }
    #endregion Services

    //
    //
    //
    internal static class CSEX {
        internal static readonly string[] ActivityMetaNameParts = new[] { "Activity", "Activities", "System" };
        internal static readonly string[] Activity1MetaNameParts = new[] { "Activity`1", "Activities", "System" };
        internal static readonly string[] ActivitiesSystemMetaNameParts = new[] { "Activities", "System" };
        internal static readonly string[] ArgumentMetaNames = new[] { "InArgument`1", "OutArgument`1", "InOutArgument`1", };//DO NOT change element order
        internal static TypeSyntax ToNonVarTypeSyntax(this Node node) {
            var type = (TypeSyntax)node.ToSyntaxNode();
            if (type.IsVar) {
                CompilationContext.Throw(node, ErrorKind.TypeVarNotAllowed);
            }
            return type;
        }

        //global::System.Activities
        internal static QualifiedNameSyntax GlobalSystemActivitiesName {
            get { return CS.QualifiedName(CS.GlobalSystemName, "Activities"); }
        }
        internal static MemberAccessExpressionSyntax GlobalSystemActivitiesExpr {
            get { return CS.MemberAccessExpr(CS.GlobalSystemName, "Activities"); }
        }

        //global::System.Activities.Activity
        internal static QualifiedNameSyntax ActivityName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "Activity"); }
        }
        //global::System.Activities.NativeActivity
        internal static QualifiedNameSyntax NativeActivityName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "NativeActivity"); }
        }
        //global::System.Activities.NativeActivity<T>
        internal static QualifiedNameSyntax NativeActivityOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesName, CS.GenericName("NativeActivity", type));
        }
        //global::System.Activities.ActivityDelegate
        internal static QualifiedNameSyntax ActivityDelegateName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "ActivityDelegate"); }
        }
        //global::System.Activities.Activity<T>
        internal static QualifiedNameSyntax ActivityOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesName, CS.GenericName("Activity", type));
        }
        //global::System.Activities.InArgument<T>
        internal static QualifiedNameSyntax InArgumentOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesName, CS.GenericName("InArgument", type));
        }
        //global::System.Activities.DelegateInArgument<T>
        internal static QualifiedNameSyntax DelegateInArgumentOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesName, CS.GenericName("DelegateInArgument", type));
        }
        //global::System.Activities.OutArgument<T>
        internal static QualifiedNameSyntax OutArgumentOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesName, CS.GenericName("OutArgument", type));
        }
        //global::System.Activities.DelegateOutArgument<T>
        internal static QualifiedNameSyntax DelegateOutArgumentOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesName, CS.GenericName("DelegateOutArgument", type));
        }
        //global::System.Activities.InOutArgument<T>
        internal static QualifiedNameSyntax InOutArgumentOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesName, CS.GenericName("InOutArgument", type));
        }
        //global::System.Activities.Variable<T>
        internal static QualifiedNameSyntax VariableOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesName, CS.GenericName("Variable", type));
        }
        //global::System.Activities.Variable
        internal static QualifiedNameSyntax VariableName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "Variable"); }
        }
        //global::System.Activities.Argument
        internal static QualifiedNameSyntax ArgumentName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "Argument"); }
        }
        //global::System.Activities.InArgument
        internal static QualifiedNameSyntax InArgumentName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "InArgument"); }
        }
        //global::System.Activities.OutArgument
        internal static QualifiedNameSyntax OutArgumentName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "OutArgument"); }
        }
        //global::System.Activities.ArgumentDirection
        internal static MemberAccessExpressionSyntax ArgumentDirectionExpr {
            get { return CS.MemberAccessExpr(GlobalSystemActivitiesExpr, "ArgumentDirection"); }
        }
        //global::System.Activities.ArgumentDirection.In
        internal static MemberAccessExpressionSyntax ArgumentDirectionInExpr {
            get { return CS.MemberAccessExpr(ArgumentDirectionExpr, "In"); }
        }

        //global::System.Activities.RuntimeArgument
        internal static QualifiedNameSyntax RuntimeArgumentName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "RuntimeArgument"); }
        }

        //global::System.Activities.Location<T>
        internal static QualifiedNameSyntax LocationOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesName, CS.GenericName("Location", type));
        }
        //global::System.Activities.RequiredArgumentAttribute 
        internal static QualifiedNameSyntax RequiredArgumentAttributeName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "RequiredArgumentAttribute"); }
        }
        internal static AttributeListSyntax RequiredArgumentAttributeList {
            get { return CS.AttributeList(RequiredArgumentAttributeName); }
        }

        //global::System.Activities.ActivityContext
        internal static QualifiedNameSyntax ActivityContextName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "ActivityContext"); }
        }
        //global::System.Activities.CodeActivityContext
        internal static QualifiedNameSyntax CodeActivityContextName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "CodeActivityContext"); }
        }
        //global::System.Activities.NativeActivityContext
        internal static QualifiedNameSyntax NativeActivityContextName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "NativeActivityContext"); }
        }
        //global::System.Activities.ActivityInstance
        internal static QualifiedNameSyntax ActivityInstanceName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "ActivityInstance"); }
        }
        //global::System.Activities.CompletionCallback
        internal static QualifiedNameSyntax CompletionCallbackName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "CompletionCallback"); }
        }
        //global::System.Activities.CompletionCallback<T>
        internal static QualifiedNameSyntax CompletionCallbackOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesName, CS.GenericName("CompletionCallback", type));
        }
        //global::System.Activities.CodeActivity
        internal static QualifiedNameSyntax CodeActivityName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "CodeActivity"); }
        }
        //global::System.Activities.CodeActivity<T>
        internal static QualifiedNameSyntax CodeActivityOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesName, CS.GenericName("CodeActivity", type));
        }
        //global::System.Activities.CodeActivityMetadata
        internal static QualifiedNameSyntax CodeActivityMetadataName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "CodeActivityMetadata"); }
        }
        //global::System.Activities.NativeActivityMetadata
        internal static QualifiedNameSyntax NativeActivityMetadataName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "NativeActivityMetadata"); }
        }

        //global::System.Activities.ActivityAction
        internal static QualifiedNameSyntax ActivityActionName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "ActivityAction"); }
        }
        //global::System.Activities.ActivityAction<T1, T2>
        internal static QualifiedNameSyntax ActivityActionOf(IEnumerable<TypeSyntax> types) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesName, CS.GenericName("ActivityAction", types));
        }
        //global::System.Activities.ActivityFunc<T1, T2, TResult>
        internal static QualifiedNameSyntax ActivityFuncOf(IEnumerable<TypeSyntax> types) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesName, CS.GenericName("ActivityFunc", types));
        }
        //global::System.Activities.Expressions
        internal static QualifiedNameSyntax GlobalSystemActivitiesExpressionsName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "Expressions"); }
        }
        //global::System.Activities.Expressions.ActivityFunc<T1, T2, TResult>
        internal static QualifiedNameSyntax InvokeFuncOf(IEnumerable<TypeSyntax> types) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesExpressionsName, CS.GenericName("InvokeFunc", types));
        }
        //global::System.Activities.Statements
        internal static QualifiedNameSyntax GlobalSystemActivitiesStatementsName {
            get { return CS.QualifiedName(GlobalSystemActivitiesName, "Statements"); }
        }
        //global::System.Activities.Statements.InvokeAction
        internal static QualifiedNameSyntax InvokeActionName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "InvokeAction"); }
        }
        //global::System.Activities.Statements.ActivityAction<T1, T2>
        internal static QualifiedNameSyntax InvokeActionOf(IEnumerable<TypeSyntax> types) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesStatementsName, CS.GenericName("InvokeAction", types));
        }
        //global::System.Activities.Statements.Sequence
        internal static QualifiedNameSyntax SequenceName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "Sequence"); }
        }
        //global::System.Activities.Statements.If
        internal static QualifiedNameSyntax IfName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "If"); }
        }
        //global::System.Activities.Statements.While
        internal static QualifiedNameSyntax WhileName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "While"); }
        }
        //global::System.Activities.Statements.DoWhile
        internal static QualifiedNameSyntax DoWhileName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "DoWhile"); }
        }
        //global::System.Activities.Statements.Switch<T>
        internal static QualifiedNameSyntax SwitchOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesStatementsName, CS.GenericName("Switch", type));
        }
        //global::System.Activities.Statements.ForEach<T>
        internal static QualifiedNameSyntax ForEachOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesStatementsName, CS.GenericName("ForEach", type));
        }
        //global::System.Activities.Statements.ParallelForEach<T>
        internal static QualifiedNameSyntax ParallelForEachOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesStatementsName, CS.GenericName("ParallelForEach", type));
        }
        //global::System.Activities.Statements.Parallel
        internal static QualifiedNameSyntax ParallelName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "Parallel"); }
        }
        //global::System.Activities.Statements.Pick
        internal static QualifiedNameSyntax PickName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "Pick"); }
        }
        //global::System.Activities.Statements.PickBranch
        internal static QualifiedNameSyntax PickBranchName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "PickBranch"); }
        }
        //global::System.Activities.Statements.Flowchart
        internal static QualifiedNameSyntax FlowchartName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "Flowchart"); }
        }
        //global::System.Activities.Statements.FlowStep
        internal static QualifiedNameSyntax FlowStepName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "FlowStep"); }
        }
        //global::System.Activities.Statements.FlowDecision
        internal static QualifiedNameSyntax FlowDecisionName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "FlowDecision"); }
        }
        //global::System.Activities.Statements.FlowSwitch<T>
        internal static QualifiedNameSyntax FlowSwitchOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesStatementsName, CS.GenericName("FlowSwitch", type));
        }
        //global::System.Activities.Statements.StateMachine
        internal static QualifiedNameSyntax StateMachineName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "StateMachine"); }
        }
        //global::System.Activities.Statements.State
        internal static QualifiedNameSyntax StateName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "State"); }
        }
        //global::System.Activities.Statements.Transition
        internal static QualifiedNameSyntax TransitionName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "Transition"); }
        }
        //global::System.Activities.Statements.TransactionScope
        internal static QualifiedNameSyntax TransactionScopeName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "TransactionScope"); }
        }
        //global::System.Activities.Statements.Delay
        internal static QualifiedNameSyntax DelayName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "Delay"); }
        }
        //global::System.Activities.Statements.Throw
        internal static QualifiedNameSyntax ThrowName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "Throw"); }
        }
        //global::System.Activities.Statements.Rethrow
        internal static QualifiedNameSyntax RethrowName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "Rethrow"); }
        }
        //global::System.Activities.Statements.TryCatch
        internal static QualifiedNameSyntax TryCatchName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "TryCatch"); }
        }
        //global::System.Activities.Statements.Catch<T>
        internal static QualifiedNameSyntax CatchOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemActivitiesStatementsName, CS.GenericName("Catch", type));
        }
        //global::System.Activities.Statements.CancellationScope
        internal static QualifiedNameSyntax CancellationScopeName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "CancellationScope"); }
        }
        //global::System.Activities.Statements.CompensableActivity
        internal static QualifiedNameSyntax CompensableActivityName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "CompensableActivity"); }
        }
        //global::System.Activities.Statements.CompensationToken
        internal static QualifiedNameSyntax CompensationTokenName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "CompensationToken"); }
        }
        //global::System.Activities.Statements.Confirm
        internal static QualifiedNameSyntax ConfirmName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "Confirm"); }
        }
        //global::System.Activities.Statements.Compensate
        internal static QualifiedNameSyntax CompensateName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "Compensate"); }
        }
        //global::System.Activities.Statements.Persist
        internal static QualifiedNameSyntax PersistName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "Persist"); }
        }
        //global::System.Activities.Statements.NoPersistScope
        internal static QualifiedNameSyntax NoPersistScopeName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "NoPersistScope"); }
        }
        //global::System.Activities.Statements.TerminateWorkflow
        internal static QualifiedNameSyntax TerminateWorkflowName {
            get { return CS.QualifiedName(GlobalSystemActivitiesStatementsName, "TerminateWorkflow"); }
        }

        //global::System.ServiceModel
        internal static QualifiedNameSyntax GlobalSystemServiceModelName {
            get { return CS.QualifiedName(CS.GlobalSystemName, "ServiceModel"); }
        }
        //global::System.ServiceModel.MessageQuerySet
        internal static QualifiedNameSyntax MessageQuerySetName {
            get { return CS.QualifiedName(GlobalSystemServiceModelName, "MessageQuerySet"); }
        }
        //global::System.ServiceModel.Activities
        internal static QualifiedNameSyntax GlobalSystemServiceModelActivitiesName {
            get { return CS.QualifiedName(GlobalSystemServiceModelName, "Activities"); }
        }
        //global::System.ServiceModel.Activities.Receive
        internal static QualifiedNameSyntax ReceiveName {
            get { return CS.QualifiedName(GlobalSystemServiceModelActivitiesName, "Receive"); }
        }
        //global::System.ServiceModel.Activities.SendReply
        internal static QualifiedNameSyntax SendReplyName {
            get { return CS.QualifiedName(GlobalSystemServiceModelActivitiesName, "SendReply"); }
        }
        //global::System.ServiceModel.Activities.Send
        internal static QualifiedNameSyntax SendName {
            get { return CS.QualifiedName(GlobalSystemServiceModelActivitiesName, "Send"); }
        }
        //global::System.ServiceModel.Activities.ReceiveReply
        internal static QualifiedNameSyntax ReceiveReplyName {
            get { return CS.QualifiedName(GlobalSystemServiceModelActivitiesName, "ReceiveReply"); }
        }
        //global::System.ServiceModel.Activities.ReceiveMessageContent
        internal static QualifiedNameSyntax ReceiveMessageContentName {
            get { return CS.QualifiedName(GlobalSystemServiceModelActivitiesName, "ReceiveMessageContent"); }
        }
        //global::System.ServiceModel.Activities.ReceiveParametersContent
        internal static QualifiedNameSyntax ReceiveParametersContentName {
            get { return CS.QualifiedName(GlobalSystemServiceModelActivitiesName, "ReceiveParametersContent"); }
        }
        //global::System.ServiceModel.Activities.SendMessageContent
        internal static QualifiedNameSyntax SendMessageContentName {
            get { return CS.QualifiedName(GlobalSystemServiceModelActivitiesName, "SendMessageContent"); }
        }
        //global::System.ServiceModel.Activities.SendParametersContent
        internal static QualifiedNameSyntax SendParametersContentName {
            get { return CS.QualifiedName(GlobalSystemServiceModelActivitiesName, "SendParametersContent"); }
        }
        //global::System.ServiceModel.Activities.CorrelationHandle
        internal static QualifiedNameSyntax CorrelationHandleName {
            get { return CS.QualifiedName(GlobalSystemServiceModelActivitiesName, "CorrelationHandle"); }
        }
        //global::System.ServiceModel.Activities.RequestReplyCorrelationInitializer
        internal static QualifiedNameSyntax RequestReplyCorrelationInitializerName {
            get { return CS.QualifiedName(GlobalSystemServiceModelActivitiesName, "RequestReplyCorrelationInitializer"); }
        }
        //global::System.ServiceModel.Activities.QueryCorrelationInitializer
        internal static QualifiedNameSyntax QueryCorrelationInitializerName {
            get { return CS.QualifiedName(GlobalSystemServiceModelActivitiesName, "QueryCorrelationInitializer"); }
        }
        //global::System.ServiceModel.Activities.CallbackCorrelationInitializer
        internal static QualifiedNameSyntax CallbackCorrelationInitializerName {
            get { return CS.QualifiedName(GlobalSystemServiceModelActivitiesName, "CallbackCorrelationInitializer"); }
        }
        //global::System.ServiceModel.Activities.ContextCorrelationInitializer
        internal static QualifiedNameSyntax ContextCorrelationInitializerName {
            get { return CS.QualifiedName(GlobalSystemServiceModelActivitiesName, "ContextCorrelationInitializer"); }
        }
        //global::System.ServiceModel.Activities.InitializeCorrelation
        internal static QualifiedNameSyntax InitializeCorrelationName {
            get { return CS.QualifiedName(GlobalSystemServiceModelActivitiesName, "InitializeCorrelation"); }
        }
        //global::System.ServiceModel.Activities.TransactedReceiveScope
        internal static QualifiedNameSyntax TransactedReceiveScopeName {
            get { return CS.QualifiedName(GlobalSystemServiceModelActivitiesName, "TransactedReceiveScope"); }
        }

        //
        //
        //global::MetahWFuncActivity<T>
        internal static AliasQualifiedNameSyntax MetahWFuncActivityOf(TypeSyntax type) {
            return CS.GlobalAliasQualifiedName(CS.GenericName("MetahWFuncActivity", type));
        }
        //global::MetahWSequenceActivity<T>
        internal static AliasQualifiedNameSyntax MetahWSequenceActivityOf(TypeSyntax type) {
            return CS.GlobalAliasQualifiedName(CS.GenericName("MetahWSequenceActivity", type));
        }
        //global::MetahWSequenceActivity
        internal static AliasQualifiedNameSyntax MetahWSequenceActivityName {
            get { return CS.GlobalAliasQualifiedName("MetahWSequenceActivity"); }
        }
        //global::MetahWLocationActivity<T>
        internal static AliasQualifiedNameSyntax MetahWLocationActivityOf(TypeSyntax type) {
            return CS.GlobalAliasQualifiedName(CS.GenericName("MetahWLocationActivity", type));
        }
        //global::MetahWWrapperActivity<T>
        internal static AliasQualifiedNameSyntax MetahWWrapperActivityOf(TypeSyntax type) {
            return CS.GlobalAliasQualifiedName(CS.GenericName("MetahWWrapperActivity", type));
        }
        //global::MetahWActionActivity
        internal static AliasQualifiedNameSyntax MetahWActionActivityName {
            get { return CS.GlobalAliasQualifiedName("MetahWActionActivity"); }
        }
        //
        //
        //
        //>store.Get(__ctx__)
        internal static ExpressionSyntax StoreGet(ExpressionSyntax store) {
            return CS.InvoExpr(CS.MemberAccessExpr(store, "Get"), CS.IdName("__ctx__"));
        }
        internal static ExpressionSyntax StoreGet(Store store) {
            return StoreGet(store.Name.CSIdName);
        }
        //>store.Set(__ctx__, value)
        internal static ExpressionSyntax StoreSet(ExpressionSyntax store, ExpressionSyntax value) {
            return CS.InvoExpr(CS.MemberAccessExpr(store, "Set"), CS.IdName("__ctx__"), value);
        }
        //>store.SetEx(__ctx__, value)
        internal static ExpressionSyntax StoreSetEx(ExpressionSyntax store, ExpressionSyntax value) {
            return CS.InvoExpr(CS.MemberAccessExpr(store, "SetEx"), CS.IdName("__ctx__"), value);
        }
        internal static ExpressionSyntax StoreSetEx(Store store, ExpressionSyntax value) {
            return StoreSetEx(store.Name.CSIdName, value);
        }
        //>store.SetEx(__ctx__, __val__ => ++__val__, true)
        internal static ExpressionSyntax StoreSetEx(Store store, bool isInc, bool isPost) {
            return CS.InvoExpr(
                CS.MemberAccessExpr(store.Name.CSIdName, "SetEx"),
                CS.IdName("__ctx__"),
                CS.SimpleLambdaExpr("__val__", isInc ? CS.PreIncrementExpr(CS.IdName("__val__")) : CS.PreDecrementExpr(CS.IdName("__val__"))),
                CS.Literal(isPost)
                );
        }
        //>new MeathWActionActivity(__ctx__ => block)
        internal static ExpressionSyntax NewMetahWActionActivity(BlockSyntax block) {
            return CS.NewObjWithLambdaArgExpr(MetahWActionActivityName, "__ctx__", block);
        }
        internal static ExpressionSyntax NewMetahWActionActivity(IEnumerable<StatementSyntax> stms) {
            return NewMetahWActionActivity(SyntaxFactory.Block(stms));
        }
        //>new MeathWFuncActivity<TYPE>(__ctx__ => BODYEXPR)
        internal static ExpressionSyntax NewMetahWFuncActivity(TypeSyntax type, ExpressionSyntax lambdaBodyExpr) {
            return CS.NewObjWithLambdaArgExpr(MetahWFuncActivityOf(type), "__ctx__", lambdaBodyExpr);
        }
        //>new MetahWSequenceActivity<TYPE>(__activity__ => { STM1; STM2; })
        internal static ExpressionSyntax MetahWSequenceActivity(TypeSyntax type, IEnumerable<StatementSyntax> stms) {
            return CS.NewObjWithLambdaArgExpr(MetahWSequenceActivityOf(type), "__activity__", stms);
        }
        //new MetahWSequenceActivity(__activity__ => { STM1; STM2; })
        internal static ExpressionSyntax MetahWSequenceActivity(IEnumerable<StatementSyntax> stms) {
            return CS.NewObjWithLambdaArgExpr(MetahWSequenceActivityName, "__activity__", stms);
        }

        //>new InArgument<TYPE>(new MeathWFuncActivity<TYPE>(__ctx__ => BODYEXPR))
        internal static ExpressionSyntax NewInArgumentWithFuncActivity(TypeSyntax type, ExpressionSyntax lambdaBodyExpr) {
            return NewInArgument(type, NewMetahWFuncActivity(type, lambdaBodyExpr));
        }
        //>new InArgument<TYPE>(expr)
        internal static ExpressionSyntax NewInArgument(TypeSyntax type, ExpressionSyntax expr) {
            return CS.NewObjExpr(InArgumentOf(type), expr);
        }
        internal static ExpressionSyntax NewInArgument(TypeSyntax type, Store store) {
            if (store is Variable) {
                return NewInArgument(type, store.Name.CSIdName);
            }
            return NewInArgumentWithFuncActivity(type, StoreGet(store));
        }
        internal static ExpressionSyntax NewInArgument(TypeSyntax type, object storeOrExpr) {
            var store = storeOrExpr as Store;
            if (store != null) return NewInArgument(type, store);
            return NewInArgument(type, (ExpressionSyntax)storeOrExpr);
        }
        //>new InOutArgument<TYPE>(new MetahWLocationActivity<TYPE>(storeName))
        private static ExpressionSyntax NewOutOrRefArgument(TypeSyntax type, bool isOut, IdentifierNameSyntax storeName) {
            return CS.NewObjExpr(isOut ? OutArgumentOf(type) : InOutArgumentOf(type), CS.NewObjExpr(MetahWLocationActivityOf(type), storeName));
        }
        internal static ExpressionSyntax NewOutArgument(TypeSyntax type, IdentifierNameSyntax storeName) {
            return NewOutOrRefArgument(type, true, storeName);
        }
        internal static ExpressionSyntax NewOutOrRefArgument(TypeSyntax type, bool isOut, Store store) {
            return NewOutOrRefArgument(type, isOut, store.Name.CSIdName);
        }
        internal static ExpressionSyntax NewOutArgument(TypeSyntax type, Store store) {
            return NewOutOrRefArgument(type, true, store);
        }
        internal static ExpressionSyntax NewVariable(TypeSyntax type) {
            return CS.NewObjExpr(VariableOf(type));
        }
        //
        //
        //
        //
        private static NamespaceDeclarationSyntax SystemAction1718 {
            get {
                return SyntaxFactory.NamespaceDeclaration(CS.IdName("System"), default(SyntaxList<ExternAliasDirectiveSyntax>),
                    default(SyntaxList<UsingDirectiveSyntax>), SyntaxFactory.List<MemberDeclarationSyntax>(new[] { Action(17), Action(18) }));
            }
        }
        private static DelegateDeclarationSyntax Action(int count) {
            //>internal delegate void Action<in T1, in T2, ...>(T1 arg1, T2 arg2, ...);
            return SyntaxFactory.DelegateDeclaration(default(SyntaxList<AttributeListSyntax>), CS.InternalTokenList, CS.VoidType, CS.Id("Action"),
                CS.TypeParameterList(GetInTypeParameters(count)), CS.ParameterList(GetParameters(count)), default(SyntaxList<TypeParameterConstraintClauseSyntax>));
        }
        private static IEnumerable<TypeParameterSyntax> GetInTypeParameters(int count) {
            for (var i = 1; i <= count; i++) {
                yield return SyntaxFactory.TypeParameter(default(SyntaxList<AttributeListSyntax>), CS.InToken, CS.Id("T" + i.ToInvariantString()));
            }
        }
        private static IEnumerable<TypeParameterSyntax> GetTypeParameters(int count, bool hasResult) {
            for (var i = 1; i <= count; i++) {
                yield return SyntaxFactory.TypeParameter("T" + i.ToInvariantString());
            }
            if (hasResult) {
                yield return SyntaxFactory.TypeParameter("TResult");
            }
        }
        private static IEnumerable<ParameterSyntax> GetParameters(int count) {
            for (var i = 1; i <= count; i++) {
                var iStr = i.ToInvariantString();
                yield return CS.Parameter(CS.IdName("T" + iStr), "arg" + iStr);
            }
        }
        private static IEnumerable<TypeSyntax> GetTypes(int count, bool hasResult) {
            for (var i = 1; i <= count; i++) {
                yield return CS.IdName("T" + i.ToInvariantString());
            }
            if (hasResult) {
                yield return CS.IdName("TResult");
            }
        }
        internal static QualifiedNameSyntax ActivityActionOf(int count) {
            return ActivityActionOf(GetTypes(count, false));
        }
        internal static QualifiedNameSyntax ActivityFuncOf(int count) {
            return ActivityFuncOf(GetTypes(count, true));
        }
        //
        //
        private static MethodDeclarationSyntax SetEx(StoreKind kind) {
            //>internal static T SetEx<T>(this InArgument<T> argOrVar, ActivityContext context, T value) {
            //>    if (argOrVar == null) throw new ArgumentNullException("argOrVar");
            //>    argOrVar.Set(context, value);
            //>    return value;
            //>}
            return CS.Method(
                attributeLists: null,
                modifiers: CS.InternalStaticTokenList,
                returnType: CS.IdName("T"),
                identifier: CS.Id("SetEx"),
                typeParameters: new[] { SyntaxFactory.TypeParameter("T") },
                parameters: new[] {
                    CS.ThisParameter(kind == StoreKind.InParameter? InArgumentOf(CS.IdName("T"))
                        : kind == StoreKind.OutParameter? OutArgumentOf(CS.IdName("T"))
                        : kind == StoreKind.RefParameter? InOutArgumentOf(CS.IdName("T"))
                        : VariableOf(CS.IdName("T"))
                        , "argOrVar"),
                    CS.Parameter(ActivityContextName, "context"),
                    CS.Parameter(CS.IdName("T"), "value")
               },
               constraintClauses: null,
               statements: new StatementSyntax[] {
                   CS.IfNullThrowArgumentNull("argOrVar"),
                   CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("argOrVar"), "Set"),
                           CS.IdName("context"),CS.IdName("value"))),
                   CS.ReturnStm(CS.IdName("value"))
               }
            );
        }
        private static MethodDeclarationSyntax SetEx2(StoreKind kind) {
            //>internal static T SetEx<T>(this InArgument<T> argOrVar, ActivityContext context, Func<T, T> func, bool isPost) {
            //>    if (argOrVar == null) throw new ArgumentNullException("argOrVar");
            //>    if (func == null) throw new ArgumentNullException("func");
            //>    var oldValue = argOrVar.Get(context);
            //>    var newValue = func(oldValue);
            //>    argOrVar.Set(context, newValue);
            //>    if (isPost) return oldValue;
            //>    return newValue;
            //>}
            return CS.Method(
                attributeLists: null,
                modifiers: CS.InternalStaticTokenList,
                returnType: CS.IdName("T"),
                identifier: CS.Id("SetEx"),
                typeParameters: new[] { SyntaxFactory.TypeParameter("T") },
                parameters: new[] {
                    CS.ThisParameter(kind == StoreKind.InParameter? InArgumentOf(CS.IdName("T"))
                        : kind == StoreKind.OutParameter? OutArgumentOf(CS.IdName("T"))
                        : kind == StoreKind.RefParameter? InOutArgumentOf(CS.IdName("T"))
                        : VariableOf(CS.IdName("T"))
                        , "argOrVar"),
                    CS.Parameter(ActivityContextName, "context"),
                    CS.Parameter(CS.FuncOf(CS.IdName("T"), CS.IdName("T")), "func"),
                    CS.Parameter(CS.BoolType, "isPost")
               },
               constraintClauses: null,
               statements: new StatementSyntax[] {
                   CS.IfNullThrowArgumentNull("argOrVar"),
                   CS.IfNullThrowArgumentNull("func"),
                   CS.LocalDeclStm(CS.VarIdName, "oldValue",
                       CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("argOrVar"), "Get"),
                           CS.IdName("context"))),
                   CS.LocalDeclStm(CS.VarIdName, "newValue",
                       CS.InvoExpr(CS.IdName("func"), CS.IdName("oldValue"))),
                   CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("argOrVar"), "Set"),
                           CS.IdName("context"),CS.IdName("newValue"))),
                   SyntaxFactory.IfStatement(CS.IdName("isPost"), CS.ReturnStm(CS.IdName("oldValue"))),
                   CS.ReturnStm(CS.IdName("newValue"))
               }
            );
        }
        private static MethodDeclarationSyntax ToDelegate(bool isFunc, int count) {
            //>internal static ActivityAction<T1, T2> ToAction<TActivity, T1, T2>(this TActivity activity, Action<TActivity, InArgument<T1>, InArgument<T2>> initializer) where TActivity : Activity {
            //    if (activity == null) throw new ArgumentNullException("activity");
            //    if (initializer == null) throw new ArgumentNullException("initializer");
            //    var darg1 = new DelegateInArgument<T1>();
            //    var darg2 = new DelegateInArgument<T2>();
            //    initializer(activity, new InArgument<T1>(darg1), new InArgument<T2>(darg2));
            //    return new ActivityAction<T1, T2> {
            //        Handler = activity,
            //        Argument1 = darg1,
            //        Argument2 = darg2
            //    };
            //}
            //>internal static ActivityFunc<T1, T2, TResult> ToFunc<TActivity, T1, T2, TResult>(this TActivity activity, Action<TActivity, InArgument<T1>, InArgument<T2>, OutArgument<TResult>> initializer) where TActivity : Activity {
            //    if (activity == null) throw new ArgumentNullException("activity");
            //    if (initializer == null) throw new ArgumentNullException("initializer");
            //    var darg1 = new DelegateInArgument<T1>();
            //    var darg2 = new DelegateInArgument<T2>();
            //    var dres = new DelegateOutArgument<TResult>();
            //    initializer(activity, new InArgument<T1>(darg1), new InArgument<T2>(darg2), new OutArgument<TResult>(dres));
            //    return new ActivityFunc<T1, T2, TResult> {
            //        Handler = activity,
            //        Argument1 = darg1,
            //        Argument2 = darg2,
            //        Result = dres
            //    };
            //}
            var delegateType = isFunc ? ActivityFuncOf(count) : ActivityActionOf(count);
            var stmList = new List<StatementSyntax>();
            stmList.Add(CS.IfNullThrowArgumentNull("activity"));
            stmList.Add(CS.IfNullThrowArgumentNull("initializer"));
            for (var i = 1; i <= count; i++) {
                var iStr = i.ToInvariantString();
                stmList.Add(CS.LocalDeclStm(CS.VarIdName, "darg" + iStr, CS.NewObjExpr(DelegateInArgumentOf(CS.IdName("T" + iStr)))));
            }
            if (isFunc) {
                stmList.Add(CS.LocalDeclStm(CS.VarIdName, "dres", CS.NewObjExpr(DelegateOutArgumentOf(CS.IdName("TResult")))));
            }
            stmList.Add(CS.ExprStm(CS.InvoExpr(CS.IdName("initializer"), GetToDelegateInitializerArguments(count, isFunc))));
            stmList.Add(CS.ReturnStm(CS.NewObjExpr(delegateType, null, GetToDelegateNewDelegateInits(count, isFunc))));
            return CS.Method(
                attributeLists: null,
                modifiers: CS.InternalStaticTokenList,
                returnType: delegateType,
                identifier: CS.Id(isFunc ? "ToFunc" : "ToAction"),
                typeParameters: GetToDelegateTypeParameters(count, isFunc),
                parameters: new ParameterSyntax[] {
                    CS.ThisParameter(CS.IdName("TActivity"), "activity"),
                    CS.Parameter(CS.ActionOf(GetToDelegateInitializerTypes(count, isFunc)), "initializer")
                },
                constraintClauses: new[] { CS.TypeParameterConstraintClause(CS.IdName("TActivity"), ActivityName) },
                statements: stmList
            );
        }
        private static IEnumerable<TypeParameterSyntax> GetToDelegateTypeParameters(int max, bool hasResult) {
            yield return SyntaxFactory.TypeParameter("TActivity");
            for (var i = 1; i <= max; i++) {
                yield return SyntaxFactory.TypeParameter("T" + i.ToInvariantString());
            }
            if (hasResult) {
                yield return SyntaxFactory.TypeParameter("TResult");
            }
        }
        private static IEnumerable<TypeSyntax> GetToDelegateInitializerTypes(int count, bool hasResult) {
            yield return CS.IdName("TActivity");
            for (var i = 1; i <= count; i++) {
                yield return InArgumentOf(CS.IdName("T" + i.ToInvariantString()));
            }
            if (hasResult) {
                yield return OutArgumentOf(CS.IdName("TResult"));
            }
        }
        private static IEnumerable<ExpressionSyntax> GetToDelegateInitializerArguments(int count, bool hasResult) {
            yield return CS.IdName("activity");
            for (var i = 1; i <= count; i++) {
                var iStr = i.ToInvariantString();
                yield return CS.NewObjExpr(InArgumentOf(CS.IdName("T" + iStr)), CS.IdName("darg" + iStr));
            }
            if (hasResult) {
                yield return CS.NewObjExpr(OutArgumentOf(CS.IdName("TResult")), CS.IdName("dres"));
            }
        }
        private static IEnumerable<ExpressionSyntax> GetToDelegateNewDelegateInits(int count, bool hasResult) {
            yield return CS.AssignExpr(CS.IdName("Handler"), CS.IdName("activity"));
            if (count == 1) {
                yield return CS.AssignExpr(CS.IdName("Argument"), CS.IdName("darg1"));
            }
            else {
                for (var i = 1; i <= count; i++) {
                    var iStr = i.ToInvariantString();
                    yield return CS.AssignExpr(CS.IdName("Argument" + iStr), CS.IdName("darg" + iStr));
                }
            }
            if (hasResult) {
                yield return CS.AssignExpr(CS.IdName("Result"), CS.IdName("dres"));
            }
        }

        private static ClassDeclarationSyntax ExtensionsClass(bool includeImplMembers) {
            //>internal static class MetahWExtensions{ ... }
            var memberList = new List<MemberDeclarationSyntax>();
            if (includeImplMembers) {
                memberList.Add(SetEx(StoreKind.InParameter));
                memberList.Add(SetEx2(StoreKind.InParameter));
                memberList.Add(SetEx(StoreKind.OutParameter));
                memberList.Add(SetEx2(StoreKind.OutParameter));
                memberList.Add(SetEx(StoreKind.RefParameter));
                memberList.Add(SetEx2(StoreKind.RefParameter));
                memberList.Add(SetEx(StoreKind.Variable));
                memberList.Add(SetEx2(StoreKind.Variable));
                //>internal static T Initialize<T>(this T activity, Action<T> action) where T : Activity {
                //>    if (activity == null) throw new ArgumentNullException("activity");
                //>    if (action != null) action(activity);
                //>    return activity;
                //>}
                memberList.Add(CS.Method(null, CS.InternalStaticTokenList, CS.IdName("T"), CS.Id("Initialize"),
                    new[] { SyntaxFactory.TypeParameter("T") },
                    new[] { CS.ThisParameter(CS.IdName("T"), "activity"), CS.Parameter(CS.ActionOf(CS.IdName("T")), "action") },
                    new[] { CS.TypeParameterConstraintClause(CS.IdName("T"), ActivityName) },
                    new StatementSyntax[] {
                        CS.IfNullThrowArgumentNull("activity"),
                        SyntaxFactory.IfStatement(CS.NotEqualsExpr(CS.IdName("action"), CS.NullLiteral),
                            CS.ExprStm(CS.InvoExpr(CS.IdName("action"), CS.IdName("activity")))),
                        CS.ReturnStm(CS.IdName("activity"))
                    }));
                //>internal static TActivity Initialize<TDelegate, TActivity>(this TDelegate deleg, Func<TDelegate, TActivity> func)
                //>  where TDelegate : ActivityDelegate where TActivity : Activity {
                //>    //if (deleg == null) throw new ArgumentNullException("delegate");
                //>    if (func == null) throw new ArgumentNullException("func");
                //>    return func(deleg);
                //>}
                memberList.Add(CS.Method(null, CS.InternalStaticTokenList, CS.IdName("TActivity"), CS.Id("Initialize"),
                    new[] { SyntaxFactory.TypeParameter("TDelegate"), SyntaxFactory.TypeParameter("TActivity") },
                    new[] { CS.ThisParameter(CS.IdName("TDelegate"), "deleg"), CS.Parameter(CS.FuncOf(CS.IdName("TDelegate"), CS.IdName("TActivity")), "func") },
                    new[] { CS.TypeParameterConstraintClause(CS.IdName("TDelegate"), ActivityDelegateName), CS.TypeParameterConstraintClause(CS.IdName("TActivity"), ActivityName) },
                    new StatementSyntax[] {
                        //CS.IfNullThrowArgumentNull("deleg"),
                        CS.IfNullThrowArgumentNull("func"),
                        CS.ReturnStm(CS.InvoExpr(CS.IdName("func"), CS.IdName("deleg")))
                    }));

                //>private static readonly PropertyInfo _allowChainedEnvironmentAccessPropertyInfo = typeof(ActivityContext).GetProperty("AllowChainedEnvironmentAccess", BindingFlags.Instance | BindingFlags.NonPublic);
                memberList.Add(CS.Field(CS.PrivateStaticReadOnlyTokenList, CS.PropertyInfoName, "_allowChainedEnvironmentAccessPropertyInfo",
                                    CS.InvoExpr(CS.MemberAccessExpr(SyntaxFactory.TypeOfExpression(ActivityContextName), "GetProperty"),
                                        CS.Literal("AllowChainedEnvironmentAccess"),
                                        CS.Literal(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))));
                //>internal static void SetAllowChainedEnvironmentAccess(this ActivityContext context, bool value) {
                //>    _allowChainedEnvironmentAccessPropertyInfo.SetValue(context, value);
                //>}
                memberList.Add(CS.Method(CS.InternalStaticTokenList, CS.VoidType, "SetAllowChainedEnvironmentAccess",
                    new[] { CS.ThisParameter(ActivityContextName, "context"), CS.Parameter(CS.BoolType, "value") },
                    CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("_allowChainedEnvironmentAccessPropertyInfo"), "SetValue"),
                        CS.IdName("context"), CS.IdName("value")))));
            }
            //>internal static ActivityAction ToAction(this Activity activity) {
            //>    if (activity == null) throw new ArgumentNullException("activity");
            //>    return new ActivityAction { Handler = activity };
            //>}
            memberList.Add(CS.Method(CS.InternalStaticTokenList, ActivityActionName, "ToAction",
                        new[] { CS.ThisParameter(ActivityName, "activity") },
                        CS.IfNullThrowArgumentNull("activity"),
                        CS.ReturnStm(CS.NewObjExpr(ActivityActionName, null, new[] {
                    CS.AssignExpr(CS.IdName("Handler"),CS.IdName("activity")) })
                       )));
            for (var i = 1; i <= 16; i++) {
                memberList.Add(ToDelegate(false, i));
            }
            for (var i = 0; i <= 16; i++) {
                memberList.Add(ToDelegate(true, i));
            }
            return CS.Class(null, CS.InternalStaticTokenList, "MetahWExtensions", null, memberList);
        }

        //
        //global::MetahWModelActivityAttribute
        internal const string ModelActivityAttributeStringName = "MetahWModelActivityAttribute";
        internal static readonly string[] ModelActivityAttributeStringNames = { ModelActivityAttributeStringName };
        internal static AliasQualifiedNameSyntax ModelActivityAttributeName {
            get { return CS.GlobalAliasQualifiedName(ModelActivityAttributeStringName); }
        }
        internal static AttributeListSyntax ModelActivityAttributeList {
            get { return CS.AttributeList(ModelActivityAttributeName); }
        }
        //global::MetahWModelMethodAttribute
        internal const string ModelMethodAttributeStringName = "MetahWModelMethodAttribute";
        internal static readonly string[] ModelMethodAttributeStringNames = { ModelMethodAttributeStringName };
        internal static AliasQualifiedNameSyntax ModelMethodAttributeName {
            get { return CS.GlobalAliasQualifiedName(ModelMethodAttributeStringName); }
        }
        internal static AttributeListSyntax ModelMethodAttributeList(ModelMethodKind kind, IEnumerable<string> parameterNames) {
            return CS.AttributeList(ModelMethodAttributeName,
                SyntaxFactory.AttributeArgument(CS.Literal((int)kind)),
                SyntaxFactory.AttributeArgument(CS.NewArrExpr(CS.StringArrayType, parameterNames.Select(i => CS.Literal(i)))),
                SyntaxFactory.AttributeArgument(CS.Literal(-1))
                );
        }
        internal static AttributeListSyntax ModelMethodAttributeList(ModelMethodKind kind, int parameterCount) {
            return CS.AttributeList(ModelMethodAttributeName,
                SyntaxFactory.AttributeArgument(CS.Literal((int)kind)),
                SyntaxFactory.AttributeArgument(CS.NullLiteral),
                SyntaxFactory.AttributeArgument(CS.Literal(parameterCount))
                );
        }
        internal static void AddModelGlobalMembers(List<MemberDeclarationSyntax> memberList, bool forImports) {
            memberList.Add(SystemAction1718);
            memberList.Add(ExtensionsClass(false));
            if (forImports) {
                //>[AttributeUsage(AttributeTargets.Class)]
                //>internal sealed class MetahWModelActivityAttribute : Attribute { 
                //>}
                memberList.Add(CS.AttributeDecl(
                    modifiers: CS.InternalSealedTokenList,
                    identifier: CS.Id(ModelActivityAttributeStringName),
                    members: null,
                    validOn: AttributeTargets.Class));
            }
            else {
                //>[AttributeUsage(AttributeTargets.Method)]
                //>internal sealed class MetahWModelMethodAttribute : Attribute { 
                //>  internal MetahWModelMethodAttribute(int kind, string[] parameterNames, int parameterCount) {}
                //>}
                memberList.Add(CS.AttributeDecl(
                    modifiers: CS.InternalSealedTokenList,
                    identifier: CS.Id(ModelMethodAttributeStringName),
                    members: new[] {
                        CS.Constructor(CS.InternalTokenList, ModelMethodAttributeStringName, new[] {
                            CS.Parameter(CS.IntType, "kind"),
                            CS.Parameter(CS.StringArrayType, "parameterNames"),
                            CS.Parameter(CS.IntType, "parameterCount")
                            }, null) },
                    validOn: AttributeTargets.Method));

                //>internal static class MetahWModelExtensions{ ... }
                var meMemberList = new List<MemberDeclarationSyntax>();
                //>[MetahWModelMethodAttribute(ModelMethodKind.ActionInvoke, null, 0)]
                //>internal static void Invoke(this ActivityAction action) { throw new NotImplementedException(); }
                meMemberList.Add(CS.Method(new[] { ModelMethodAttributeList(ModelMethodKind.Action, 0) }, CS.InternalStaticTokenList, CS.VoidType,
                    CS.Id("Invoke"), new[] { CS.ThisParameter(ActivityActionName, "action") }, new[] { CS.ThrowNotImplemented }));
                for (var i = 1; i <= 16; i++) {
                    meMemberList.Add(InvokeDelegate(false, i));
                }
                for (var i = 0; i <= 16; i++) {
                    meMemberList.Add(InvokeDelegate(true, i));
                }
                memberList.Add(CS.Class(null, CS.InternalStaticTokenList, "MetahWModelExtensions", null, meMemberList));
            }
        }
        private static MethodDeclarationSyntax InvokeDelegate(bool isFunc, int count) {
            //>[MetahWModelMethodAttribute(ModelMethodKind.ActionInvoke, null, 2)]
            //>internal static void Invoke<T1, T2>(this ActivityAction<T1, T2> action, T1 arg1, T2 arg2) { throw new NotImplementedException(); }
            //>[MetahWModelMethodAttribute(ModelMethodKind.ActionInvoke, null, 2)]
            //>internal static TResult Invoke<T1, T2, TResult>(this ActivityFunc<T1, T2, TResult> func, T1 arg1, T2 arg2) { throw new NotImplementedException(); }
            return CS.Method(
                attributeLists: new[] { ModelMethodAttributeList(isFunc ? ModelMethodKind.Func : ModelMethodKind.Action, count) },
                modifiers: CS.InternalStaticTokenList,
                returnType: isFunc ? (TypeSyntax)CS.IdName("TResult") : CS.VoidType,
                identifier: CS.Id("Invoke"),
                typeParameters: GetTypeParameters(count, isFunc),
                parameters: GetInvokeDelegateParameters(isFunc, count),
                constraintClauses: null,
                statements: new[] { CS.ThrowNotImplemented }
            );
        }
        private static IEnumerable<ParameterSyntax> GetInvokeDelegateParameters(bool isFunc, int count) {
            if (isFunc) {
                yield return CS.ThisParameter(ActivityFuncOf(count), "func");
            }
            else {
                yield return CS.ThisParameter(ActivityActionOf(count), "action");
            }
            for (var i = 1; i <= count; i++) {
                var iStr = i.ToInvariantString();
                yield return CS.Parameter(CS.IdName("T" + iStr), "arg" + iStr);
            }
        }

        internal static void AddImplGlobalMembers(List<MemberDeclarationSyntax> memberList) {
            memberList.Add(SystemAction1718);
            memberList.Add(ExtensionsClass(true));
            //>internal sealed class MetahWFuncActivity<T> : CodeActivity<T> {
            //    internal MetahWFuncActivity(Func<ActivityContext, T> func) {
            //        if (func == null) throw new ArgumentNullException("func");
            //        Func = func;
            //    }
            //    internal readonly Func<ActivityContext, T> Func;
            //    protected override void CacheMetadata(CodeActivityMetadata metadata) { }
            //    protected override T Execute(CodeActivityContext context) {
            //        try {
            //            context.SetAllowChainedEnvironmentAccess(true);
            //            return Func(context);
            //        }
            //        finally {
            //            context.SetAllowChainedEnvironmentAccess(false);
            //        }
            //    }
            //}
            memberList.Add(CS.Class(null, CS.InternalSealedTokenList, CS.Id("MetahWFuncActivity"),
                new[] { SyntaxFactory.TypeParameter("T") },
                new[] { CodeActivityOf(CS.IdName("T")) },
                null,
                new MemberDeclarationSyntax[] {
                    CS.Constructor(CS.InternalTokenList, "MetahWFuncActivity",
                        new[] {CS.Parameter(CS.FuncOf(ActivityContextName, CS.IdName("T")), "func") },
                        null,
                        CS.IfNullThrowArgumentNull("func"),
                        CS.AssignStm(CS.IdName("Func"), CS.IdName("func"))),
                    CS.Field(CS.InternalReadOnlyTokenList, CS.FuncOf(ActivityContextName, CS.IdName("T")), "Func"),
                    CS.Method(CS.ProtectedOverrideTokenList, CS.VoidType, "CacheMetadata", new[] {CS.Parameter(CodeActivityMetadataName, "metadata") }),
                    CS.Method(CS.ProtectedOverrideTokenList, CS.IdName("T"), "Execute", new[] {CS.Parameter(CodeActivityContextName, "context") },
                        CS.TryFinallyStm(new StatementSyntax[] {
                                CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("context"), "SetAllowChainedEnvironmentAccess"), CS.TrueLiteral)),
                                CS.ReturnStm(CS.InvoExpr(CS.IdName("Func"), CS.IdName("context")))
                            },
                            new StatementSyntax[] {
                                CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("context"), "SetAllowChainedEnvironmentAccess"), CS.FalseLiteral)),
                            }
                        )
                    )
                }));
            //>internal sealed class MetahWActionActivity : CodeActivity {
            //    internal MetahWActionActivity(Action<ActivityContext> action) {
            //        Action = action;
            //    }
            //    internal readonly Action<ActivityContext> Action;
            //    protected override void CacheMetadata(CodeActivityMetadata metadata) { }
            //    protected override void Execute(CodeActivityContext context) {
            //        try {
            //            context.SetAllowChainedEnvironmentAccess(true);
            //            if (Action != null) Action(context);
            //        }
            //        finally {
            //            context.SetAllowChainedEnvironmentAccess(false);
            //        }
            //    }
            //}
            memberList.Add(CS.Class(null, CS.InternalSealedTokenList, CS.Id("MetahWActionActivity"),
                null,
                new[] { CodeActivityName },
                null,
                new MemberDeclarationSyntax[] {
                    CS.Constructor(CS.InternalTokenList, "MetahWActionActivity",
                        new[] {CS.Parameter(CS.ActionOf(ActivityContextName), "action") },
                        null,
                        CS.AssignStm(CS.IdName("Action"), CS.IdName("action"))),
                    CS.Field(CS.InternalReadOnlyTokenList, CS.ActionOf(ActivityContextName), "Action"),
                    CS.Method(CS.ProtectedOverrideTokenList, CS.VoidType, "CacheMetadata", new[] {CS.Parameter(CodeActivityMetadataName, "metadata") }),
                    CS.Method(CS.ProtectedOverrideTokenList, CS.VoidType, "Execute", new[] {CS.Parameter(CodeActivityContextName, "context") },
                        CS.TryFinallyStm(new StatementSyntax[] {
                                CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("context"), "SetAllowChainedEnvironmentAccess"), CS.TrueLiteral)),
                                SyntaxFactory.IfStatement(CS.NotEqualsExpr(CS.IdName("Action"), CS.NullLiteral),
                                    CS.ExprStm(CS.InvoExpr(CS.IdName("Action"), CS.IdName("context"))))
                            },
                            new StatementSyntax[] {
                                CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("context"), "SetAllowChainedEnvironmentAccess"), CS.FalseLiteral)),
                            }
                        )
                    )
                }));
            //>internal sealed class MetahWLocationActivity<T> : CodeActivity<Location<T>> {
            //    internal MetahWLocationActivity(Variable<T> variable) {
            //        if (variable == null) throw new ArgumentNullException("variable");
            //        Variable = variable;
            //    }
            //    internal MetahWLocationActivity(Argument argument) {
            //        if (argument == null) throw new ArgumentNullException("argument");
            //        Argument = argument;
            //    }
            //    internal readonly Variable<T> Variable;
            //    internal readonly Argument Argument;
            //    protected override void CacheMetadata(CodeActivityMetadata metadata) { }
            //    protected override Location<T> Execute(CodeActivityContext context) {
            //        try {
            //            context.SetAllowChainedEnvironmentAccess(true);
            //            if (Variable != null) return Variable.GetLocation(context);
            //            return (Location<T>)Argument.GetLocation(context);
            //        }
            //        finally {
            //            context.SetAllowChainedEnvironmentAccess(false);
            //        }
            //    }
            //}
            memberList.Add(CS.Class(null, CS.InternalSealedTokenList, CS.Id("MetahWLocationActivity"),
                new[] { SyntaxFactory.TypeParameter("T") },
                new[] { CodeActivityOf(LocationOf(CS.IdName("T"))) },
                null,
                new MemberDeclarationSyntax[] {
                    CS.Constructor(CS.InternalTokenList, "MetahWLocationActivity",
                        new[] { CS.Parameter(VariableOf(CS.IdName("T")), "variable") },
                        null,
                        CS.IfNullThrowArgumentNull("variable"),
                        CS.AssignStm(CS.IdName("Variable"), CS.IdName("variable"))),
                    CS.Constructor(CS.InternalTokenList, "MetahWLocationActivity",
                        new[] { CS.Parameter(ArgumentName, "argument") },
                        null,
                        CS.IfNullThrowArgumentNull("argument"),
                        CS.AssignStm(CS.IdName("Argument"), CS.IdName("argument"))),
                    CS.Field(CS.InternalReadOnlyTokenList, VariableOf(CS.IdName("T")), "Variable"),
                    CS.Field(CS.InternalReadOnlyTokenList, ArgumentName, "Argument"),
                    CS.Method(CS.ProtectedOverrideTokenList, CS.VoidType, "CacheMetadata", new[] {CS.Parameter(CodeActivityMetadataName, "metadata") }),
                    CS.Method(CS.ProtectedOverrideTokenList, LocationOf(CS.IdName("T")), "Execute", new[] {CS.Parameter(CodeActivityContextName, "context") },
                        CS.TryFinallyStm(new StatementSyntax[] {
                            CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("context"), "SetAllowChainedEnvironmentAccess"), CS.TrueLiteral)),
                            SyntaxFactory.IfStatement(CS.NotEqualsExpr(CS.IdName("Variable"), CS.NullLiteral),
                                CS.ReturnStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("Variable"), "GetLocation"), CS.IdName("context")))),
                            CS.ReturnStm(SyntaxFactory.CastExpression(LocationOf(CS.IdName("T")),
                                CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("Argument"), "GetLocation"), CS.IdName("context"))))
                            },
                            new StatementSyntax[] {
                                CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("context"), "SetAllowChainedEnvironmentAccess"), CS.FalseLiteral)),
                            }
                        )
                    )
                }));
            //>internal sealed class MetahWWrapperActivity<T> : NativeActivity {
            //    [RequiredArgument]
            //    public InArgument<T> Argument { get; set; }
            //    public Activity Child { get; set; }
            //    internal ActivityAction<T> ToAction() {
            //        return this.ToAction<MetahWWrapperActivity<T>, T>((ac, arg) => {
            //            ac.Argument = arg;
            //        });
            //    }
            //    protected override void CacheMetadata(NativeActivityMetadata metadata) {
            //        var rtArgument = new RuntimeArgument("Argument", typeof(T), ArgumentDirection.In, true);
            //        metadata.Bind(Argument, rtArgument);
            //        metadata.AddArgument(rtArgument);
            //        metadata.AddChild(Child);
            //    }
            //    protected override void Execute(NativeActivityContext context) {
            //        if (Child != null) context.ScheduleActivity(Child);
            //    }
            //}
            memberList.Add(CS.Class(null, CS.InternalSealedTokenList, CS.Id("MetahWWrapperActivity"),
                new[] { SyntaxFactory.TypeParameter("T") },
                new[] { NativeActivityName },
                null,
                new MemberDeclarationSyntax[] {
                    CS.Property(new[]{ RequiredArgumentAttributeList }, CS.PublicTokenList, InArgumentOf(CS.IdName("T")), CS.Id("Argument"), CS.GetSetAccessorList),
                    CS.Property(null, CS.PublicTokenList, ActivityName, CS.Id("Child"), CS.GetSetAccessorList),
                    CS.Method(CS.InternalTokenList, ActivityActionOf(new[] { CS.IdName("T") }), "ToAction", null, new StatementSyntax[] {
                        CS.ReturnStm(CS.InvoExpr(
                            CS.MemberAccessExpr(SyntaxFactory.ThisExpression(), CS.GenericName("ToAction", MetahWWrapperActivityOf(CS.IdName("T")), CS.IdName("T"))),
                            CS.ParedLambdaExpr(new[] {CS.Parameter("ac"), CS.Parameter("arg") },
                                SyntaxFactory.Block(CS.AssignStm(CS.MemberAccessExpr(CS.IdName("ac"), "Argument"), CS.IdName("arg"))))))
                    }),
                    CS.Method(CS.ProtectedOverrideTokenList, CS.VoidType, "CacheMetadata", new[] {CS.Parameter(NativeActivityMetadataName, "metadata") }, new StatementSyntax[] {
                        CS.LocalDeclStm(CS.VarIdName, "rtArgument", CS.NewObjExpr(RuntimeArgumentName, CS.Literal("Argument"), SyntaxFactory.TypeOfExpression(CS.IdName("T")), ArgumentDirectionInExpr, CS.Literal(true))),
                        CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("metadata"), "Bind"), CS.IdName("Argument"), CS.IdName("rtArgument"))),
                        CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("metadata"), "AddArgument"), CS.IdName("rtArgument"))),
                        CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("metadata"), "AddChild"), CS.IdName("Child"))),
                    }),
                    CS.Method(CS.ProtectedOverrideTokenList, CS.VoidType, "Execute", new[] {CS.Parameter(NativeActivityContextName, "context") },
                        SyntaxFactory.IfStatement(CS.NotEqualsExpr(CS.IdName("Child"), CS.NullLiteral),
                            CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("context"), "ScheduleActivity"), CS.IdName("Child"))))
                    )
                }
            ));
            //>internal sealed class MetahWSequenceActivity<T> : NativeActivity<T> {
            //    internal MetahWSequenceActivity(Action<global::MetahWSequenceActivity<T>> initializer) {
            //        if (initializer == null) throw new ArgumentNullException("initializer");
            //        Variables = new Collection<Variable>();
            //        Activities = new Collection<Activity>();
            //        initializer(this);
            //        if (Variables.Count == 0) throw new InvalidOperationException("Variables.Count == 0");
            //        if (Activities.Count == 0) throw new InvalidOperationException("Activities.Count == 0");
            //        _commonCallback = CommonCallback;
            //        _finalCallback = FinalCallback;
            //    }
            //    internal readonly Collection<Variable> Variables;
            //    internal readonly Collection<Activity> Activities;
            //    private readonly CompletionCallback _commonCallback;
            //    private readonly CompletionCallback<T> _finalCallback;
            //    protected override void CacheMetadata(NativeActivityMetadata metadata) {
            //        metadata.SetVariablesCollection(Variables);
            //        metadata.SetChildrenCollection(Activities);
            //    }
            //    protected override void Execute(NativeActivityContext context) {
            //        CommonCallback(context, null);
            //    }
            //    private void CommonCallback(NativeActivityContext context, ActivityInstance completedInstance) {
            //       try {
            //         context.SetAllowChainedEnvironmentAccess(true);
            //         var indexVar = (Variable<int>)Variables[0];
            //         var index = indexVar.Get(context);
            //         var child = Activities[index];
            //         if (index == Activities.Count - 1)
            //            context.ScheduleActivity<T>((Activity<T>)child, _finalCallback);
            //         else {
            //            context.ScheduleActivity(child, _commonCallback);
            //            indexVar.Set(context, index + 1);
            //         }
            //       }
            //       finally {
            //           context.SetAllowChainedEnvironmentAccess(false);
            //       }
            //    }
            //    private void FinalCallback(NativeActivityContext context, ActivityInstance completedInstance, T result) {
            //        Result.Set(context, result);
            //    }
            //}
            memberList.Add(CS.Class(null, CS.InternalSealedTokenList, CS.Id("MetahWSequenceActivity"),
                new[] { SyntaxFactory.TypeParameter("T") },
                new[] { NativeActivityOf(CS.IdName("T")) },
                null,
                new MemberDeclarationSyntax[] {
                    CS.Constructor(CS.InternalTokenList, "MetahWSequenceActivity",
                        new[] {CS.Parameter(CS.ActionOf(MetahWSequenceActivityOf(CS.IdName("T"))), "initializer") },
                        null,
                        CS.IfNullThrowArgumentNull("initializer"),
                        CS.AssignStm(CS.IdName("Variables"), CS.NewObjExpr(CS.CollectionOf(VariableName))),
                        CS.AssignStm(CS.IdName("Activities"), CS.NewObjExpr(CS.CollectionOf(ActivityName))),
                        CS.ExprStm(CS.InvoExpr(CS.IdName("initializer"), SyntaxFactory.ThisExpression())),
                        CS.IfThrowInvalidOperation(CS.EqualsExpr(CS.MemberAccessExpr(CS.IdName("Variables"), "Count"), CS.Literal(0)), "Variables.Count == 0"),
                        CS.IfThrowInvalidOperation(CS.EqualsExpr(CS.MemberAccessExpr(CS.IdName("Activities"), "Count"), CS.Literal(0)), "Activities.Count == 0"),
                        CS.AssignStm(CS.IdName("_commonCallback"), CS.IdName("CommonCallback")),
                        CS.AssignStm(CS.IdName("_finalCallback"), CS.IdName("FinalCallback"))
                        ),
                    CS.Field(CS.InternalReadOnlyTokenList, CS.CollectionOf(VariableName), "Variables"),
                    CS.Field(CS.InternalReadOnlyTokenList, CS.CollectionOf(ActivityName), "Activities"),
                    CS.Field(CS.PrivateReadOnlyTokenList, CompletionCallbackName, "_commonCallback"),
                    CS.Field(CS.PrivateReadOnlyTokenList, CompletionCallbackOf(CS.IdName("T")), "_finalCallback"),
                    CS.Method(CS.ProtectedOverrideTokenList, CS.VoidType, "CacheMetadata", new[] {CS.Parameter(NativeActivityMetadataName, "metadata") }, new StatementSyntax[] {
                        CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("metadata"), "SetVariablesCollection"), CS.IdName("Variables"))),
                        CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("metadata"), "SetChildrenCollection"), CS.IdName("Activities"))),
                    }),
                    CS.Method(CS.ProtectedOverrideTokenList, CS.VoidType, "Execute", new[] {CS.Parameter(NativeActivityContextName, "context") },
                        CS.ExprStm(CS.InvoExpr(CS.IdName("CommonCallback"), CS.IdName("context"), CS.NullLiteral))
                    ),
                    CS.Method(CS.PrivateTokenList, CS.VoidType, "CommonCallback", new[] {CS.Parameter(NativeActivityContextName, "context"), CS.Parameter(ActivityInstanceName, "completedInstance")},
                        CS.TryFinallyStm(new StatementSyntax[] {
                            CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("context"), "SetAllowChainedEnvironmentAccess"), CS.TrueLiteral)),
                            CS.LocalDeclStm(CS.VarIdName, "indexVar", CS.CastExpr(VariableOf(CS.IntType), CS.ElementAccessExpr(CS.IdName("Variables"), CS.Literal(0 )))),
                            CS.LocalDeclStm(CS.VarIdName, "index", CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("indexVar"), "Get"), CS.IdName("context"))),
                            CS.LocalDeclStm(CS.VarIdName, "child", CS.ElementAccessExpr(CS.IdName("Activities"), CS.IdName("index"))),
                            SyntaxFactory.IfStatement(CS.EqualsExpr(CS.IdName("index"), CS.SubtractExpr(CS.MemberAccessExpr(CS.IdName("Activities"), "Count"), CS.Literal(1))),
                                CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("context"), CS.GenericName("ScheduleActivity", CS.IdName("T"))),
                                    CS.CastExpr(ActivityOf(CS.IdName("T")), CS.IdName("child")), CS.IdName("_finalCallback"))),
                                SyntaxFactory.ElseClause(SyntaxFactory.Block(
                                    CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("context"), "ScheduleActivity"), CS.IdName("child"), CS.IdName("_commonCallback"))),
                                    CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("indexVar"), "Set"), CS.IdName("context"), CS.AddExpr(CS.IdName("index"), CS.Literal(1))))
                                ))
                            )},
                            new StatementSyntax[] {
                                CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("context"), "SetAllowChainedEnvironmentAccess"), CS.FalseLiteral)),
                            }
                        )
                    ),
                    CS.Method(CS.PrivateTokenList, CS.VoidType, "FinalCallback", new[] {CS.Parameter(NativeActivityContextName, "context"), CS.Parameter(ActivityInstanceName, "completedInstance"), CS.Parameter(CS.IdName("T"), "result")},
                        CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("Result"), "Set"), CS.IdName("context"), CS.IdName("result")))
                    ),
                }
            ));
            //>internal sealed class MetahWSequenceActivity : NativeActivity {
            //    internal MetahWSequenceActivity(Action<global::MetahWSequenceActivity> initializer) {
            //        if (initializer == null) throw new ArgumentNullException("initializer");
            //        Variables = new Collection<Variable>();
            //        Activities = new Collection<Activity>();
            //        initializer(this);
            //        if (Variables.Count == 0) throw new InvalidOperationException("Variables.Count == 0");
            //        if (Activities.Count == 0) throw new InvalidOperationException("Activities.Count == 0");
            //        _commonCallback = CommonCallback;
            //    }
            //    internal readonly Collection<Variable> Variables;
            //    internal readonly Collection<Activity> Activities;
            //    private readonly CompletionCallback _commonCallback;
            //    protected override void CacheMetadata(NativeActivityMetadata metadata) {
            //        metadata.SetVariablesCollection(Variables);
            //        metadata.SetChildrenCollection(Activities);
            //    }
            //    protected override void Execute(NativeActivityContext context) {
            //        CommonCallback(context, null);
            //    }
            //    private void CommonCallback(NativeActivityContext context, ActivityInstance completedInstance) {
            //       try {
            //         context.SetAllowChainedEnvironmentAccess(true);
            //         var indexVar = (Variable<int>)Variables[0];
            //         var index = indexVar.Get(context);
            //         if (index < Activities.Count) {
            //             context.ScheduleActivity(Activities[index], _commonCallback);
            //             indexVar.Set(context, index + 1);
            //         }
            //       }
            //       finally {
            //           context.SetAllowChainedEnvironmentAccess(false);
            //       }
            //    }
            //}
            memberList.Add(CS.Class(null, CS.InternalSealedTokenList, CS.Id("MetahWSequenceActivity"),
                null,
                new[] { NativeActivityName },
                null,
                new MemberDeclarationSyntax[] {
                    CS.Constructor(CS.InternalTokenList, "MetahWSequenceActivity",
                        new[] {CS.Parameter(CS.ActionOf(MetahWSequenceActivityName), "initializer") },
                        null,
                        CS.IfNullThrowArgumentNull("initializer"),
                        CS.AssignStm(CS.IdName("Variables"), CS.NewObjExpr(CS.CollectionOf(VariableName))),
                        CS.AssignStm(CS.IdName("Activities"), CS.NewObjExpr(CS.CollectionOf(ActivityName))),
                        CS.ExprStm(CS.InvoExpr(CS.IdName("initializer"), SyntaxFactory.ThisExpression())),
                        CS.IfThrowInvalidOperation(CS.EqualsExpr(CS.MemberAccessExpr(CS.IdName("Variables"), "Count"), CS.Literal(0)), "Variables.Count == 0"),
                        CS.IfThrowInvalidOperation(CS.EqualsExpr(CS.MemberAccessExpr(CS.IdName("Activities"), "Count"), CS.Literal(0)), "Activities.Count == 0"),
                        CS.AssignStm(CS.IdName("_commonCallback"), CS.IdName("CommonCallback"))
                        ),
                    CS.Field(CS.InternalReadOnlyTokenList, CS.CollectionOf(VariableName), "Variables"),
                    CS.Field(CS.InternalReadOnlyTokenList, CS.CollectionOf(ActivityName), "Activities"),
                    CS.Field(CS.PrivateReadOnlyTokenList, CompletionCallbackName, "_commonCallback"),
                    CS.Method(CS.ProtectedOverrideTokenList, CS.VoidType, "CacheMetadata", new[] {CS.Parameter(NativeActivityMetadataName, "metadata") }, new StatementSyntax[] {
                        CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("metadata"), "SetVariablesCollection"), CS.IdName("Variables"))),
                        CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("metadata"), "SetChildrenCollection"), CS.IdName("Activities"))),
                    }),
                    CS.Method(CS.ProtectedOverrideTokenList, CS.VoidType, "Execute", new[] {CS.Parameter(NativeActivityContextName, "context") },
                        CS.ExprStm(CS.InvoExpr(CS.IdName("CommonCallback"), CS.IdName("context"), CS.NullLiteral))
                    ),
                    CS.Method(CS.PrivateTokenList, CS.VoidType, "CommonCallback", new[] {CS.Parameter(NativeActivityContextName, "context"), CS.Parameter(ActivityInstanceName, "completedInstance")},
                        CS.TryFinallyStm(new StatementSyntax[] {
                            CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("context"), "SetAllowChainedEnvironmentAccess"), CS.TrueLiteral)),
                            CS.LocalDeclStm(CS.VarIdName, "indexVar", CS.CastExpr(VariableOf(CS.IntType), CS.ElementAccessExpr(CS.IdName("Variables"), CS.Literal(0)))),
                            CS.LocalDeclStm(CS.VarIdName, "index", CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("indexVar"), "Get"), CS.IdName("context"))),
                            SyntaxFactory.IfStatement(CS.LessThanExpr(CS.IdName("index"), CS.MemberAccessExpr(CS.IdName("Activities"), "Count")),
                                SyntaxFactory.Block(
                                    CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("context"), "ScheduleActivity"), CS.ElementAccessExpr(CS.IdName("Activities"), CS.IdName("index")), CS.IdName("_commonCallback"))),
                                    CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("indexVar"), "Set"), CS.IdName("context"), CS.AddExpr(CS.IdName("index"), CS.Literal(1))))
                                )
                            )},
                            new StatementSyntax[] {
                                CS.ExprStm(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("context"), "SetAllowChainedEnvironmentAccess"), CS.FalseLiteral)),
                            }
                        )
                    ),
                }
            ));


        }
    }
}
