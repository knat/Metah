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
    internal static class CSEX {
        internal const string MXNameString = "Metah.X";
        internal static QualifiedNameSyntax MXName { get { return CS.QualifiedName(CS.GlobalMetahName, "X"); } }
        internal static MemberAccessExpressionSyntax MXExp { get { return CS.MemberAccessExpr(CS.GlobalMetahName, "X"); } }
        internal static MemberAccessExpressionSyntax ExtensionMethodsExp { get { return CS.MemberAccessExpr(CS.MemberAccessExpr(MXExp, "Extensions"), "ExtensionMethods"); } }
        internal static MemberAccessExpressionSyntax CreateInstanceExp { get { return CS.MemberAccessExpr(ExtensionMethodsExp, "CreateInstance"); } }
        internal static MemberAccessExpressionSyntax CopyToExp { get { return CS.MemberAccessExpr(ExtensionMethodsExp, "CopyTo"); } }
        internal static AliasQualifiedNameSyntax XProgramInfoName { get { return CS.GlobalAliasQualifiedName("MetahXProgramInfo"); } }
        internal static MemberAccessExpressionSyntax XProgramInfoInstanceExp { get { return CS.MemberAccessExpr(XProgramInfoName, "Instance"); } }
        internal static QualifiedNameSyntax ProgramInfoName { get { return CS.QualifiedName(MXName, "ProgramInfo"); } }
        internal static QualifiedNameSyntax NamespaceInfoName { get { return CS.QualifiedName(MXName, "NamespaceInfo"); } }
        internal static ArrayTypeSyntax NamespaceInfoArrayType { get { return CS.OneDimArrayType(NamespaceInfoName); } }
        internal static QualifiedNameSyntax ListedSimpleTypeValueName { get { return CS.QualifiedName(MXName, "ListedSimpleTypeValue"); } }
        internal static ExpressionSyntax Literal(ListedSimpleTypeValue value) {
            return CS.NewObjExpr(ListedSimpleTypeValueName, CS.NewArrOrNullExpr(CS.ObjectArrayType, value.Select(i => SimpleValueLiteral(i))), CS.TrueLiteral);
        }
        internal static QualifiedNameSyntax UnitedSimpleTypeValueName { get { return CS.QualifiedName(MXName, "UnitedSimpleTypeValue"); } }
        internal static ExpressionSyntax Literal(UnitedSimpleTypeValue value) {
            return CS.NewObjExpr(UnitedSimpleTypeValueName, SimpleValueLiteral(value.Value), CS.TrueLiteral, CS.Literal(value.Literal), CS.Literal(value.ResovledIndex));
        }
        internal static QualifiedNameSyntax FullNameValueName { get { return CS.QualifiedName(MXName, "FullNameValue"); } }
        internal static ExpressionSyntax Literal(FullNameValue value) {
            return CS.NewObjExpr(FullNameValueName, CS.Literal(value.Uri), CS.Literal(value.LocalName), CS.Literal(value.Prefix));
        }
        internal static ExpressionSyntax SimpleValueLiteral(object value) {
            var exp = CS.TryToLiteral(value);
            if (exp != null) return exp;
            var listValue = value as ListedSimpleTypeValue;
            if (listValue != null) return Literal(listValue);
            var unionValue = value as UnitedSimpleTypeValue;
            if (unionValue != null) return Literal(unionValue);
            var fnValue = value as FullNameValue;
            if (fnValue != null) return Literal(fnValue);
            throw new InvalidOperationException();
        }
        internal static QualifiedNameSyntax EnumerationItemInfoName { get { return CS.QualifiedName(MXName, "EnumerationItemInfo"); } }
        internal static ArrayTypeSyntax EnumerationItemInfoArrayType { get { return CS.OneDimArrayType(EnumerationItemInfoName); } }
        internal static ExpressionSyntax Literal(EnumerationItemInfo value) { return CS.NewObjExpr(EnumerationItemInfoName, CS.Literal(value.Name), SimpleValueLiteral(value.Value)); }
        internal static QualifiedNameSyntax PatternItemInfoName { get { return CS.QualifiedName(MXName, "PatternItemInfo"); } }
        internal static ArrayTypeSyntax PatternItemInfoArrayType { get { return CS.OneDimArrayType(PatternItemInfoName); } }
        internal static ExpressionSyntax Literal(PatternItemInfo value) { return CS.NewObjExpr(PatternItemInfoName, CS.Literal(value.Pattern)); }
        internal static QualifiedNameSyntax WhitespaceNormalizationName { get { return CS.QualifiedName(MXName, "WhitespaceNormalization"); } }
        internal static NullableTypeSyntax WhitespaceNormalizationNullableType { get { return SyntaxFactory.NullableType(WhitespaceNormalizationName); } }
        internal static ExpressionSyntax Literal(WhitespaceNormalization value) {
            return SyntaxFactory.CastExpression(WhitespaceNormalizationName, CS.Literal((int)value));
        }
        internal static ExpressionSyntax Literal(WhitespaceNormalization? value) {
            return SyntaxFactory.CastExpression(WhitespaceNormalizationNullableType, value == null ? CS.NullLiteral : Literal(value.Value));
        }
        internal static QualifiedNameSyntax FacetSetInfoName { get { return CS.QualifiedName(MXName, "FacetSetInfo"); } }
        internal static ExpressionSyntax Literal(MX.FacetSetInfo value) {
            if (value == null) return CS.NullLiteral;
            return CS.NewObjExpr(FacetSetInfoName,
                CS.Literal(value.MinLength),
                CS.Literal(value.MinLengthFixed),
                CS.Literal(value.MaxLength),
                CS.Literal(value.MaxLengthFixed),
                CS.Literal(value.TotalDigits),
                CS.Literal(value.TotalDigitsFixed),
                CS.Literal(value.FractionDigits),
                CS.Literal(value.FractionDigitsFixed),
                SimpleValueLiteral(value.LowerValue),
                CS.Literal(value.LowerValueInclusive),
                CS.Literal(value.LowerValueFixed),
                CS.Literal(value.LowerValueText),
                SimpleValueLiteral(value.UpperValue),
                CS.Literal(value.UpperValueInclusive),
                CS.Literal(value.UpperValueFixed),
                CS.Literal(value.UpperValueText),
                CS.NewArrOrNullExpr(EnumerationItemInfoArrayType, value.Enumerations == null ? null : value.Enumerations.Select(i => Literal(i))),
                CS.Literal(value.EnumerationsText),
                CS.NewArrOrNullExpr(PatternItemInfoArrayType, value.Patterns == null ? null : value.Patterns.Select(i => Literal(i))),
                Literal(value.WhitespaceNormalization),
                CS.Literal(value.WhitespaceNormalizationFixed)
                );
        }
        internal static QualifiedNameSyntax ObjectInfoName { get { return CS.QualifiedName(MXName, "ObjectInfo"); } }
        internal static QualifiedNameSyntax ContextName { get { return CS.QualifiedName(MXName, "Context"); } }
        internal static QualifiedNameSyntax TypeName { get { return CS.QualifiedName(MXName, "Type"); } }
        internal static MemberAccessExpressionSyntax TypeExp { get { return CS.MemberAccessExpr(MXExp, "Type"); } }
        internal static QualifiedNameSyntax SimpleTypeName { get { return CS.QualifiedName(MXName, "SimpleType"); } }
        internal static MemberAccessExpressionSyntax SimpleTypeExp { get { return CS.MemberAccessExpr(MXExp, "SimpleType"); } }
        internal static MemberAccessExpressionSyntax TryGetTypedValueExp { get { return CS.MemberAccessExpr(SimpleTypeExp, "TryGetTypedValue"); } }

        internal static QualifiedNameSyntax TypeInfoName { get { return CS.QualifiedName(MXName, "TypeInfo"); } }
        internal static ArrayTypeSyntax TypeInfoArrayType { get { return CS.OneDimArrayType(TypeInfoName); } }
        //internal static QualifiedNameSyntax SimpleTypeInfoName { get { return CS.QualifiedName(MXName, "SimpleTypeInfo"); } }
        //internal static ArrayTypeSyntax SimpleTypeInfoArrayType { get { return CS.OneDimArrayType(SimpleTypeInfoName); } }
        internal static QualifiedNameSyntax ListedSimpleTypeOf(TypeSyntax elementType) {
            return SyntaxFactory.QualifiedName(MXName, CS.GenericName("ListedSimpleType", elementType));
        }
        internal static QualifiedNameSyntax ListedSimpleTypeInfoName { get { return CS.QualifiedName(MXName, "ListedSimpleTypeInfo"); } }
        internal static QualifiedNameSyntax UnitedSimpleTypeName { get { return CS.QualifiedName(MXName, "UnitedSimpleType"); } }
        internal static QualifiedNameSyntax UnitedSimpleTypeInfoName { get { return CS.QualifiedName(MXName, "UnitedSimpleTypeInfo"); } }
        internal static QualifiedNameSyntax UnitedSimpleTypeMemberInfoName { get { return CS.QualifiedName(MXName, "UnitedSimpleTypeMemberInfo"); } }
        internal static ArrayTypeSyntax UnitedSimpleTypeMemberInfoArrayType { get { return CS.OneDimArrayType(UnitedSimpleTypeMemberInfoName); } }
        internal static QualifiedNameSyntax AtomicSimpleTypeInfoName { get { return CS.QualifiedName(MXName, "AtomicSimpleTypeInfo"); } }
        internal static QualifiedNameSyntax ComplexTypeName { get { return CS.QualifiedName(MXName, "ComplexType"); } }
        internal static QualifiedNameSyntax ComplexTypeInfoName { get { return CS.QualifiedName(MXName, "ComplexTypeInfo"); } }
        internal static QualifiedNameSyntax TypeKindName { get { return CS.QualifiedName(MXName, "TypeKind"); } }
        internal static ExpressionSyntax Literal(MX.TypeKind value) { return SyntaxFactory.CastExpression(TypeKindName, CS.Literal((int)value)); }
        internal static QualifiedNameSyntax InstanceProhibitionName { get { return CS.QualifiedName(MXName, "InstanceProhibition"); } }
        internal static ExpressionSyntax Literal(InstanceProhibition value) { return SyntaxFactory.CastExpression(InstanceProhibitionName, CS.Literal((int)value)); }
        internal static QualifiedNameSyntax AttributeName { get { return CS.QualifiedName(MXName, "Attribute"); } }
        internal static QualifiedNameSyntax AttributeSetName { get { return CS.QualifiedName(MXName, "AttributeSet"); } }
        internal static QualifiedNameSyntax EntityDeclarationKindName { get { return CS.QualifiedName(MXName, "EntityDeclarationKind"); } }
        internal static ExpressionSyntax Literal(EntityDeclarationKind value) { return SyntaxFactory.CastExpression(EntityDeclarationKindName, CS.Literal((int)value)); }
        internal static QualifiedNameSyntax GlobalObjectKindName { get { return CS.QualifiedName(MXName, "GlobalObjectKind"); } }
        internal static ExpressionSyntax Literal(GlobalObjectKind value) { return SyntaxFactory.CastExpression(GlobalObjectKindName, CS.Literal((int)value)); }
        internal static QualifiedNameSyntax GlobalObjectRefInfoName { get { return CS.QualifiedName(MXName, "GlobalObjectRefInfo"); } }
        internal static ArrayTypeSyntax GlobalObjectRefInfoArrayType { get { return CS.OneDimArrayType(GlobalObjectRefInfoName); } }
        internal static QualifiedNameSyntax AttributeInfoName { get { return CS.QualifiedName(MXName, "AttributeInfo"); } }
        internal static ArrayTypeSyntax AttributeInfoArrayType { get { return CS.OneDimArrayType(AttributeInfoName); } }
        internal static QualifiedNameSyntax AttributeSetInfoName { get { return CS.QualifiedName(MXName, "AttributeSetInfo"); } }
        internal static QualifiedNameSyntax ElementName { get { return CS.QualifiedName(MXName, "Element"); } }
        internal static QualifiedNameSyntax ElementInfoName { get { return CS.QualifiedName(MXName, "ElementInfo"); } }
        internal static ArrayTypeSyntax ElementInfoArrayType { get { return CS.OneDimArrayType(ElementInfoName); } }
        internal static QualifiedNameSyntax ElementWildcardInfoName { get { return CS.QualifiedName(MXName, "ElementWildcardInfo"); } }
        internal static QualifiedNameSyntax ChildListInfoName { get { return CS.QualifiedName(MXName, "ChildListInfo"); } }
        internal static QualifiedNameSyntax ChildStructInfoName { get { return CS.QualifiedName(MXName, "ChildStructInfo"); } }
        internal static QualifiedNameSyntax ChildContainerName { get { return CS.QualifiedName(MXName, "ChildContainer"); } }
        internal static QualifiedNameSyntax ChildContainerKindName { get { return CS.QualifiedName(MXName, "ChildContainerKind"); } }
        internal static ExpressionSyntax Literal(ChildContainerKind value) { return SyntaxFactory.CastExpression(ChildContainerKindName, CS.Literal((int)value)); }
        internal static QualifiedNameSyntax ChildInfoName { get { return CS.QualifiedName(MXName, "ChildInfo"); } }
        internal static ArrayTypeSyntax ChildInfoArrayType { get { return CS.OneDimArrayType(ChildInfoName); } }
        internal static QualifiedNameSyntax ChildListOf(TypeSyntax elementType) {
            return SyntaxFactory.QualifiedName(MXName, CS.GenericName("ChildList", elementType));
        }
        internal static QualifiedNameSyntax DefaultOrFixedValueInfoName { get { return CS.QualifiedName(MXName, "DefaultOrFixedValueInfo"); } }
        internal static ExpressionSyntax Literal(DefaultOrFixedValueInfo value) {
            if (value == null) return CS.NullLiteral;
            return CS.NewObjExpr(DefaultOrFixedValueInfoName, CS.Literal(value.IsDefault), SimpleValueLiteral(value.Value), CS.Literal(value.ValueText));
        }
        internal static QualifiedNameSyntax WildcardUriKindName { get { return CS.QualifiedName(MXName, "WildcardUriKind"); } }
        internal static ExpressionSyntax Literal(WildcardUriKind value) { return SyntaxFactory.CastExpression(WildcardUriKindName, CS.Literal((int)value)); }
        internal static QualifiedNameSyntax WildcardUriInfoName { get { return CS.QualifiedName(MXName, "WildcardUriInfo"); } }
        internal static ArrayTypeSyntax WildcardUriInfoArrayType { get { return CS.OneDimArrayType(WildcardUriInfoName); } }
        internal static ExpressionSyntax Literal(WildcardUriInfo value) {
            if (value == null) return CS.NullLiteral;
            return CS.NewObjExpr(WildcardUriInfoName, Literal(value.Kind), CS.Literal(value.Value));
        }
        internal static QualifiedNameSyntax WildcardInfoName { get { return CS.QualifiedName(MXName, "WildcardInfo"); } }
        internal static QualifiedNameSyntax WildcardValidationName { get { return CS.QualifiedName(MXName, "WildcardValidation"); } }
        internal static ExpressionSyntax Literal(WildcardValidation value) { return SyntaxFactory.CastExpression(WildcardValidationName, CS.Literal((int)value)); }
        internal static QualifiedNameSyntax IdentityConstraintName { get { return CS.QualifiedName(MXName, "IdentityConstraint"); } }
        internal static QualifiedNameSyntax IdentityConstraintKindName { get { return CS.QualifiedName(MXName, "IdentityConstraintKind"); } }
        internal static ExpressionSyntax Literal(IdentityConstraintKind value) { return SyntaxFactory.CastExpression(IdentityConstraintKindName, CS.Literal((int)value)); }
        internal static QualifiedNameSyntax IdentityConstraintInfoName { get { return CS.QualifiedName(MXName, "IdentityConstraintInfo"); } }
        internal static ArrayTypeSyntax IdentityConstraintInfoArrayType { get { return CS.OneDimArrayType(IdentityConstraintInfoName); } }
        internal static ExpressionSyntax Literal(IdentityConstraintInfo value) {
            if (value == null) return CS.NullLiteral;
            //public IdentityConstraintInfo(IdentityConstraintKind kind, XName name, XName referentialName, bool isSplitListValue, PathExpressionInfo identity, IReadOnlyList<PathExpressionInfo> values)
            return CS.NewObjExpr(IdentityConstraintInfoName, Literal(value.Kind), CS.Literal(value.Name), CS.Literal(value.ReferentialName), CS.Literal(value.IsSplitListValue),
                Literal(value.IdentityPathExpression), CS.NewArrExpr(PathExpressionInfoArrayType, value.ValuePathExpressions.Select(Literal)));
        }
        internal static QualifiedNameSyntax PathExpressionInfoName { get { return CS.QualifiedName(MXName, "PathExpressionInfo"); } }
        internal static ArrayTypeSyntax PathExpressionInfoArrayType { get { return CS.OneDimArrayType(PathExpressionInfoName); } }
        internal static ExpressionSyntax Literal(PathExpressionInfo value) {
            if (value == null) return CS.NullLiteral;
            return CS.NewObjExpr(PathExpressionInfoName, CS.NewArrExpr(PathInfoArrayType, value.Paths.Select(Literal)));
        }
        internal static QualifiedNameSyntax PathInfoName { get { return CS.QualifiedName(MXName, "PathInfo"); } }
        internal static ArrayTypeSyntax PathInfoArrayType { get { return CS.OneDimArrayType(PathInfoName); } }
        internal static ExpressionSyntax Literal(PathInfo value) {
            if (value == null) return CS.NullLiteral;
            return CS.NewObjExpr(PathInfoName, CS.NewArrExpr(StepInfoArrayType, value.Steps.Select(Literal)));
        }
        internal static QualifiedNameSyntax StepKindName { get { return CS.QualifiedName(MXName, "StepKind"); } }
        internal static ExpressionSyntax Literal(StepKind value) { return SyntaxFactory.CastExpression(StepKindName, CS.Literal((int)value)); }
        internal static QualifiedNameSyntax StepInfoName { get { return CS.QualifiedName(MXName, "StepInfo"); } }
        internal static ArrayTypeSyntax StepInfoArrayType { get { return CS.OneDimArrayType(StepInfoName); } }
        internal static ExpressionSyntax Literal(StepInfo value) {
            if (value == null) return CS.NullLiteral;
            //StepInfo(kind, isAttribute, uri, name)
            return CS.NewObjExpr(StepInfoName, Literal(value.Kind), CS.Literal(value.IsAttribute), CS.Literal(value.Uri), CS.Literal(value.Name));
        }

        //
        //>public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        internal static PropertyDeclarationSyntax ObjectInfoProperty() {
            return CS.Property(CS.PublicOverrideTokenList, ObjectInfoName, "ObjectInfo", true,
                default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.IdName("ThisInfo")) });
        }
        //>VALUETYPE r; SimpleType.TryGetTypedValue(VALUEEXP, out r); return r;
        internal static StatementSyntax[] TryGetTypedValueStatements(TypeSyntax valueType, ExpressionSyntax valueExp) {
            return new StatementSyntax[] {
                CS.LocalDeclStm(valueType, "r"),
                SyntaxFactory.ExpressionStatement(CS.InvoExpr(TryGetTypedValueExp, SyntaxFactory.Argument(valueExp), CS.OutArgument("r"))),
                SyntaxFactory.ReturnStatement(CS.IdName("r")) };
        }
        //>var obj = OBJECT; if(obj == null) return null; return obj.MEMBER;
        internal static StatementSyntax[] ObjectMemberOrNullStatements(ExpressionSyntax objExp, string memberName) {
            return new StatementSyntax[] { 
                CS.LocalDeclStm(CS.VarIdName, "obj", objExp),
                SyntaxFactory.IfStatement(CS.EqualsExpr(CS.IdName("obj"), CS.NullLiteral), SyntaxFactory.ReturnStatement(CS.NullLiteral)),
                SyntaxFactory.ReturnStatement(CS.MemberAccessExpr(CS.IdName("obj"), memberName)) 
            };
        }
        //>MODIFIERS IEnumerator<T> GetEnumerator() {
        //>    using (var enumerator = SOURCE.GetEnumerator())
        //>        while (enumerator.MoveNext())
        //>            yield return enumerator.Current as ITEMTYPE;
        //>}
        internal static MethodDeclarationSyntax GetEnumeratorMethod(SyntaxTokenList modifiers, TypeSyntax itemType, ExpressionSyntax source) {
            return CS.Method(modifiers, CS.IEnumeratorOf(itemType), "GetEnumerator", null,
                SyntaxFactory.UsingStatement(CS.VarDecl(CS.VarIdName, "enumerator", CS.InvoExpr(CS.MemberAccessExpr(source, "GetEnumerator"))), null,
                    SyntaxFactory.WhileStatement(CS.InvoExpr(CS.MemberAccessExpr(CS.IdName("enumerator"), "MoveNext")),
                        SyntaxFactory.YieldStatement(SyntaxKind.YieldReturnStatement, CS.AsExpr(CS.MemberAccessExpr(CS.IdName("enumerator"), "Current"), itemType)))));
        }
        ////System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }
        //internal static MethodDeclarationSyntax NonGenericGetEnumeratorMethod() {
        //    return CS.Method(default(SyntaxTokenList), CS.IEnumeratorName, "GetEnumerator", null,
        //              SyntaxFactory.ReturnStatement(CS.Invocation(CS.IdName("GetEnumerator")))).
        //              WithExplicitInterfaceSpecifier(SyntaxFactory.ExplicitInterfaceSpecifier(CS.IEnumerableName));
        //}
        internal static IEnumerable<MemberDeclarationSyntax> IListOverrideMembers(TypeSyntax itemType) {
            //>public bool Contains(TYPE item) { return base.Contains(item); }
            yield return CS.Method(CS.PublicTokenList, CS.BoolType, "Contains",
                new[] { CS.Parameter(itemType, "item") },
                SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.BaseMemberAccessExpr("Contains"), CS.IdName("item"))));
            //>public int IndexOf(TYPE item) { return base.IndexOf(item); }
            yield return CS.Method(CS.PublicTokenList, CS.IntType, "IndexOf",
                new[] { CS.Parameter(itemType, "item") },
                SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.BaseMemberAccessExpr("IndexOf"), CS.IdName("item"))));
            //>public void Add(TYPE item) { base.Add(item); }
            yield return CS.Method(CS.PublicTokenList, CS.VoidType, "Add",
                new[] { CS.Parameter(itemType, "item") },
                SyntaxFactory.ExpressionStatement(CS.InvoExpr(CS.BaseMemberAccessExpr("Add"), CS.IdName("item"))));
            //>public void Insert(int index, TYPE item) { base.Insert(index, item); }
            yield return CS.Method(CS.PublicTokenList, CS.VoidType, "Insert",
                new[] { CS.Parameter(CS.IntType, "index"), CS.Parameter(itemType, "item") },
                SyntaxFactory.ExpressionStatement(CS.InvoExpr(CS.BaseMemberAccessExpr("Insert"), CS.IdName("index"), CS.IdName("item"))));
            //>new public TYPE this[int index] {
            //>    get { return base[index] as TYPE; }
            //>    set { base[index] = value; }
            //>}
            yield return CS.Indexer(CS.NewPublicTokenList, itemType, new[] { CS.Parameter(CS.IntType, "index") }, false,
                default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.AsExpr(CS.BaseElementAccessExpr(CS.IdName("index")), itemType)) },
                default(SyntaxTokenList), new[] { SyntaxFactory.ExpressionStatement(CS.AssignExpr(CS.BaseElementAccessExpr(CS.IdName("index")), CS.IdName("value"))) });
            //>public bool Remove(TYPE item) { return base.Remove(item); }
            yield return CS.Method(CS.PublicTokenList, CS.BoolType, "Remove",
                new[] { CS.Parameter(itemType, "item") },
                SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.BaseMemberAccessExpr("Remove"), CS.IdName("item"))));
            //>public void CopyTo(TYPE[] array, int arrayIndex) { Extensions.CopyTo(this, array, arrayIndex); }
            yield return CS.Method(CS.PublicTokenList, CS.VoidType, "CopyTo",
                new[] { CS.Parameter(CS.OneDimArrayType(itemType), "array"), CS.Parameter(CS.IntType, "arrayIndex") },
                SyntaxFactory.ExpressionStatement(CS.InvoExpr(CopyToExp, SyntaxFactory.ThisExpression(), CS.IdName("array"), CS.IdName("arrayIndex"))));
            //>new public IEnumerator<TYPE> GetEnumerator(){...}
            yield return GetEnumeratorMethod(CS.NewPublicTokenList, itemType, SyntaxFactory.BaseExpression());
            //>System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }
            yield return CS.Method(default(SyntaxTokenList), CS.IEnumeratorName, "GetEnumerator", null,
                SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.IdName("GetEnumerator")))).
                WithExplicitInterfaceSpecifier(SyntaxFactory.ExplicitInterfaceSpecifier(CS.IEnumerableName));
            //>bool System.Collections.Generic.ICollection<T>.IsReadOnly { get { return false; } }
            yield return CS.Property(default(SyntaxTokenList), CS.BoolType, "IsReadOnly", true,
                default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.FalseLiteral) }).
                    WithExplicitInterfaceSpecifier(SyntaxFactory.ExplicitInterfaceSpecifier(CS.ICollectionOf(itemType)));
        }
    }
    //
    //
    public abstract class Info {
        protected Info(SimpleToken keywordToken) {
            KeywordToken = keywordToken;
        }
        internal readonly SimpleToken KeywordToken;//opt
    }
    public sealed class ProgramInfo : Info {
        internal ProgramInfo()
            : base(null) {
            AddNamespace(NamespaceInfo.System);
        }
        internal readonly Dictionary<XNamespace, NamespaceInfo> NamespaceMap = new Dictionary<XNamespace, NamespaceInfo>();
        internal void AddNamespace(NamespaceInfo ns) { NamespaceMap.Add(ns.Uri, ns); }
        internal T GetObjectInfo<T>(Object obj, NameResolutionKind kind) where T : ObjectInfo {
            return (T)NamespaceMap[obj.NamespaceAncestor.Uri].GetObjectInfo(obj, kind);
        }
    }
    public abstract class ObjectInfo : Info {
        protected ObjectInfo(SimpleToken keywordToken, Object obj, string csName, CSPart csPart)
            : base(keywordToken) {
            //if (csName == null) throw new ArgumentNullException("csName");
            //if (csPart == null) throw new ArgumentNullException("csPart");
            Object = obj;
            CSName = csName;
            CSPart = csPart;
        }
        internal readonly Object Object;
        internal readonly string CSName;//C# class name
        internal NameSyntax CSFullName;//C# qualified name
        internal ExpressionSyntax CSFullExp;//C# member access expr
        internal NamespaceInfo NamespaceParent;//for global type, attribute, element
        internal bool IsGlobal { get { return NamespaceParent != null; } }
        internal bool IsCSClassOverride;//same name inner class? (need 'new' modifier)
        internal virtual bool IsCSAbstract { get { return false; } }
        internal readonly CSPart CSPart;
        private ExpressionSyntax _thisInfoExp;
        internal ExpressionSyntax ThisInfoExp { get { return _thisInfoExp ?? (_thisInfoExp = CS.MemberAccessExpr(CSFullExp, "ThisInfo")); } }
        internal virtual ObjectInfo GenerateCS(ObjectInfo parent) {
            if (CSFullName != null) return null;
            if (IsGlobal) parent = NamespaceParent;
            CSFullName = CS.QualifiedName(parent.CSFullName, CSName);
            CSFullExp = CS.MemberAccessExpr(parent.CSFullExp, CSName);
            Object.AnalyzerAncestor.AddClassAlias((CSClass)CSPart, CSFullName);
            //private static CSFullName AsThis(object o){return o as CSFullName;}
            AddCSMember(CS.Method(CS.PrivateStaticTokenList, CSFullName, "AsThis", new[] { CS.Parameter(CS.ObjectType, "o") },
                SyntaxFactory.ReturnStatement(CS.AsExpr(CS.IdName("o"), CSFullName))));
            return parent;
        }
        internal void AddCSMember(MemberDeclarationSyntax member) { CSPart.MemberList.Add(member); }
        internal void AddCSMembers(IEnumerable<MemberDeclarationSyntax> members) { CSPart.MemberList.AddRange(members); }
        protected void CreateAndAddCSClass(NameSyntax baseName, ObjectInfo parent) { CreateAndAddCSClass(new[] { baseName }, parent); }
        private IEnumerable<SyntaxToken> GetModifiers() {
            if (IsCSClassOverride) yield return CS.NewToken;
            yield return CS.PublicToken;
            if (IsCSAbstract) yield return CS.AbstractToken;
            yield return CS.PartialToken;
        }
        protected void CreateAndAddCSClass(NameSyntax[] baseNames, ObjectInfo parent) {
            if (parent.CSName != null && Identifier.ValueEquals(parent.CSName, CSName))
                CompilationContext.Throw(KeywordToken, ErrorKind.NestedClassNameEqualToParent, CSName);
            var isFirst = true;
            foreach (var csClass in CSPart.Parts<CSClass>()) {
                var isGenerated = csClass.IsGenerated;
                ClassDeclarationSyntax clsSyntax;
                if (isFirst) {
                    SyntaxAnnotation ann;
                    clsSyntax = CS.Class(isGenerated ? new[] { CS.SerializableAttributeList } : csClass.AttributeListList.Append(CS.SerializableAttributeList),
                        SyntaxFactory.TokenList(GetModifiers()), CSName, isGenerated ? baseNames : baseNames.Concat(csClass.BaseNameList), csClass.MemberList)
                        .SetAnn(out ann);
                    csClass.SetCSSyntaxAnnotation(ann);
                    isFirst = false;
                }
                else {
                    clsSyntax = CS.Class(isGenerated ? null : csClass.AttributeListList, CS.PartialTokenList, CSName,
                        isGenerated ? null : csClass.BaseNameList, csClass.MemberList);
                }
                //clsSyntax = clsSyntax.WithAdditionalAnnotations(new CSClassAnnotation(csClass), csClass.Keyword.SourceSpan);
                if (!parent.TryAddCSClass(csClass.CompilationUnitIndex, csClass.NamespaceIndex, clsSyntax)) {
                    if (isGenerated) {
                        parent.CSPart.MemberList.Add(clsSyntax);
                    }
                    else {
                        CompilationContext.Throw(csClass.Keyword, ErrorKind.CannotAddClassToParent);
                    }
                }
            }
        }
        private bool TryAddCSClass(int compilationUnitIndex, int namespaceIndex, ClassDeclarationSyntax clsSyntax) {
            foreach (var csPart in CSPart.Parts<CSPart>()) {
                if (csPart.CompilationUnitIndex == compilationUnitIndex && csPart.NamespaceIndex == namespaceIndex) {
                    csPart.MemberList.Add(clsSyntax);
                    return true;
                }
            }
            return false;
        }
    }
    public sealed class NamespaceInfo : ObjectInfo {
        internal NamespaceInfo(Namespace nsObj)
            : base(nsObj.Keyword, nsObj, null, nsObj.CSNamespace) {
            CSFullName = nsObj.CSFullName;
            CSFullExp = nsObj.CSFullExp;
            Uri = nsObj.Uri;
            IsSystem = nsObj.IsSystem;
            CSNonGlobalFullName = nsObj.CSNonGlobalFullName;
        }
        internal static readonly NamespaceInfo System = new NamespaceInfo(Namespace.System).AddSystemTypes();
        //
        internal readonly XNamespace Uri;
        internal readonly bool IsSystem;
        internal readonly NameSyntax CSNonGlobalFullName;
        internal readonly Dictionary<string, TypeInfo> TypeMap = new Dictionary<string, TypeInfo>();
        private void AddType(TypeInfo info) { TypeMap.Add(info.LocalName, info); }
        internal readonly Dictionary<string, AttributeInfo> AttributeMap = new Dictionary<string, AttributeInfo>();
        private void AddAttribute(AttributeInfo info) { AttributeMap.Add(info.LocalName, info); }
        internal readonly Dictionary<string, ElementInfo> ElementMap = new Dictionary<string, ElementInfo>();
        private void AddElement(ElementInfo info) { ElementMap.Add(info.LocalName, info); }
        //
        internal ObjectInfo GetObjectInfo(Object obj, NameResolutionKind kind) {
            switch (kind) {
                case NameResolutionKind.Type: {
                        var typeObj = (Type)obj;
                        var info = TypeMap.TryGetValue(typeObj.Name);
                        if (info == null) {
                            info = typeObj.CreateInfo(null);
                            info.NamespaceParent = this;
                            AddType(info);
                        }
                        return info;
                    }
                case NameResolutionKind.Attribute: {
                        var attributeObj = (GlobalAttribute)obj;
                        var info = AttributeMap.TryGetValue(attributeObj.Name);
                        if (info == null) {
                            info = attributeObj.CreateInfo();
                            info.NamespaceParent = this;
                            AddAttribute(info);
                        }
                        return info;
                    }
                case NameResolutionKind.Element: {
                        var elementObj = (GlobalElement)obj;
                        var info = ElementMap.TryGetValue(elementObj.Name);
                        if (info == null) {
                            info = elementObj.CreateInfo();
                            info.NamespaceParent = this;
                            AddElement(info);
                        }
                        return info;
                    }
                default: throw new InvalidOperationException();
            }
        }
        internal CSNamespace CSNamespace { get { return (CSNamespace)base.CSPart; } }
        internal void GenerateCS(IReadOnlyList<CompilationUnit> compilationUnitList) {
            if (IsSystem) return;
            foreach (var type in TypeMap.Values) type.GenerateCS(null);
            foreach (var attribute in AttributeMap.Values) attribute.GenerateCS(null);
            foreach (var element in ElementMap.Values) element.GenerateCS(null);
            foreach (var csNamespace in CSPart.Parts<CSNamespace>()) {
                var nsSyntax = SyntaxFactory.NamespaceDeclaration(CSNonGlobalFullName, SyntaxFactory.List<ExternAliasDirectiveSyntax>(csNamespace.ExternList),
                     SyntaxFactory.List<UsingDirectiveSyntax>(csNamespace.FinalUsingList), SyntaxFactory.List<MemberDeclarationSyntax>(csNamespace.MemberList));
                var added = false;
                foreach (var compilationUnit in compilationUnitList) {
                    if (compilationUnit.Index == csNamespace.CompilationUnitIndex) {
                        compilationUnit.CSMemberList.Add(nsSyntax);
                        added = true;
                        break;
                    }
                }
                if (!added) throw new InvalidOperationException();
            }
        }
        internal ExpressionSyntax InfoLiteral {
            get {
                //new NamespaceInfo(XNamespace.Get("..."), new TypeInfo[] { }, new AttributeInfo[] { }, new ElementInfo[] { })
                return CS.NewObjExpr(CSEX.NamespaceInfoName, CS.Literal(Uri),
                    CS.NewArrOrNullExpr(CSEX.TypeInfoArrayType, TypeMap.Values.Select(i => i.ThisInfoExp)),
                    CS.NewArrOrNullExpr(CSEX.AttributeInfoArrayType, AttributeMap.Values.Select(i => i.ThisInfoExp)),
                    CS.NewArrOrNullExpr(CSEX.ElementInfoArrayType, ElementMap.Values.Select(i => i.ThisInfoExp))
                    );
            }
        }
        //
        private NamespaceInfo AddSystemTypes() {
            AddType(TypeInfo.Instance);
            AddType(SimpleTypeInfo.Instance);
            var StringInfo = CreateAndAdd(MX.String.ThisInfo, CS.StringType, null, true, SimpleTypeInfo.Instance);
            var NormalizedStringInfo = CreateAndAdd(MX.NormalizedString.ThisInfo, CS.StringType, null, true, StringInfo);
            var TokenInfo = CreateAndAdd(MX.Token.ThisInfo, CS.StringType, null, true, NormalizedStringInfo);
            CreateAndAdd(MX.Language.ThisInfo, CS.StringType, null, true, TokenInfo);
            var NameInfo = CreateAndAdd(MX.Name.ThisInfo, CS.StringType, null, true, TokenInfo);
            var NonColonizedNameInfo = CreateAndAdd(MX.NonColonizedName.ThisInfo, CS.StringType, null, true, NameInfo);
            CreateAndAdd(MX.Id.ThisInfo, CS.StringType, null, true, NonColonizedNameInfo);
            var IdRefInfo = CreateAndAdd(MX.IdRef.ThisInfo, CS.StringType, null, true, NonColonizedNameInfo);
            CreateListAndAdd(MX.IdRefs.ThisInfo, IdRefInfo);
            var EntityInfo = CreateAndAdd(MX.Entity.ThisInfo, CS.StringType, null, true, NonColonizedNameInfo);
            CreateListAndAdd(MX.Entities.ThisInfo, EntityInfo);
            var NameTokenInfo = CreateAndAdd(MX.NameToken.ThisInfo, CS.StringType, null, true, TokenInfo);
            CreateListAndAdd(MX.NameTokens.ThisInfo, NameTokenInfo);
            CreateAndAdd(MX.Uri.ThisInfo, CS.XNamespaceName, null, true, SimpleTypeInfo.Instance);
            CreateAndAdd(MX.FullName.ThisInfo, CSEX.FullNameValueName, null, true, SimpleTypeInfo.Instance);
            //CreateAndAdd(MX.Notation.ThisInfo, CSEX.FullNameValueName, null, true, SimpleTypeInfo.Instance);
            var DecimalInfo = CreateAndAdd(MX.Decimal.ThisInfo, CS.DecimalType, CS.DecimalNullableType, false, SimpleTypeInfo.Instance);
            var IntegerInfo = CreateAndAdd(MX.Integer.ThisInfo, CS.DecimalType, CS.DecimalNullableType, false, DecimalInfo);
            var NonPositiveIntegerInfo = CreateAndAdd(MX.NonPositiveInteger.ThisInfo, CS.DecimalType, CS.DecimalNullableType, false, IntegerInfo);
            CreateAndAdd(MX.NegativeInteger.ThisInfo, CS.DecimalType, CS.DecimalNullableType, false, NonPositiveIntegerInfo);
            var NonNegativeIntegerInfo = CreateAndAdd(MX.NonNegativeInteger.ThisInfo, CS.DecimalType, CS.DecimalNullableType, false, IntegerInfo);
            CreateAndAdd(MX.PositiveInteger.ThisInfo, CS.DecimalType, CS.DecimalNullableType, false, NonNegativeIntegerInfo);
            var Int64Info = CreateAndAdd(MX.Int64.ThisInfo, CS.LongType, CS.LongNullableType, false, IntegerInfo);
            var Int32Info = CreateAndAdd(MX.Int32.ThisInfo, CS.IntType, CS.IntNullableType, false, Int64Info);
            var Int16Info = CreateAndAdd(MX.Int16.ThisInfo, CS.ShortType, CS.ShortNullableType, false, Int32Info);
            CreateAndAdd(MX.SByte.ThisInfo, CS.SByteType, CS.SByteNullableType, false, Int16Info);
            var UInt64Info = CreateAndAdd(MX.UInt64.ThisInfo, CS.ULongType, CS.ULongNullableType, false, NonNegativeIntegerInfo);
            var UInt32Info = CreateAndAdd(MX.UInt32.ThisInfo, CS.UIntType, CS.UIntNullableType, false, UInt64Info);
            var UInt16Info = CreateAndAdd(MX.UInt16.ThisInfo, CS.UShortType, CS.UShortNullableType, false, UInt32Info);
            CreateAndAdd(MX.Byte.ThisInfo, CS.ByteType, CS.ByteNullableType, false, UInt16Info);
            CreateAndAdd(MX.Single.ThisInfo, CS.FloatType, CS.FloatNullableType, false, SimpleTypeInfo.Instance);
            CreateAndAdd(MX.Double.ThisInfo, CS.DoubleType, CS.DoubleNullableType, false, SimpleTypeInfo.Instance);
            CreateAndAdd(MX.Boolean.ThisInfo, CS.BoolType, CS.BoolNullableType, false, SimpleTypeInfo.Instance);
            CreateAndAdd(MX.Base64Binary.ThisInfo, CS.ByteArrayType, null, true, SimpleTypeInfo.Instance);
            CreateAndAdd(MX.HexBinary.ThisInfo, CS.ByteArrayType, null, true, SimpleTypeInfo.Instance);
            CreateAndAdd(MX.TimeSpan.ThisInfo, CS.TimeSpanName, CS.TimeSpanNullableType, false, SimpleTypeInfo.Instance);
            CreateAndAdd(MX.DateTime.ThisInfo, CS.DateTimeName, CS.DateTimeNullableType, false, SimpleTypeInfo.Instance);
            CreateAndAdd(MX.Date.ThisInfo, CS.DateTimeName, CS.DateTimeNullableType, false, SimpleTypeInfo.Instance);
            CreateAndAdd(MX.Time.ThisInfo, CS.DateTimeName, CS.DateTimeNullableType, false, SimpleTypeInfo.Instance);
            CreateAndAdd(MX.YearMonth.ThisInfo, CS.DateTimeName, CS.DateTimeNullableType, false, SimpleTypeInfo.Instance);
            CreateAndAdd(MX.Year.ThisInfo, CS.DateTimeName, CS.DateTimeNullableType, false, SimpleTypeInfo.Instance);
            CreateAndAdd(MX.MonthDay.ThisInfo, CS.DateTimeName, CS.DateTimeNullableType, false, SimpleTypeInfo.Instance);
            CreateAndAdd(MX.Month.ThisInfo, CS.DateTimeName, CS.DateTimeNullableType, false, SimpleTypeInfo.Instance);
            CreateAndAdd(MX.Day.ThisInfo, CS.DateTimeName, CS.DateTimeNullableType, false, SimpleTypeInfo.Instance);
            return this;
        }
        private AtomicSimpleTypeInfo CreateAndAdd(MX.AtomicSimpleTypeInfo plxInfo, TypeSyntax valueCSFullName, TypeSyntax nullableValueCSFullName, bool isValueClrTypeRef, SimpleTypeInfo baseType) {
            var csName = plxInfo.ClrType.Name;
            var info = new AtomicSimpleTypeInfo(null, null, csName, null, plxInfo.Kind, plxInfo.Name, baseType, DerivationProhibition.None, valueCSFullName, nullableValueCSFullName,
                plxInfo.ValueClrType, isValueClrTypeRef, plxInfo.FacetSet == null ? null : new FacetSetInfo(null, null, plxInfo.FacetSet)) {
                    CSFullName = CS.QualifiedName(CSEX.MXName, csName),
                    CSFullExp = CS.MemberAccessExpr(CSEX.MXExp, csName)
                };
            var f = info.FacetSetExp;//ensure it
            AddType(info);
            return info;
        }
        private ListedSimpleTypeInfo CreateListAndAdd(MX.ListedSimpleTypeInfo plxInfo, SimpleTypeInfo itemType) {
            var csName = plxInfo.ClrType.Name;
            var info = new ListedSimpleTypeInfo(null, null, csName, null, plxInfo.Name, DerivationProhibition.None, itemType) {
                CSFullName = CS.QualifiedName(CSEX.MXName, csName),
                CSFullExp = CS.MemberAccessExpr(CSEX.MXExp, csName)
            };
            //var f = info.FacetSetExp;
            AddType(info);
            return info;
        }
    }
    public class TypeInfo : ObjectInfo {
        protected TypeInfo(SimpleToken keywordToken, Type obj, string csName, CSClass csClass,
            MX.TypeKind kind, XName name, TypeInfo baseType, DerivationMethod derivationMethod, DerivationProhibition derivationProhibition)
            : base(keywordToken, obj, csName, csClass) {
            Kind = kind;
            Name = name;
            BaseType = baseType;
            DerivationMethod = derivationMethod;
            DerivationProhibition = derivationProhibition;
        }
        internal static readonly TypeInfo Instance = new TypeInfo(null, null, MX.Type.ThisInfo.LocalName, null, MX.TypeKind.Type, MX.Type.ThisInfo.Name, null,
            DerivationMethod.None, DerivationProhibition.None) { CSFullName = CSEX.TypeName, CSFullExp = CSEX.TypeExp };
        internal override bool IsCSAbstract { get { return true; } }
        internal readonly MX.TypeKind Kind;
        internal readonly XName Name;//opt
        internal string LocalName { get { return Name == null ? null : Name.LocalName; } }
        internal readonly TypeInfo BaseType;//opt
        internal readonly DerivationMethod DerivationMethod;
        internal readonly DerivationProhibition DerivationProhibition;
        internal bool IsEqualToOrDeriveFrom(TypeInfo other) {
            if (other == null) throw new ArgumentNullException("other");
            for (var info = this; info != null; info = info.BaseType)
                if (info == other) return true;
            return false;
        }
        internal bool IsEqualToOrRestrictedDeriveFrom(TypeInfo other) {
            if (other == null) throw new ArgumentNullException("other");
            for (var info = this; info != null; info = info.BaseType) {
                if (info == other) return true;
                if (info.DerivationMethod != X.DerivationMethod.Restriction) return false;
            }
            return false;
        }
    }
    public sealed class FacetSetInfo : Info {
        internal FacetSetInfo(SimpleToken keywordToken, FacetSetInfo baseFacetSet, MX.FacetSetInfo value)
            : base(keywordToken) {
            if (value == null) throw new ArgumentNullException("value");
            BaseFacetSet = baseFacetSet;
            Value = value;
            if (baseFacetSet != null) AddEnumerationsItemNames(baseFacetSet.Value);
            AddEnumerationsItemNames(value);
        }
        internal readonly FacetSetInfo BaseFacetSet;//opt
        internal readonly MX.FacetSetInfo Value;
        internal readonly HashSet<string> AllEnumerationsItemNameSet = new HashSet<string>();
        private void AddEnumerationsItemNames(MX.FacetSetInfo facetSet) {
            if (facetSet.Enumerations != null)
                foreach (var i in facetSet.Enumerations)
                    if (i.Name != null)
                        AllEnumerationsItemNameSet.Add(i.Name);
        }
        private ExpressionSyntax _exp;
        internal ExpressionSyntax GetExp(SimpleTypeInfo parent) {
            if (_exp != null) return _exp;
            _exp = CS.MemberAccessExpr(parent.ThisInfoExp, "FacetSet");
            return CSEX.Literal(Value);
        }
    }
    public class SimpleTypeInfo : TypeInfo, ISimpleTypeInfo {
        protected SimpleTypeInfo(SimpleToken keywordToken, Type obj, string csName, CSClass csClass,
            MX.TypeKind kind, XName name, TypeInfo baseType, DerivationProhibition derivationProhibition,
            TypeSyntax valueCSFullName, TypeSyntax nullableValueCSFullName, SType valueClrType, bool isValueClrTypeRef, FacetSetInfo facetSet)
            : base(keywordToken, obj, csName, csClass, kind, name, baseType, DerivationMethod.Restriction, derivationProhibition) {
            ValueCSFullName = valueCSFullName;
            NullableValueCSFullName = nullableValueCSFullName ?? valueCSFullName;
            ValueClrType = valueClrType;
            IsValueClrTypeRef = isValueClrTypeRef;
            FacetSet = facetSet;
        }
        new internal static readonly SimpleTypeInfo Instance = new SimpleTypeInfo(null, null, MX.SimpleType.ThisInfo.LocalName, null,
            MX.TypeKind.SimpleType, MX.SimpleType.ThisInfo.Name, TypeInfo.Instance, DerivationProhibition.None,
            CS.ObjectType, null, typeof(object), true, null) { CSFullName = CSEX.SimpleTypeName, CSFullExp = CSEX.SimpleTypeExp };
        //
        internal override sealed bool IsCSAbstract { get { return false; } }
        internal readonly TypeSyntax ValueCSFullName;
        internal readonly TypeSyntax NullableValueCSFullName;//if value is ref type, same as ValueCSFullName
        public SType ValueClrType { get; private set; }
        internal readonly bool IsValueClrTypeRef;
        MX.TypeKind ISimpleTypeInfo.Kind { get { return base.Kind; } }
        internal readonly FacetSetInfo FacetSet;
        MX.FacetSetInfo ISimpleTypeInfo.FacetSet { get { return FacetSet == null ? null : FacetSet.Value; } }
        internal virtual SimpleTypeInfo ItemType { get { return null; } }
        ISimpleTypeInfo ISimpleTypeInfo.ItemType { get { return ItemType; } }
        internal virtual IReadOnlyList<UnitedSimpleTypeMemberInfo> MemberList { get { return null; } }
        IReadOnlyList<IUnitedSimpleTypeMemberInfo> ISimpleTypeInfo.Members { get { return MemberList; } }
        internal ExpressionSyntax FacetSetExp {
            get {
                if (FacetSet == null) return CS.NullLiteral;
                return FacetSet.GetExp(this);
            }
        }
        internal override ObjectInfo GenerateCS(ObjectInfo parent) {
            if ((parent = base.GenerateCS(parent)) == null) return null;
            if (FacetSet != null && FacetSet.Value.Enumerations != null) {
                var baseFacetSet = FacetSet.BaseFacetSet;
                foreach (var i in FacetSet.Value.Enumerations) {
                    if (i.Name != null) {
                        //public readonly VALUETYPE EnumName = ...;
                        AddCSMember(CS.Field(baseFacetSet != null && baseFacetSet.AllEnumerationsItemNameSet.Contains(i.Name) ? CS.NewPublicStaticReadOnlyTokenList : CS.PublicStaticReadOnlyTokenList,
                            ValueCSFullName, "@" + i.Name, CSEX.SimpleValueLiteral(i.Value)));
                    }
                }
            }
            return parent;
        }
    }
    public sealed class AtomicSimpleTypeInfo : SimpleTypeInfo {
        internal AtomicSimpleTypeInfo(SimpleToken keywordToken, Type obj, string csName, CSClass csClass,
            MX.TypeKind kind, XName name, SimpleTypeInfo baseType, DerivationProhibition derivationProhibition,
            TypeSyntax valueCSFullName, TypeSyntax nullableValueCSFullName, SType valueClrType, bool isValueClrTypeRef, FacetSetInfo facetSet)
            : base(keywordToken, obj, csName, csClass, kind, name, baseType, derivationProhibition, valueCSFullName, nullableValueCSFullName, valueClrType, isValueClrTypeRef, facetSet) { }
        new public SimpleTypeInfo BaseType { get { return (SimpleTypeInfo)base.BaseType; } }
        internal override ObjectInfo GenerateCS(ObjectInfo parent) {
            if ((parent = base.GenerateCS(parent)) == null) return null;
            var baseType = BaseType;
            baseType.GenerateCS(parent);
            //public static implicit operator CLASS(NULLABLEVALUE value){
            //  if (value == null) return null;
            //  return new CLASS{Value=value};
            //}
            AddCSMember(CS.ConversionOperator(true, CSFullName, new[] { CS.Parameter(NullableValueCSFullName, "value") },
                SyntaxFactory.IfStatement(CS.EqualsExpr(CS.IdName("value"), CS.NullLiteral), SyntaxFactory.ReturnStatement(CS.NullLiteral)),
                SyntaxFactory.ReturnStatement(CS.NewObjExpr(CSFullName, null, new[] { CS.AssignExpr(CS.IdName("Value"), CS.IdName("value")) }))));
            //new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(clrType, kind, name, baseType, valueClrType, facetSet);
            AddCSMember(CS.Field(CS.NewPublicStaticReadOnlyTokenList, CSEX.AtomicSimpleTypeInfoName, "ThisInfo",
                CS.NewObjExpr(CSEX.AtomicSimpleTypeInfoName, SyntaxFactory.TypeOfExpression(CSFullName), CSEX.Literal(baseType.Kind), CS.Literal(Name),
                baseType.ThisInfoExp, SyntaxFactory.TypeOfExpression(baseType.ValueCSFullName), FacetSetExp)));
            //public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
            AddCSMember(CSEX.ObjectInfoProperty());
            CreateAndAddCSClass(baseType.CSFullName, parent);
            return parent;
        }
    }
    public sealed class ListedSimpleTypeInfo : SimpleTypeInfo {
        internal ListedSimpleTypeInfo(SimpleToken keywordToken, Type obj, string csName, CSClass csClass,
            XName name, DerivationProhibition derivationProhibition, SimpleTypeInfo itemType)
            : base(keywordToken, obj, csName, csClass, MX.TypeKind.ListedSimpleType, name, SimpleTypeInfo.Instance, derivationProhibition, CSEX.ListedSimpleTypeValueName, null, typeof(ListedSimpleTypeValue), true, null) {
            if (itemType == null) throw new ArgumentNullException("itemType");
            _itemType = itemType;
        }
        internal ListedSimpleTypeInfo(SimpleToken keywordToken, Type obj, string csName, CSClass csClass,
            XName name, ListedSimpleTypeInfo baseType, DerivationProhibition derivationProhibition, FacetSetInfo facetSet)
            : base(keywordToken, obj, csName, csClass, MX.TypeKind.ListedSimpleType, name, baseType, derivationProhibition, CSEX.ListedSimpleTypeValueName, null, typeof(ListedSimpleTypeValue), true, facetSet) { }
        new internal SimpleTypeInfo BaseType { get { return (SimpleTypeInfo)base.BaseType; } }
        private readonly SimpleTypeInfo _itemType;//opt
        internal override SimpleTypeInfo ItemType { get { return _itemType ?? BaseType.ItemType; } }
        internal bool IsRestricting { get { return _itemType == null; } }
        //
        internal override ObjectInfo GenerateCS(ObjectInfo parent) {
            if ((parent = base.GenerateCS(parent)) == null) return null;
            //public static implicit operator CLASS(ListedSimpleTypeValue value){
            //  if (value == null) return null;
            //  return new CLASS{Value=value};
            //}
            AddCSMember(CS.ConversionOperator(true, CSFullName, new[] { CS.Parameter(CSEX.ListedSimpleTypeValueName, "value") },
                SyntaxFactory.IfStatement(CS.EqualsExpr(CS.IdName("value"), CS.NullLiteral), SyntaxFactory.ReturnStatement(CS.NullLiteral)),
                SyntaxFactory.ReturnStatement(CS.NewObjExpr(CSFullName, null, new[] { CS.AssignExpr(CS.IdName("Value"), CS.IdName("value")) }))));
            NameSyntax baseClassFullName;
            if (IsRestricting) {
                BaseType.GenerateCS(parent);
                //new public static readonly ListedSimpleTypeInfo ThisInfo = new ListedSimpleTypeInfo(clrType, name, baseType, facetSet);
                AddCSMember(CS.Field(CS.NewPublicStaticReadOnlyTokenList, CSEX.ListedSimpleTypeInfoName, "ThisInfo",
                    CS.NewObjExpr(CSEX.ListedSimpleTypeInfoName, SyntaxFactory.TypeOfExpression(CSFullName), CS.Literal(Name), BaseType.ThisInfoExp, FacetSetExp)));
                baseClassFullName = BaseType.CSFullName;
            }
            else {
                _itemType.GenerateCS(this);
                //protected override VALUE TryGetTypedItem(object itemValue) { VALUE r; SimpleType.TryGetTypedValue(itemValue, out r); return r;}
                AddCSMember(CS.Method(CS.ProtectedOverrideTokenList, _itemType.NullableValueCSFullName, "TryGetTypedItem", new[] { CS.Parameter(CS.ObjectType, "itemValue") },
                    CSEX.TryGetTypedValueStatements(_itemType.NullableValueCSFullName, CS.IdName("itemValue"))));
                //new public static readonly ListedSimpleTypeInfo ThisInfo = new ListedSimpleTypeInfo(clrType, name, itemType);
                AddCSMember(CS.Field(CS.NewPublicStaticReadOnlyTokenList, CSEX.ListedSimpleTypeInfoName, "ThisInfo",
                    CS.NewObjExpr(CSEX.ListedSimpleTypeInfoName, SyntaxFactory.TypeOfExpression(CSFullName), CS.Literal(Name), _itemType.ThisInfoExp)));
                baseClassFullName = CSEX.ListedSimpleTypeOf(_itemType.NullableValueCSFullName);
            }
            //public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
            AddCSMember(CSEX.ObjectInfoProperty());
            CreateAndAddCSClass(baseClassFullName, parent);
            return parent;
        }
    }
    public sealed class UnitedSimpleTypeMemberInfo : Info, IUnitedSimpleTypeMemberInfo {
        internal UnitedSimpleTypeMemberInfo(SimpleToken keywordToken, Identifier nameId, SimpleTypeInfo type)
            : base(keywordToken) {
            NameId = nameId;
            Type = type;
        }
        internal readonly Identifier NameId;
        internal string Name { get { return NameId.Value; } }
        internal string PlainName { get { return NameId.PlainValue; } }
        string IUnitedSimpleTypeMemberInfo.Name { get { return PlainName; } }
        internal readonly SimpleTypeInfo Type;
        ISimpleTypeInfo IUnitedSimpleTypeMemberInfo.Type { get { return Type; } }
        internal void GenerateCS(UnitedSimpleTypeInfo parent) {
            Type.GenerateCS(parent);
            //public string M0 { 
            //  get { VALUE r; SimpleType.TryGetTypedValue(base.NetValue, out r); return r; }
            //  set { base.NetValue = value; }
            //}
            parent.AddCSMember(CS.Property(CS.PublicTokenList, Type.NullableValueCSFullName, Name, false,
                default(SyntaxTokenList), CSEX.TryGetTypedValueStatements(Type.NullableValueCSFullName, CS.BaseMemberAccessExpr("NetValue")),
                default(SyntaxTokenList), new[] { SyntaxFactory.ExpressionStatement(CS.AssignExpr(CS.BaseMemberAccessExpr("NetValue"), CS.IdName("value"))) }));
        }
    }
    public sealed class UnitedSimpleTypeInfo : SimpleTypeInfo {
        internal UnitedSimpleTypeInfo(SimpleToken keywordToken, Type obj, string csName, CSClass csClass,
            XName name, DerivationProhibition derivationProhibition, IReadOnlyList<UnitedSimpleTypeMemberInfo> memberList)
            : base(keywordToken, obj, csName, csClass, MX.TypeKind.UnitedSimpleType, name, SimpleTypeInfo.Instance, derivationProhibition, CSEX.UnitedSimpleTypeValueName, null, typeof(UnitedSimpleTypeValue), true, null) {
            if (memberList == null) throw new ArgumentNullException("memberList");
            _memberList = memberList;
        }
        internal UnitedSimpleTypeInfo(SimpleToken keywordToken, Type obj, string csName, CSClass csClass,
            XName name, UnitedSimpleTypeInfo baseType, DerivationProhibition derivationProhibition, FacetSetInfo facetSet)
            : base(keywordToken, obj, csName, csClass, MX.TypeKind.UnitedSimpleType, name, baseType, derivationProhibition, CSEX.UnitedSimpleTypeValueName, null, typeof(UnitedSimpleTypeValue), true, facetSet) { }
        new public SimpleTypeInfo BaseType { get { return (SimpleTypeInfo)base.BaseType; } }
        private readonly IReadOnlyList<UnitedSimpleTypeMemberInfo> _memberList;//opt
        internal override IReadOnlyList<UnitedSimpleTypeMemberInfo> MemberList { get { return _memberList ?? BaseType.MemberList; } }
        internal bool IsRestricting { get { return _memberList == null; } }
        //
        internal override ObjectInfo GenerateCS(ObjectInfo parent) {
            if ((parent = base.GenerateCS(parent)) == null) return null;
            //public static implicit operator CLASS(UnitedSimpleTypeValue value){
            //  if (value == null) return null;
            //  return new CLASS{Value=value};
            //}
            AddCSMember(CS.ConversionOperator(true, CSFullName, new[] { CS.Parameter(CSEX.UnitedSimpleTypeValueName, "value") },
                SyntaxFactory.IfStatement(CS.EqualsExpr(CS.IdName("value"), CS.NullLiteral), SyntaxFactory.ReturnStatement(CS.NullLiteral)),
                SyntaxFactory.ReturnStatement(CS.NewObjExpr(CSFullName, null, new[] { CS.AssignExpr(CS.IdName("Value"), CS.IdName("value")) }))));
            NameSyntax baseClassFullName;
            if (IsRestricting) {
                BaseType.GenerateCS(parent);
                //new public static readonly UnitedSimpleTypeInfo ThisInfo = new UnitedSimpleTypeInfo(clrType, name, baseType, facetSet);
                AddCSMember(CS.Field(CS.NewPublicStaticReadOnlyTokenList, CSEX.UnitedSimpleTypeInfoName, "ThisInfo",
                    CS.NewObjExpr(CSEX.UnitedSimpleTypeInfoName, SyntaxFactory.TypeOfExpression(CSFullName), CS.Literal(Name), BaseType.ThisInfoExp, FacetSetExp)));
                baseClassFullName = BaseType.CSFullName;
            }
            else {
                foreach (var member in _memberList) member.GenerateCS(this);
                //new public static readonly UnitedSimpleTypeInfo ThisInfo = new UnitedSimpleTypeInfo(clrType, name, members);
                AddCSMember(CS.Field(CS.NewPublicStaticReadOnlyTokenList, CSEX.UnitedSimpleTypeInfoName, "ThisInfo",
                    CS.NewObjExpr(CSEX.UnitedSimpleTypeInfoName, SyntaxFactory.TypeOfExpression(CSFullName), CS.Literal(Name),
                        CS.NewArrExpr(CSEX.UnitedSimpleTypeMemberInfoArrayType, _memberList.Select(i =>
                            CS.NewObjExpr(CSEX.UnitedSimpleTypeMemberInfoName, CS.Literal(i.PlainName), i.Type.ThisInfoExp))))));
                baseClassFullName = CSEX.UnitedSimpleTypeName;
            }
            //public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
            AddCSMember(CSEX.ObjectInfoProperty());
            CreateAndAddCSClass(baseClassFullName, parent);
            return parent;
        }
    }
    public sealed class ComplexTypeInfo : TypeInfo {
        internal ComplexTypeInfo(SimpleToken keywordToken, Type obj, string csName, CSClass csClass,
            XName name, ComplexTypeInfo baseType, bool isExtension, bool isAbstract, bool isMixed,
            DerivationProhibition derivationProhibition, InstanceProhibition instanceProhibition,
            AttributeSetInfo attributeSet, SimpleTypeInfo simpleChild, bool needSimpleChildMembers, bool hasSimpleChildClass, ChildStructInfo complexChild)
            : base(keywordToken, obj, csName, csClass, MX.TypeKind.ComplexType, name, baseType ?? TypeInfo.Instance,
                baseType == null ? DerivationMethod.Restriction : (isExtension ? DerivationMethod.Extension : DerivationMethod.Restriction), derivationProhibition) {
            IsAbstract = isAbstract;
            IsMixed = isMixed;
            InstanceProhibition = instanceProhibition;
            AttributeSet = attributeSet;
            SimpleChild = simpleChild;
            NeedSimpleChildMembers = needSimpleChildMembers;
            HasSimpleChildClass = hasSimpleChildClass;
            ComplexChild = complexChild;
        }
        internal bool IsExtension { get { return base.DerivationMethod == X.DerivationMethod.Extension; } }
        internal readonly bool IsAbstract;
        internal override bool IsCSAbstract { get { return IsAbstract; } }
        internal readonly bool IsMixed;
        internal readonly InstanceProhibition InstanceProhibition;
        internal readonly AttributeSetInfo AttributeSet;//opt
        internal readonly SimpleTypeInfo SimpleChild;//opt
        internal readonly bool NeedSimpleChildMembers;
        internal readonly bool HasSimpleChildClass;
        internal readonly ChildStructInfo ComplexChild;//opt
        internal override ObjectInfo GenerateCS(ObjectInfo parent) {
            if ((parent = base.GenerateCS(parent)) == null) return null;
            BaseType.GenerateCS(null);
            var hasComplexTypeBase = BaseType is ComplexTypeInfo;
            if (AttributeSet != null) AttributeSet.GenerateCS(this);
            if (ComplexChild != null) ComplexChild.GenerateCS(this);
            else if (SimpleChild != null) {
                SimpleChild.GenerateCS(this);
                if (NeedSimpleChildMembers) {
                    //new public CLASS SimpleChild {
                    //    get { return base.GenericSimpleChild as CLASS; }
                    //    set { base.GenericSimpleChild = value; }
                    //}
                    AddCSMember(CS.Property(CS.NewPublicTokenList, SimpleChild.CSFullName, "SimpleChild", false,
                        default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.AsExpr(CS.BaseMemberAccessExpr("GenericSimpleChild"), SimpleChild.CSFullName)) },
                        default(SyntaxTokenList), new[] { CS.AssignStm(CS.BaseMemberAccessExpr("GenericSimpleChild"), CS.IdName("value")) }));
                    //new public CLASS EnsureSimpleChild() { return base.EnsureSimpleChild<CLASS>(); }
                    AddCSMember(CS.Method(hasComplexTypeBase ? CS.NewPublicTokenList : CS.PublicTokenList, SimpleChild.CSFullName, "EnsureSimpleChild", null,
                        SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.BaseMemberAccessExpr(CS.GenericName("EnsureSimpleChild", SimpleChild.CSFullName))))));
                    //new public VALUE Value {
                    //    get { var obj = SimpleChild; if(obj == null) return null; return obj.Value;}
                    //    set { EnsureSimpleChild().Value = value; }
                    //}
                    AddCSMember(CS.Property(CS.NewPublicTokenList, SimpleChild.NullableValueCSFullName, "Value", false,
                        default(SyntaxTokenList), CSEX.ObjectMemberOrNullStatements(CS.IdName("SimpleChild"), "Value"),
                        default(SyntaxTokenList), new[] { CS.AssignStm(CS.MemberAccessExpr(CS.InvoExpr(CS.IdName("EnsureSimpleChild")), "Value"), CS.IdName("value")) }));
                }
            }
            //
            //new public static readonly ComplexTypeInfo ThisInfo = new ComplexTypeInfo(clrType, name, baseType, isExtension, isAbstract, instanceProhibition, 
            //  attributeSet, simpleChild, complexChild);
            AddCSMember(CS.Field(CS.NewPublicStaticReadOnlyTokenList, CSEX.ComplexTypeInfoName, "ThisInfo",
                CS.NewObjExpr(CSEX.ComplexTypeInfoName, SyntaxFactory.TypeOfExpression(CSFullName), CS.Literal(Name),
                BaseType.ThisInfoExp, CS.Literal(IsExtension), CS.Literal(IsAbstract), CSEX.Literal(InstanceProhibition),
                AttributeSet == null ? CS.NullLiteral : AttributeSet.ThisInfoExp,
                SimpleChild == null ? CS.NullLiteral : SimpleChild.ThisInfoExp,
                ComplexChild == null ? CS.NullLiteral : ComplexChild.ThisInfoExp)));
            //public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
            AddCSMember(CSEX.ObjectInfoProperty());
            CreateAndAddCSClass(hasComplexTypeBase ? BaseType.CSFullName : CSEX.ComplexTypeName, parent);
            return parent;
        }
    }
    //
    //
    internal sealed class IdentifierSet : HashSet<Identifier> { }
    public sealed class AttributeSetInfo : ObjectInfo {
        internal AttributeSetInfo(SimpleToken keywordToken, RootAttributeSet obj, string csName, CSClass csClass, AttributeSetInfo baseAttributeSet, bool isExtension)
            : base(keywordToken, obj, csName, csClass) {
            if (baseAttributeSet != null) {
                if (isExtension) {
                    AttributeList.AddRange(baseAttributeSet.AttributeList);
                    ThisStartIndex = AttributeList.Count;
                }
                AllMemberNameIdSet.AddRange(baseAttributeSet.AllMemberNameIdSet);
                BaseAttributeSet = baseAttributeSet;
            }
            else if (!isExtension) throw new ArgumentNullException("baseAttributeSet");
            IsExtension = isExtension;
        }
        internal readonly AttributeSetInfo BaseAttributeSet;//opt
        internal readonly bool IsExtension;
        internal int ThisStartIndex;
        internal readonly List<AttributeInfo> AttributeList = new List<AttributeInfo>();
        internal bool ContainsAttribute(XName name) { return AttributeList.Any(i => i.Name == name); }
        internal AttributeInfo TryGetAttribute(XName name) {
            foreach (var i in AttributeList)
                if (i.Name == name) return i;
            return null;
        }
        internal readonly IdentifierSet AllMemberNameIdSet = new IdentifierSet();
        internal WildcardInfo Wildcard;//opt
        internal List<WildcardInfo> TempWildcardList;
        private bool? _isOptional;
        internal bool IsOptional {
            get {
                if (_isOptional == null) _isOptional = AttributeList.All(i => i.IsOptional);
                return _isOptional.Value;
            }
        }
        internal override ObjectInfo GenerateCS(ObjectInfo parent) {
            if ((parent = base.GenerateCS(parent)) == null) return null;
            foreach (var attribute in AttributeList) attribute.GenerateCS(this);
            var hasBase = BaseAttributeSet != null;
            //new public static readonly AttributeSetInfo ThisInfo = new AttributeSetInfo(clrType, attributes, wildcard, isOptional);
            AddCSMember(CS.Field(hasBase ? CS.NewPublicStaticReadOnlyTokenList : CS.PublicStaticReadOnlyTokenList, CSEX.AttributeSetInfoName, "ThisInfo",
                CS.NewObjExpr(CSEX.AttributeSetInfoName, SyntaxFactory.TypeOfExpression(CSFullName),
                CS.NewArrOrNullExpr(CSEX.AttributeInfoArrayType, AttributeList.Select(i => i.ThisInfoExp)),
                Wildcard == null ? CS.NullLiteral : Wildcard.GetExp(this), CS.Literal(IsOptional))));
            //public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
            AddCSMember(CSEX.ObjectInfoProperty());
            CreateAndAddCSClass(hasBase ? BaseAttributeSet.CSFullName : CSEX.AttributeSetName, parent);
            //
            //parent's members
            //
            //new public CLASS AttributeSet {
            //    get { return base.GenericAttributeSet as CLASS; }
            //    set { base.GenericAttributeSet = value; }
            //}
            parent.AddCSMember(CS.Property(CS.NewPublicTokenList, CSFullName, "AttributeSet", false,
                default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.AsExpr(CS.BaseMemberAccessExpr("GenericAttributeSet"), CSFullName)) },
                default(SyntaxTokenList), new[] { CS.AssignStm(CS.BaseMemberAccessExpr("GenericAttributeSet"), CS.IdName("value")) }));
            //new public CLASS EnsureAttributeSet() { return base.EnsureAttributeSet<CLASS>(); }
            parent.AddCSMember(CS.Method(hasBase ? CS.NewPublicTokenList : CS.PublicTokenList, CSFullName, "EnsureAttributeSet", null,
               SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.BaseMemberAccessExpr(CS.GenericName("EnsureAttributeSet", CSFullName))))));
            return parent;
        }
    }
    public sealed class AttributeInfo : ObjectInfo {
        internal AttributeInfo(SimpleToken keywordToken, AttributeBase obj, string csName, CSClass csClass, EntityDeclarationKind kind,
            XName name, Identifier memberNameId, bool isOptional, DefaultOrFixedValueInfo defaultOrFixedValue, SimpleTypeInfo type, AttributeInfo restrictedAttribute)
            : base(keywordToken, obj, csName, csClass) {
            if (name == null) throw new ArgumentNullException("name");
            if (type == null) throw new ArgumentNullException("type");
            Kind = kind;
            _name = name;
            MemberNameId = memberNameId;
            IsOptional = isOptional;
            DefaultOrFixedValue = defaultOrFixedValue;
            _type = type;
            RestrictedAttribute = restrictedAttribute;
        }
        internal AttributeInfo(SimpleToken keywordToken, AttributeBase obj, string csName, CSClass csClass, AttributeInfo referentialAttribute,
            Identifier memberNameId, bool isOptional, DefaultOrFixedValueInfo defaultOrFixedValue, AttributeInfo restrictedAttribute)
            : base(keywordToken, obj, csName, csClass) {
            if (referentialAttribute == null) throw new ArgumentNullException("referentialAttribute");
            if (memberNameId == null) throw new ArgumentNullException("memberNameId");
            Kind = EntityDeclarationKind.Reference;
            ReferentialAttribute = referentialAttribute;
            MemberNameId = memberNameId;
            IsOptional = isOptional;
            DefaultOrFixedValue = defaultOrFixedValue;
            RestrictedAttribute = restrictedAttribute;
        }
        internal readonly EntityDeclarationKind Kind;
        internal bool IsLocal { get { return Kind == EntityDeclarationKind.Local; } }
        //internal bool IsGlobal { get { return Kind == EntityDeclarationKind.Global; } }
        internal bool IsReference { get { return Kind == EntityDeclarationKind.Reference; } }
        internal readonly AttributeInfo ReferentialAttribute;//for att ref
        private readonly XName _name;
        internal XName Name { get { return IsReference ? ReferentialAttribute._name : _name; } }
        internal string LocalName { get { return Name.LocalName; } }
        internal readonly Identifier MemberNameId;//null for global
        internal string MemberName { get { return MemberNameId == null ? null : MemberNameId.Value; } }
        internal string MemberPlainName { get { return MemberNameId == null ? null : MemberNameId.PlainValue; } }
        internal readonly bool IsOptional;
        internal readonly DefaultOrFixedValueInfo DefaultOrFixedValue;//opt
        private readonly SimpleTypeInfo _type;
        internal SimpleTypeInfo Type { get { return IsReference ? ReferentialAttribute._type : _type; } }
        internal readonly AttributeInfo RestrictedAttribute;//opt
        internal override ObjectInfo GenerateCS(ObjectInfo parent) {
            if ((parent = base.GenerateCS(parent)) == null) return null;
            var isRef = IsReference;
            if (isRef) ReferentialAttribute.GenerateCS(null);
            var type = Type;
            type.GenerateCS(this);
            var hasBase = RestrictedAttribute != null;
            if (isRef) {
                //new public REFATT ReferentialAttribute { get{return base.GenericReferentialAttribute as REFATT;} set{base.GenericReferentialAttribute=value;}} 
                AddCSMember(CS.Property(CS.NewPublicTokenList, ReferentialAttribute.CSFullName, "ReferentialAttribute", false,
                    default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.AsExpr(CS.BaseMemberAccessExpr("GenericReferentialAttribute"), ReferentialAttribute.CSFullName)) },
                    default(SyntaxTokenList), new[] { CS.AssignStm(CS.BaseMemberAccessExpr("GenericReferentialAttribute"), CS.IdName("value")) }));
            }
            //new public TYPE Type { get{return base.GenericType as TYPE;} set{base.GenericType=value;}} 
            AddCSMember(CS.Property(CS.NewPublicTokenList, type.CSFullName, "Type", false,
                default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.AsExpr(CS.BaseMemberAccessExpr("GenericType"), type.CSFullName)) },
                default(SyntaxTokenList), new[] { CS.AssignStm(CS.BaseMemberAccessExpr("GenericType"), CS.IdName("value")) }));
            //new public TYPE EnsureType(){return base.EnsureType<TYPE>();}
            AddCSMember(CS.Method(CS.NewPublicTokenList, type.CSFullName, "EnsureType", null,
                SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.BaseMemberAccessExpr(CS.GenericName("EnsureType", type.CSFullName))))));
            //new public VALUE Value {
            //    get { var obj = Type; if(obj == null) return null; return obj.Value; }
            //    set { EnsureType().Value = value; }
            //}
            AddCSMember(CS.Property(CS.NewPublicTokenList, type.NullableValueCSFullName, "Value", false,
                default(SyntaxTokenList), CSEX.ObjectMemberOrNullStatements(CS.IdName("Type"), "Value"),
                default(SyntaxTokenList), new[] { CS.AssignStm(CS.MemberAccessExpr(CS.InvoExpr(CS.IdName("EnsureType")), "Value"), CS.IdName("value")) }));
            if (!hasBase) {
                //public static readonly XName ThisName = XName.Get("", "");
                AddCSMember(CS.Field(CS.PublicStaticReadOnlyTokenList, CS.XNameName, "ThisName", CS.Literal(Name)));
                //protected override XName GetName() { return ThisName; }
                AddCSMember(CS.Method(CS.ProtectedOverrideTokenList, CS.XNameName, "GetName", null, SyntaxFactory.ReturnStatement(CS.IdName("ThisName"))));
            }
            if (isRef) {
                //public static readonly AttributeInfo ThisInfo = new AttributeInfo(clrType, referentialAttribute, memberName, isOptional, defaultOrFixedValue);
                AddCSMember(CS.Field(hasBase ? CS.NewPublicStaticReadOnlyTokenList : CS.PublicStaticReadOnlyTokenList, CSEX.AttributeInfoName, "ThisInfo",
                    CS.NewObjExpr(CSEX.AttributeInfoName, SyntaxFactory.TypeOfExpression(CSFullName), ReferentialAttribute.ThisInfoExp,
                    CS.Literal(MemberPlainName), CS.Literal(IsOptional), CSEX.Literal(DefaultOrFixedValue))));
            }
            else {
                //public static readonly AttributeInfo ThisInfo = new AttributeInfo(clrType, kind, name, memberName, isOptional, defaultOrFixedValue, type);
                AddCSMember(CS.Field(hasBase ? CS.NewPublicStaticReadOnlyTokenList : CS.PublicStaticReadOnlyTokenList, CSEX.AttributeInfoName, "ThisInfo",
                    CS.NewObjExpr(CSEX.AttributeInfoName, SyntaxFactory.TypeOfExpression(CSFullName), CSEX.Literal(Kind), CS.IdName("ThisName"),
                    CS.Literal(MemberPlainName), CS.Literal(IsOptional), CSEX.Literal(DefaultOrFixedValue), type.ThisInfoExp)));
            }
            //public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
            AddCSMember(CSEX.ObjectInfoProperty());
            //class
            CreateAndAddCSClass(hasBase ? RestrictedAttribute.CSFullName : CSEX.AttributeName, parent);
            //AttributeSet member
            if (!IsGlobal) {
                //public ATTCLS ATTNAME{
                //  get{ return base.TryGet(ATTCLS.ThisName) as ATTCLS; }
                //  set{if (value == null) base.Remove(ATTCLS.ThisName); else base.AddOrSet(value);}
                //}
                parent.AddCSMember(CS.Property(hasBase ? CS.NewPublicTokenList : CS.PublicTokenList, CSFullName, MemberNameId.Value, false,
                    default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.AsExpr(CS.InvoExpr(CS.BaseMemberAccessExpr("TryGet"), CS.MemberAccessExpr(CSFullExp, "ThisName")), CSFullName)) },
                    default(SyntaxTokenList), new StatementSyntax[] {
                        SyntaxFactory.IfStatement(CS.EqualsExpr(CS.IdName("value"), CS.NullLiteral),
                            SyntaxFactory.ExpressionStatement(CS.InvoExpr(CS.BaseMemberAccessExpr("Remove"), CS.MemberAccessExpr(CSFullExp, "ThisName"))),
                            SyntaxFactory.ElseClause(SyntaxFactory.ExpressionStatement(CS.InvoExpr(CS.BaseMemberAccessExpr("AddOrSet"), CS.IdName("value"))))) 
                    }));
                //public ATTCLS Ensure_ATTNAME(bool @try = false){return ATTNAME ?? (ATTNAME = base.CreateAttribute<ATTCLS>(ATTCLS.ThisName, @try));}
                parent.AddCSMember(CS.Method(hasBase ? CS.NewPublicTokenList : CS.PublicTokenList, CSFullName, "Ensure_" + MemberNameId.PlainValue, new[] { CS.Parameter(CS.BoolType, "@try", CS.FalseLiteral) },
                    SyntaxFactory.ReturnStatement(CS.CoalesceExpr(CS.IdName(MemberNameId.Value),
                        SyntaxFactory.ParenthesizedExpression(CS.AssignExpr(CS.IdName(MemberNameId.Value),
                            CS.InvoExpr(CS.BaseMemberAccessExpr(CS.GenericName("CreateAttribute", CSFullName)), CS.MemberAccessExpr(CSFullExp, "ThisName"), CS.IdName("@try"))))))));
                //public VALUE ATTNAME_Value {
                //    get { var obj = ATTNAME; if(obj == null) return null; return obj.Value; }
                //    set { Ensure_ATTNAME().Value = value; }
                //}
                parent.AddCSMember(CS.Property(hasBase ? CS.NewPublicTokenList : CS.PublicTokenList, type.NullableValueCSFullName, MemberNameId.Value + "_Value", false,
                    default(SyntaxTokenList), CSEX.ObjectMemberOrNullStatements(CS.IdName(MemberNameId.Value), "Value"),
                    default(SyntaxTokenList), new[] { CS.AssignStm(CS.MemberAccessExpr(CS.InvoExpr(CS.IdName("Ensure_" + MemberNameId.PlainValue)), "Value"), CS.IdName("value")) }));
            }
            return parent;
        }
    }
    //
    //
    public abstract class ChildInfo : ObjectInfo {
        protected ChildInfo(SimpleToken keywordToken, Child obj, string csName, CSClass csClass,
            ulong minOccurs, ulong maxOccurs, bool isListItem, int order, Identifier memberNameId, ChildInfo restrictedChild)
            : base(keywordToken, obj, csName, csClass) {
            MinOccurs = minOccurs;
            MaxOccurs = maxOccurs;
            IsListItem = isListItem;
            Order = order;
            MemberNameId = memberNameId;
            RestrictedChild = restrictedChild;
        }
        internal readonly ulong MinOccurs;
        internal readonly ulong MaxOccurs;
        internal readonly bool IsListItem;
        internal readonly int Order;//-1 for list item, global element, child struct wrapper
        internal readonly Identifier MemberNameId;//opt for list item, global element, child struct wrapper
        internal string MemberName { get { return MemberNameId == null ? null : MemberNameId.Value; } }
        internal string MemberPlainName { get { return MemberNameId == null ? null : MemberNameId.PlainValue; } }
        internal readonly ChildInfo RestrictedChild;//opt
        internal bool IsChoiceMember;
        internal bool IsUnorderedMember;
        internal virtual bool IsEffectiveOptional { get { return MinOccurs == 0; } }
        internal abstract bool HasElementBases { get; }
        internal abstract bool CheckUpaTransitions(IEnumerable<ElementBaseInfo> itemsToCheck);//return true:continue check next;false:stop check next
        internal abstract void GetUpaTransitions(HashSet<ElementBaseInfo> itemSet);
        internal abstract void CheckUpaChoiceSelectors(HashSet<ElementBaseInfo> itemSet);
        internal override ObjectInfo GenerateCS(ObjectInfo parent) {
            if ((parent = base.GenerateCS(parent)) == null) return null;
            if (Order != -1) {
                var hasBase = RestrictedChild != null;
                if (!hasBase) {
                    //public const int ThisOrder = ORDER;
                    AddCSMember(CS.Field(CS.PublicConstTokenList, CS.IntType, "ThisOrder", CS.Literal(Order)));
                    //public override int ChildOrder { get { return ThisOrder(or 0 for choice); } }
                    AddCSMember(CS.Property(CS.PublicOverrideTokenList, CS.IntType, "ChildOrder", true, default(SyntaxTokenList),
                        new[] { SyntaxFactory.ReturnStatement(IsChoiceMember ? (ExpressionSyntax)CS.Literal(0) : CS.IdName("ThisOrder")) }));
                    if (IsUnorderedMember) {
                        //private int? _specifiedOrder;
                        AddCSMember(CS.Field(CS.PrivateTokenList, CS.IntNullableType, "_specifiedOrder"));
                        //public override int SpecifiedOrder{
                        // get{
                        //   if(_specifiedOrder!=null) return _specifiedOrder.Value;
                        //   return ThisOrder;
                        // }
                        // set{_specifiedOrder=value;}
                        //}
                        AddCSMember(CS.Property(CS.PublicOverrideTokenList, CS.IntType, "SpecifiedOrder", false,
                           default(SyntaxTokenList), new StatementSyntax[] { 
                                SyntaxFactory.IfStatement(CS.NotEqualsExpr(CS.IdName("_specifiedOrder"), CS.NullLiteral),
                                    SyntaxFactory.ReturnStatement(CS.MemberAccessExpr(CS.IdName("_specifiedOrder"), "Value"))),
                                SyntaxFactory.ReturnStatement(CS.IdName("ThisOrder"))
                           },
                           default(SyntaxTokenList), new[] { CS.AssignStm(CS.IdName("_specifiedOrder"), CS.IdName("value")) }));
                    }
                }
                //public CHILDCLS CHILDNAME {
                //    get { return base.TryGet(CHILDCLS.ThisOrder(or 0 for choice)) as CHILDCLS; }
                //    set { if (value == null) base.Remove(CHILDCLS.ThisOrder(or 0 for choice)); else base.AddOrSet(value); }
                //}
                parent.AddCSMember(CS.Property(hasBase ? CS.NewPublicTokenList : CS.PublicTokenList, CSFullName, MemberNameId.Value, false,
                    default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.AsExpr(CS.InvoExpr(CS.BaseMemberAccessExpr("TryGet"),
                        IsChoiceMember ? (ExpressionSyntax)CS.Literal(0) : CS.MemberAccessExpr(CSFullExp, "ThisOrder")), CSFullName)) },
                    default(SyntaxTokenList), new StatementSyntax[] {
                        SyntaxFactory.IfStatement(CS.EqualsExpr(CS.IdName("value"), CS.NullLiteral),
                            SyntaxFactory.ExpressionStatement(CS.InvoExpr(CS.BaseMemberAccessExpr("Remove"),
                            IsChoiceMember ? (ExpressionSyntax)CS.Literal(0) : CS.MemberAccessExpr(CSFullExp, "ThisOrder"))),
                            SyntaxFactory.ElseClause(SyntaxFactory.ExpressionStatement(CS.InvoExpr(CS.BaseMemberAccessExpr("AddOrSet"), CS.IdName("value"))))) 
                    }));
                //public CHILDCLS Ensure_CHILDNAME(bool @try = false) { return CHILDNAME ?? (CHILDNAME = base.CreateChildMember<CHILDCLS>(CHILDCLS.ThisOrder, @try)); }
                parent.AddCSMember(CS.Method(hasBase ? CS.NewPublicTokenList : CS.PublicTokenList, CSFullName, "Ensure_" + MemberNameId.PlainValue, new[] { CS.Parameter(CS.BoolType, "@try", CS.FalseLiteral) },
                    SyntaxFactory.ReturnStatement(CS.CoalesceExpr(CS.IdName(MemberNameId.Value),
                        SyntaxFactory.ParenthesizedExpression(CS.AssignExpr(CS.IdName(MemberNameId.Value),
                            CS.InvoExpr(CS.BaseMemberAccessExpr(CS.GenericName("CreateChildMember", CSFullName)), CS.MemberAccessExpr(CSFullExp, "ThisOrder"), CS.IdName("@try"))))))));
            }
            return parent;
        }
    }
    public abstract class ElementBaseInfo : ChildInfo {
        protected ElementBaseInfo(SimpleToken keywordToken, Child obj, string csName, CSClass csClass,
            ulong minOccurs, ulong maxOccurs, bool isListItem, int order, Identifier memberNameId, ElementBaseInfo restrictedElementBase)
            : base(keywordToken, obj, csName, csClass, minOccurs, maxOccurs, isListItem, order, memberNameId, restrictedElementBase) { }
        internal ElementBaseInfo RestrictedElementBase { get { return (ElementBaseInfo)base.RestrictedChild; } }
        internal override sealed bool HasElementBases { get { return true; } }
        internal override sealed void GetUpaTransitions(HashSet<ElementBaseInfo> itemSet) {
            if (MinOccurs != MaxOccurs) itemSet.Add(this);
        }
        internal override sealed bool CheckUpaTransitions(IEnumerable<ElementBaseInfo> itemsToCheck) {
            foreach (var itemToCheck in itemsToCheck)
                if (this != itemToCheck && IsUpaIntersectWith(itemToCheck)) CompilationContext.Throw(KeywordToken, ErrorKind.UpaViolated);
            return MinOccurs == 0;
        }
        internal override sealed void CheckUpaChoiceSelectors(HashSet<ElementBaseInfo> itemSet) {
            foreach (var item in itemSet)
                if (IsUpaIntersectWith(item)) CompilationContext.Throw(KeywordToken, ErrorKind.UpaViolated);
            itemSet.Add(this);
        }
        internal abstract bool IsUpaIntersectWith(ElementBaseInfo item);
    }
    public sealed class ElementInfo : ElementBaseInfo {
        internal ElementInfo(SimpleToken keywordToken, ElementBase obj, string csName, CSClass csClass,
            ulong minOccurs, ulong maxOccurs, bool isListItem, int order, Identifier memberNameId, ElementBaseInfo restrictedElementBase,
            EntityDeclarationKind kind, XName name, bool isAbstract, bool isNullable, DerivationProhibition derivationProhibition,
            InstanceProhibition instanceProhibition, DefaultOrFixedValueInfo defaultOrFixedValue, TypeInfo type,
            ElementInfo substitutedElement, GlobalElement globalElementObject, IReadOnlyList<IdentityConstraintInfo> identityConstraints)
            : base(keywordToken, obj, csName, csClass, minOccurs, maxOccurs, isListItem, order, memberNameId, restrictedElementBase) {
            if (name == null) throw new ArgumentNullException("name");
            if (type == null) throw new ArgumentNullException("type");
            Kind = kind;
            _name = name;
            _isAbstract = isAbstract;
            _isNullable = isNullable;
            _derivationProhibition = derivationProhibition;
            _instanceProhibition = instanceProhibition;
            _defaultOrFixedValue = defaultOrFixedValue;
            _type = type;
            _substitutedElement = substitutedElement;
            _globalElementObject = globalElementObject;
            _identityConstraints = identityConstraints;
        }
        internal ElementInfo(SimpleToken keywordToken, ElementBase obj, string csName, CSClass csClass,
            ulong minOccurs, ulong maxOccurs, bool isListItem, int order, Identifier memberNameId, ElementBaseInfo restrictedElementBase,
            ElementInfo referentialElement)
            : base(keywordToken, obj, csName, csClass, minOccurs, maxOccurs, isListItem, order, memberNameId, restrictedElementBase) {
            if (referentialElement == null) throw new ArgumentNullException("referentialElement");
            Kind = EntityDeclarationKind.Reference;
            ReferentialElement = referentialElement;
        }
        internal readonly EntityDeclarationKind Kind;
        internal bool IsLocal { get { return Kind == EntityDeclarationKind.Local; } }
        //internal bool IsGlobal { get { return Kind == EntityDeclarationKind.Global; } }
        internal bool IsReference { get { return Kind == EntityDeclarationKind.Reference; } }
        internal readonly ElementInfo ReferentialElement;//for element ref
        private readonly XName _name;
        internal XName Name { get { return IsReference ? ReferentialElement._name : _name; } }
        internal string LocalName { get { return Name.LocalName; } }
        private readonly bool _isAbstract;
        internal bool IsAbstract { get { return IsReference ? ReferentialElement._isAbstract : _isAbstract; } }
        internal override bool IsCSAbstract { get { return IsGlobal ? _isAbstract : false; } }
        private readonly bool _isNullable;
        internal bool IsNullable { get { return IsReference ? ReferentialElement._isNullable : _isNullable; } }
        private readonly DerivationProhibition _derivationProhibition;
        internal DerivationProhibition DerivationProhibition { get { return IsReference ? ReferentialElement._derivationProhibition : _derivationProhibition; } }
        private readonly InstanceProhibition _instanceProhibition;
        internal InstanceProhibition InstanceProhibition { get { return IsReference ? ReferentialElement._instanceProhibition : _instanceProhibition; } }
        private readonly DefaultOrFixedValueInfo _defaultOrFixedValue;//opt
        internal DefaultOrFixedValueInfo DefaultOrFixedValue { get { return IsReference ? ReferentialElement._defaultOrFixedValue : _defaultOrFixedValue; } }
        private readonly TypeInfo _type;
        internal TypeInfo Type { get { return IsReference ? ReferentialElement._type : _type; } }
        private readonly ElementInfo _substitutedElement;//opt, for global element
        internal ElementInfo SubstitutedElement { get { return IsReference ? ReferentialElement._substitutedElement : _substitutedElement; } }
        private readonly GlobalElement _globalElementObject;//for global element
        internal GlobalElement GlobalElementObject { get { return IsReference ? ReferentialElement._globalElementObject : _globalElementObject; } }
        private readonly IReadOnlyList<IdentityConstraintInfo> _identityConstraints;//opt
        internal IReadOnlyList<IdentityConstraintInfo> IdentityConstraints { get { return IsReference ? ReferentialElement._identityConstraints : _identityConstraints; } }
        internal bool IsMatch(XName name) {
            if (IsReference) return ReferentialElement.IsMatch(name);
            if (name == _name) return true;
            if (IsGlobal) return GlobalElementObject.TryGet(name) != null;
            return false;
        }
        internal IEnumerable<XName> UpaNames {
            get {
                if (IsReference) {
                    foreach (var i in GlobalElementObject.SelfAndSubstitutingElementNames) yield return i;
                }
                else yield return Name;
            }
        }
        internal override bool IsUpaIntersectWith(ElementBaseInfo item) {
            var element = item as ElementInfo;
            if (element != null) {
                foreach (var i in UpaNames)
                    foreach (var j in element.UpaNames)
                        if (i == j) return true;
                return false;
            }
            return ((ElementWildcardInfo)item).IsUpaIntersectWith(this);
        }
        internal override ObjectInfo GenerateCS(ObjectInfo parent) {
            if ((parent = base.GenerateCS(parent)) == null) return null;
            var isRef = IsReference;
            if (isRef) ReferentialElement.GenerateCS(null);
            if (_substitutedElement != null) _substitutedElement.GenerateCS(null);
            var type = Type;
            type.GenerateCS(this);
            var baseElementBase = RestrictedElementBase ?? SubstitutedElement;
            var hasBase = baseElementBase != null;
            var hasElementBase = baseElementBase is ElementInfo;
            if (isRef) {
                //new public REFELE ReferentialElement { get{return base.GenericReferentialElement as REFELE;} set{base.GenericReferentialElement=value;}} 
                AddCSMember(CS.Property(CS.NewPublicTokenList, ReferentialElement.CSFullName, "ReferentialElement", false,
                    default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.AsExpr(CS.BaseMemberAccessExpr("GenericReferentialElement"), ReferentialElement.CSFullName)) },
                    default(SyntaxTokenList), new[] { CS.AssignStm(CS.BaseMemberAccessExpr("GenericReferentialElement"), CS.IdName("value")) }));
            }
            //new public TYPE Type { get{return base.GenericType as TYPE;} set{base.GenericType=value;}}
            AddCSMember(CS.Property(CS.NewPublicTokenList, type.CSFullName, "Type", false,
                default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.AsExpr(CS.BaseMemberAccessExpr("GenericType"), type.CSFullName)) },
                default(SyntaxTokenList), new[] { CS.AssignStm(CS.BaseMemberAccessExpr("GenericType"), CS.IdName("value")) }));
            //new public TYPE EnsureType(bool @try = false){return base.EnsureType<TYPE>(@try);}
            AddCSMember(CS.Method(CS.NewPublicTokenList, type.CSFullName, "EnsureType", new[] { CS.Parameter(CS.BoolType, "@try", CS.FalseLiteral) },
                SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.BaseMemberAccessExpr(CS.GenericName("EnsureType", type.CSFullName)), CS.IdName("@try")))));
            var complexType = type as ComplexTypeInfo;
            if (complexType != null) {
                if (complexType.AttributeSet != null) {
                    //new public CLASS AttributeSet {
                    //    get { return base.GenericAttributeSet as CLASS; }
                    //    set { EnsureType().AttributeSet = value; }
                    //}
                    AddCSMember(CS.Property(CS.NewPublicTokenList, complexType.AttributeSet.CSFullName, "AttributeSet", false,
                        default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.AsExpr(CS.BaseMemberAccessExpr("GenericAttributeSet"), complexType.AttributeSet.CSFullName)) },
                        default(SyntaxTokenList), new[] { CS.AssignStm(CS.MemberAccessExpr(CS.InvoExpr(CS.IdName("EnsureType")), "AttributeSet"), CS.IdName("value")) }));
                    //new public CLASS EnsureAttributeSet(bool @try = false) { return EnsureType(@try).EnsureAttributeSet(); }
                    AddCSMember(CS.Method(CS.NewPublicTokenList, complexType.AttributeSet.CSFullName, "EnsureAttributeSet", new[] { CS.Parameter(CS.BoolType, "@try", CS.FalseLiteral) },
                       SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.MemberAccessExpr(CS.InvoExpr(CS.IdName("EnsureType"), CS.IdName("@try")), "EnsureAttributeSet")))));
                }
                if (complexType.ComplexChild != null) {
                    //new public CLASS ComplexChild {
                    //    get { return base.GenericComplexChild as CLASS; }
                    //    set { EnsureType().ComplexChild = value; }
                    //}
                    AddCSMember(CS.Property(CS.NewPublicTokenList, complexType.ComplexChild.CSFullName, "ComplexChild", false,
                        default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.AsExpr(CS.BaseMemberAccessExpr("GenericComplexChild"), complexType.ComplexChild.CSFullName)) },
                        default(SyntaxTokenList), new[] { CS.AssignStm(CS.MemberAccessExpr(CS.InvoExpr(CS.IdName("EnsureType")), "ComplexChild"), CS.IdName("value")) }));
                    //new public CLASS EnsureComplexChild(bool @try = false) { return EnsureType(@try).EnsureComplexChild(); }
                    AddCSMember(CS.Method(CS.NewPublicTokenList, complexType.ComplexChild.CSFullName, "EnsureComplexChild", new[] { CS.Parameter(CS.BoolType, "@try", CS.FalseLiteral) },
                       SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.MemberAccessExpr(CS.InvoExpr(CS.IdName("EnsureType"), CS.IdName("@try")), "EnsureComplexChild")))));
                }
                else if (complexType.SimpleChild != null) {
                    //new public CLASS SimpleChild {
                    //    get { return base.GenericSimpleChild as CLASS; }
                    //    set { EnsureType().SimpleChild = value; }
                    //}
                    AddCSMember(CS.Property(CS.NewPublicTokenList, complexType.SimpleChild.CSFullName, "SimpleChild", false,
                        default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.AsExpr(CS.BaseMemberAccessExpr("GenericSimpleChild"), complexType.SimpleChild.CSFullName)) },
                        default(SyntaxTokenList), new[] { CS.AssignStm(CS.MemberAccessExpr(CS.InvoExpr(CS.IdName("EnsureType")), "SimpleChild"), CS.IdName("value")) }));
                    //new public CLASS EnsureSimpleChild(bool @try = false) { return EnsureType(@try).EnsureSimpleChild(); }
                    AddCSMember(CS.Method(CS.NewPublicTokenList, complexType.SimpleChild.CSFullName, "EnsureSimpleChild", new[] { CS.Parameter(CS.BoolType, "@try", CS.FalseLiteral) },
                       SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.MemberAccessExpr(CS.InvoExpr(CS.IdName("EnsureType"), CS.IdName("@try")), "EnsureSimpleChild")))));
                    //new public VALUE Value {
                    //    get { var obj = SimpleChild; if(obj == null) return null; return obj.Value; }
                    //    set { EnsureSimpleChild().Value = value; }
                    //}
                    AddCSMember(CS.Property(CS.NewPublicTokenList, complexType.SimpleChild.NullableValueCSFullName, "Value", false,
                        default(SyntaxTokenList), CSEX.ObjectMemberOrNullStatements(CS.IdName("SimpleChild"), "Value"),
                        default(SyntaxTokenList), new[] { CS.AssignStm(CS.MemberAccessExpr(CS.InvoExpr(CS.IdName("EnsureSimpleChild")), "Value"), CS.IdName("value")) }));
                }
            }
            else {
                var simpleType = type as SimpleTypeInfo;
                if (simpleType != null) {
                    //new public VALUE Value {
                    //    get { var obj = Type; if(obj == null) return null; return obj.Value; }
                    //    set { EnsureType().Value = value; }
                    //}
                    AddCSMember(CS.Property(CS.NewPublicTokenList, simpleType.NullableValueCSFullName, "Value", false,
                        default(SyntaxTokenList), CSEX.ObjectMemberOrNullStatements(CS.IdName("Type"), "Value"),
                        default(SyntaxTokenList), new[] { CS.AssignStm(CS.MemberAccessExpr(CS.InvoExpr(CS.IdName("EnsureType")), "Value"), CS.IdName("value")) }));
                }
            }
            var identityConstraints = _identityConstraints;
            if (identityConstraints != null) {
                foreach (var ic in identityConstraints) {
                    var icName = ic.Name.LocalName;
                    var icNameName = icName + "_ConstraintName";
                    //public static readonly XName Name_ConstraintName = XName.Get("", "");
                    AddCSMember(CS.Field(CS.PublicStaticReadOnlyTokenList, CS.XNameName, icNameName, CS.Literal(ic.Name)));
                    //public IdentityConstraint Name_Constraint{get{ return base.TryGetIdentityConstraint(Name_ConstraintName); }}
                    AddCSMember(CS.Property(CS.PublicTokenList, CSEX.IdentityConstraintName, icName + "_Constraint", true,
                        default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.BaseMemberAccessExpr("TryGetIdentityConstraint"), CS.IdName(icNameName))) }
                        ));
                }
            }
            if (Kind == EntityDeclarationKind.Global) {
                //public static bool TryLoadAndValidate<T>(XmlReader reader, Context context, out CLASS result) { return TryLoadAndSpecialize(reader, context, ThisInfo, out result); }
                AddCSMember(CS.Method(CS.PublicStaticTokenList, CS.BoolType, "TryLoadAndValidate",
                    new[] { CS.Parameter(CS.XmlReaderName, "reader"), CS.Parameter(CSEX.ContextName, "context"), CS.OutParameter(CSFullName, "result") },
                    SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.IdName("TryLoadAndSpecialize"),
                        SyntaxFactory.Argument(CS.IdName("reader")), SyntaxFactory.Argument(CS.IdName("context")), SyntaxFactory.Argument(CS.IdName("ThisInfo")), CS.OutArgument("result")))));
            }
            //public static readonly XName ThisName = XName.Get("", "");
            AddCSMember(CS.Field(hasElementBase ? CS.NewPublicStaticReadOnlyTokenList : CS.PublicStaticReadOnlyTokenList, CS.XNameName, "ThisName", CS.Literal(Name)));
            //protected override XName GetName() { return ThisName; }
            AddCSMember(CS.Method(CS.ProtectedOverrideTokenList, CS.XNameName, "GetName", null, SyntaxFactory.ReturnStatement(CS.IdName("ThisName"))));
            if (isRef) {
                //public static readonly ElementInfo ThisInfo = new ElementInfo(clrType, memberName, isEffectiveOptional, referentialElement);
                AddCSMember(CS.Field(hasBase ? CS.NewPublicStaticReadOnlyTokenList : CS.PublicStaticReadOnlyTokenList, CSEX.ElementInfoName, "ThisInfo",
                    CS.NewObjExpr(CSEX.ElementInfoName, SyntaxFactory.TypeOfExpression(CSFullName), CS.Literal(MemberPlainName), CS.Literal(IsEffectiveOptional), ReferentialElement.ThisInfoExp)));
            }
            else {
                //public static readonly ElementInfo ThisInfo = new ElementInfo(clrType, memberName, isEffectiveOptional,
                //  kind, program, name, isAbstract, isNullable, instanceProhibition, defaultOrFixedValue, type,
                //  substitutedElement, directSubstitutingElementRefs, identityConstraints);
                AddCSMember(CS.Field(hasBase ? CS.NewPublicStaticReadOnlyTokenList : CS.PublicStaticReadOnlyTokenList, CSEX.ElementInfoName, "ThisInfo",
                    CS.NewObjExpr(CSEX.ElementInfoName, SyntaxFactory.TypeOfExpression(CSFullName), CS.Literal(MemberPlainName), CS.Literal(IsEffectiveOptional),
                    CSEX.Literal(Kind), CSEX.XProgramInfoInstanceExp, CS.IdName("ThisName"), CS.Literal(_isAbstract), CS.Literal(_isNullable),
                    CSEX.Literal(_instanceProhibition), CSEX.Literal(_defaultOrFixedValue), type.ThisInfoExp, _substitutedElement == null ? CS.NullLiteral : _substitutedElement.ThisInfoExp,
                    CS.NewArrOrNullExpr(CSEX.GlobalObjectRefInfoArrayType, _globalElementObject == null ? null : _globalElementObject.DirectSubstitutingElementList.Select(i => CS.NewObjExpr(CSEX.GlobalObjectRefInfoName, CSEX.XProgramInfoInstanceExp, CS.Literal(i.FullName), CSEX.Literal(GlobalObjectKind.Element)))),
                    CS.NewArrOrNullExpr(CSEX.IdentityConstraintInfoArrayType, identityConstraints == null ? null : identityConstraints.Select(i => CSEX.Literal(i))))));
            }
            //public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
            AddCSMember(CSEX.ObjectInfoProperty());
            //class
            CreateAndAddCSClass(hasBase ? baseElementBase.CSFullName : CSEX.ElementName, parent);
            return parent;
        }
    }
    public sealed class ElementWildcardInfo : ElementBaseInfo {
        internal ElementWildcardInfo(SimpleToken keywordToken, ElementWildcard obj, string csName, CSClass csClass,
            ulong minOccurs, ulong maxOccurs, bool isListItem, int order, Identifier memberNameId, ElementWildcardInfo restrictedElementWildcard,
            WildcardInfo wildcard)
            : base(keywordToken, obj, csName, csClass, minOccurs, maxOccurs, isListItem, order, memberNameId, restrictedElementWildcard) {
            if (wildcard == null) throw new ArgumentNullException("wildcard");
            Wildcard = wildcard;
        }
        internal readonly WildcardInfo Wildcard;
        internal ElementWildcardInfo RestrictedElementWildcard { get { return (ElementWildcardInfo)base.RestrictedChild; } }
        internal bool IsMatch(XNamespace value) { return Wildcard.IsMatch(value); }
        internal override bool IsUpaIntersectWith(ElementBaseInfo item) {
            var element = item as ElementInfo;
            if (element != null) {
                foreach (var name in element.UpaNames)
                    if (Wildcard.IsMatch(name.Namespace)) return true;
                return false;
            }
            return Wildcard.IsIntersectWith(((ElementWildcardInfo)item).Wildcard);
        }
        internal override ObjectInfo GenerateCS(ObjectInfo parent) {
            if ((parent = base.GenerateCS(parent)) == null) return null;
            var hasBase = RestrictedElementWildcard != null;
            //public static readonly ElementWildcardInfo ThisInfo = new ElementWildcardInfo(clrType, memberName, isEffectiveOptional, program, wildcard);
            AddCSMember(CS.Field(hasBase ? CS.NewPublicStaticReadOnlyTokenList : CS.PublicStaticReadOnlyTokenList, CSEX.ElementWildcardInfoName, "ThisInfo",
                CS.NewObjExpr(CSEX.ElementWildcardInfoName, SyntaxFactory.TypeOfExpression(CSFullName), CS.Literal(MemberPlainName), CS.Literal(IsEffectiveOptional),
                CSEX.XProgramInfoInstanceExp, Wildcard.GetExp(this))));
            //public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
            AddCSMember(CSEX.ObjectInfoProperty());
            //class
            CreateAndAddCSClass(hasBase ? RestrictedElementWildcard.CSFullName : CSEX.ElementName, parent);
            return parent;
        }
    }
    public abstract class ChildContainerInfo : ChildInfo {
        protected ChildContainerInfo(SimpleToken keywordToken, Child obj, string csName, CSClass csClass,
            ulong minOccurs, ulong maxOccurs, bool isListItem, int order, Identifier memberNameId, ChildContainerInfo restrictedChildContainer,
            ChildContainerKind kind, bool isMixed)
            : base(keywordToken, obj, csName, csClass, minOccurs, maxOccurs, isListItem, order, memberNameId, restrictedChildContainer) {
            Kind = kind;
            IsMixed = isMixed;
        }
        internal readonly ChildContainerKind Kind;
        internal readonly bool IsMixed;
        internal ChildContainerInfo RestrictedChildContainer { get { return (ChildContainerInfo)base.RestrictedChild; } }
    }
    public sealed class ChildListInfo : ChildContainerInfo {
        internal ChildListInfo(SimpleToken keywordToken, Child obj, string csName, CSClass csClass,
            ulong minOccurs, ulong maxOccurs, int order, Identifier memberNameId, ChildListInfo restrictedChildList,
            bool isMixed, ChildInfo item)
            : base(keywordToken, obj, csName, csClass, minOccurs, maxOccurs, false, order, memberNameId, restrictedChildList, ChildContainerKind.List, isMixed) {
            if (item == null) throw new ArgumentNullException("item");
            Item = item;
        }
        internal readonly ChildInfo Item;
        internal ChildListInfo RestrictedChildList { get { return (ChildListInfo)base.RestrictedChild; } }
        internal override bool HasElementBases { get { return Item.HasElementBases; } }
        internal override bool CheckUpaTransitions(IEnumerable<ElementBaseInfo> itemsToCheck) { return Item.CheckUpaTransitions(itemsToCheck); }
        internal override void GetUpaTransitions(HashSet<ElementBaseInfo> itemSet) { Item.GetUpaTransitions(itemSet); }
        internal override void CheckUpaChoiceSelectors(HashSet<ElementBaseInfo> itemSet) { Item.CheckUpaChoiceSelectors(itemSet); }
        internal override ObjectInfo GenerateCS(ObjectInfo parent) {
            if ((parent = base.GenerateCS(parent)) == null) return null;
            Item.GenerateCS(this);
            var hasBase = RestrictedChildList != null;
            if (hasBase) {
                //new public IList<ITEMCLS> ItemList { get { return this; } }
                AddCSMember(CS.Property(CS.NewPublicTokenList, CS.IListOf(Item.CSFullName), "ItemList", true,
                    default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(SyntaxFactory.ThisExpression()) }));
                AddCSMembers(CSEX.IListOverrideMembers(Item.CSFullName));
            }
            //public ITEMCLS CreateItem() {return base.CreateItem<ITEMCLS>(); }
            AddCSMember(CS.Method(hasBase ? CS.NewPublicTokenList : CS.PublicTokenList, Item.CSFullName, "CreateItem", null,
                SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.BaseMemberAccessExpr(CS.GenericName("CreateItem", Item.CSFullName))))));
            //public ITEMCLS CreateAndAddItem() {return base.CreateAndAddItem<ITEMCLS>(); }
            AddCSMember(CS.Method(hasBase ? CS.NewPublicTokenList : CS.PublicTokenList, Item.CSFullName, "CreateAndAddItem", null,
                SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.BaseMemberAccessExpr(CS.GenericName("CreateAndAddItem", Item.CSFullName))))));
            //public static readonly ChildListInfo ThisInfo = new ChildListInfo(clrType, memberName, isEffectiveOptional, isMixed, minOccurs, maxOccurs, item);
            AddCSMember(CS.Field(hasBase ? CS.NewPublicStaticReadOnlyTokenList : CS.PublicStaticReadOnlyTokenList, CSEX.ChildListInfoName, "ThisInfo",
                CS.NewObjExpr(CSEX.ChildListInfoName, SyntaxFactory.TypeOfExpression(CSFullName), CS.Literal(MemberName), CS.Literal(IsEffectiveOptional), CS.Literal(IsMixed),
                CS.Literal(MinOccurs), CS.Literal(MaxOccurs), Item.ThisInfoExp)));
            //public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
            AddCSMember(CSEX.ObjectInfoProperty());
            //class
            CreateAndAddCSClass(hasBase ? new[] { RestrictedChildList.CSFullName, CS.IListOf(Item.CSFullName), CS.IReadOnlyListOf(Item.CSFullName) }
                : new[] { CSEX.ChildListOf(Item.CSFullName) }, parent);
            return parent;
        }
    }
    public sealed class ChildStructInfo : ChildContainerInfo {
        internal ChildStructInfo(SimpleToken keywordToken, ChildStructBase obj, string csName, CSClass csClass,
            ulong minOccurs, ulong maxOccurs, bool isListItem, int order, Identifier memberNameId, ChildStructInfo restrictedChildStruct,
            ChildContainerKind kind, bool isMixed, IEnumerable<ChildInfo> members)
            : base(keywordToken, obj, csName, csClass, minOccurs, maxOccurs, isListItem, order, memberNameId, restrictedChildStruct, kind, isMixed) {
            if (members != null) MemberList.AddRange(members);
        }
        internal ChildStructInfo RestrictedChildStruct { get { return (ChildStructInfo)base.RestrictedChild; } }
        internal readonly List<ChildInfo> MemberList = new List<ChildInfo>();
        internal IEnumerable<ChildInfo> NonNullMembers { get { return MemberList.Where(i => i != null); } }
        //for root
        internal bool IsRoot;
        internal ChildStructInfo BaseChildStruct;
        internal bool IsExtension;
        internal int ThisStartIndex;
        internal bool ContainsUnordered;
        private IdentifierSet _allMemberNameIdSet;
        internal IdentifierSet AllMemberNameIdSet { get { return _allMemberNameIdSet ?? (_allMemberNameIdSet = new IdentifierSet()); } }
        //end for root
        private bool? _isEffectiveOptional;
        internal override bool IsEffectiveOptional {
            get {
                if (_isEffectiveOptional == null) {
                    if (MinOccurs == 0) _isEffectiveOptional = true;
                    else {
                        if (Kind == ChildContainerKind.Choice) {
                            //_isEffectiveOptional = NonNullMemers.Count() == 0 || NonNullMemers.Any(i => i.IsEffectiveOptional);
                            var found = false;
                            var count = 0;
                            foreach (var member in NonNullMembers) {
                                if (member.IsEffectiveOptional) { found = true; break; }
                                count++;
                            }
                            _isEffectiveOptional = found || count == 0;
                        }
                        else _isEffectiveOptional = NonNullMembers.All(i => i.IsEffectiveOptional);//seq or unordered
                    }
                }
                return _isEffectiveOptional.Value;
            }
        }
        private bool? _hasElementBases;
        internal override bool HasElementBases {
            get {
                if (_hasElementBases == null) _hasElementBases = NonNullMembers.Any(i => i.HasElementBases);
                return _hasElementBases.Value;
            }
        }
        internal ChildInfo TryGetMember(Identifier memberNameId, out int order) {
            for (var i = 0; i < MemberList.Count; i++) {
                var member = MemberList[i];
                if (member != null && member.MemberNameId == memberNameId) {
                    order = i;
                    return member;
                }
            }
            order = -1;
            return null;
        }
        internal bool ContainsMemberNameId(Identifier memberNameId) {
            int order;
            return TryGetMember(memberNameId, out order) != null;
        }
        internal void AddMember(ChildInfo member, int order) {
            var count = MemberList.Count;
            if (order >= count) {
                for (; count <= order; count++)
                    MemberList.Add(null);
            }
            MemberList[order] = member;
        }
        internal void CheckUpa() { GetUpaTransitions(null); }
        internal override void GetUpaTransitions(HashSet<ElementBaseInfo> itemSet) {
            if (Kind == ChildContainerKind.Seq) {
                var thisItemSet = new HashSet<ElementBaseInfo>();
                if (itemSet != null) thisItemSet.AddRange(itemSet);
                foreach (var member in NonNullMembers) {
                    if (!member.CheckUpaTransitions(thisItemSet)) thisItemSet.Clear();
                    member.GetUpaTransitions(thisItemSet);
                }
                if (MaxOccurs > 1) {
                    foreach (var member in NonNullMembers) {
                        if (!member.CheckUpaTransitions(thisItemSet)) break;
                    }
                }
                if (itemSet != null) itemSet.AddRange(thisItemSet);
            }
            else if (Kind == ChildContainerKind.Choice) {
                var thisItemSet = new HashSet<ElementBaseInfo>();
                foreach (var member in NonNullMembers) member.CheckUpaChoiceSelectors(thisItemSet);
                foreach (var member in NonNullMembers) member.CheckUpaTransitions(itemSet);
                foreach (var member in NonNullMembers) {
                    thisItemSet.Clear();
                    member.GetUpaTransitions(thisItemSet);
                    itemSet.AddRange(thisItemSet);
                }
                if (MaxOccurs > 1) {
                    foreach (var member in NonNullMembers) member.CheckUpaTransitions(itemSet);
                }
            }
            else throw new InvalidOperationException();
        }
        internal override bool CheckUpaTransitions(IEnumerable<ElementBaseInfo> itemsToCheck) {
            if (Kind == ChildContainerKind.Seq) {
                foreach (var member in NonNullMembers) {
                    if (!member.CheckUpaTransitions(itemsToCheck)) return MinOccurs == 0;
                }
                return true;
            }
            else if (Kind == ChildContainerKind.Choice) {
                var count = 0;
                var ret = false;
                foreach (var member in NonNullMembers) {
                    if (member.CheckUpaTransitions(itemsToCheck)) ret = true;
                    count++;
                }
                return ret || count == 0 || MinOccurs == 0;
            }
            else throw new InvalidOperationException();
        }
        internal override void CheckUpaChoiceSelectors(HashSet<ElementBaseInfo> itemSet) {
            if (Kind == ChildContainerKind.Seq) {
                foreach (var member in NonNullMembers) {
                    member.CheckUpaChoiceSelectors(itemSet);
                    if (member.MinOccurs > 0) break;
                }
            }
            else if (Kind == ChildContainerKind.Choice) {
                foreach (var member in NonNullMembers)
                    member.CheckUpaChoiceSelectors(itemSet);
            }
            else throw new InvalidOperationException();
        }
        internal override ObjectInfo GenerateCS(ObjectInfo parent) {
            if ((parent = base.GenerateCS(parent)) == null) return null;
            foreach (var member in NonNullMembers) member.GenerateCS(this);
            var baseChildStruct = RestrictedChildStruct ?? BaseChildStruct;
            var hasBase = baseChildStruct != null;
            //public static readonly ChildStructInfo ThisInfo = new ChildStructInfo(clrType, memberName, isEffectiveOptional, kind, isMixed, isRoot, hasElementBases, members);
            AddCSMember(CS.Field(hasBase ? CS.NewPublicStaticReadOnlyTokenList : CS.PublicStaticReadOnlyTokenList, CSEX.ChildStructInfoName, "ThisInfo",
                CS.NewObjExpr(CSEX.ChildStructInfoName, SyntaxFactory.TypeOfExpression(CSFullName), CS.Literal(MemberPlainName), CS.Literal(IsEffectiveOptional),
                CSEX.Literal(Kind), CS.Literal(IsMixed), CS.Literal(IsRoot), CS.Literal(HasElementBases),
                CS.NewArrExpr(CSEX.ChildInfoArrayType, MemberList.Select(i => i == null ? CS.NullLiteral : i.ThisInfoExp)))));
            //public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
            AddCSMember(CSEX.ObjectInfoProperty());
            CreateAndAddCSClass(hasBase ? baseChildStruct.CSFullName : CSEX.ChildContainerName, parent);
            if (IsRoot) {
                //new public CLASS ComplexChild {
                //    get { return base.GenericComplexChild as CLASS; }
                //    set { base.GenericComplexChild = value; }
                //}
                parent.AddCSMember(CS.Property(CS.NewPublicTokenList, CSFullName, "ComplexChild", false,
                    default(SyntaxTokenList), new[] { SyntaxFactory.ReturnStatement(CS.AsExpr(CS.BaseMemberAccessExpr("GenericComplexChild"), CSFullName)) },
                    default(SyntaxTokenList), new[] { CS.AssignStm(CS.BaseMemberAccessExpr("GenericComplexChild"), CS.IdName("value")) }));
                //new public CLASS EnsureComplexChild() { return base.EnsureComplexChild<CLASS>(); }
                parent.AddCSMember(CS.Method(hasBase ? CS.NewPublicTokenList : CS.PublicTokenList, CSFullName, "EnsureComplexChild", null,
                   SyntaxFactory.ReturnStatement(CS.InvoExpr(CS.BaseMemberAccessExpr(CS.GenericName("EnsureComplexChild", CSFullName))))));
            }
            return parent;
        }
    }
    //
    public sealed class WildcardInfo : Info {
        internal WildcardInfo(IEnumerable<WildcardUriInfo> uris, WildcardValidation validation, SimpleToken keywordToken)
            : base(keywordToken) {
            if (uris != null) UriList.AddRange(uris);
            Validation = validation;
        }
        internal readonly List<WildcardUriInfo> UriList = new List<WildcardUriInfo>();
        internal readonly WildcardValidation Validation;
        private string _urisText;
        internal string UrisText { get { return _urisText ?? (_urisText = MX.WildcardInfo.GetUrisText(UriList)); } }
        private string _text;
        public override string ToString() { return _text ?? (_text = UrisText + " " + Validation.ToString()); }
        internal bool IsMatch(XNamespace value) {
            foreach (var uri in UriList)
                if (uri.IsMatch(value)) return true;
            return false;
        }
        private static bool IsEqualToOrRestrictedThan(WildcardUriInfo x, WildcardUriInfo y) {
            if (y.Kind == WildcardUriKind.Any) return true;
            if (x.Kind == WildcardUriKind.Any) return false;
            if (y.Kind == WildcardUriKind.Unqualified) return x.Kind == WildcardUriKind.Unqualified;
            if (x.Kind == WildcardUriKind.Unqualified) return false;
            if (y.Kind == WildcardUriKind.Other) {
                if (x.Kind == WildcardUriKind.Other) return x.Value == y.Value;
                return x.Value != y.Value;
            }
            if (x.Kind == WildcardUriKind.Other) return false;
            return x.Value == y.Value;//specific
        }
        private static bool IsEqualToOrRestrictedThan(IEnumerable<WildcardUriInfo> xs, IEnumerable<WildcardUriInfo> ys) {
            foreach (var x in xs) {
                var found = false;
                foreach (var y in ys)
                    if (IsEqualToOrRestrictedThan(x, y)) {
                        found = true;
                        break;
                    }
                if (!found) return false;
            }
            return true;
        }
        internal bool IsEqualToOrRestrictedThan(WildcardInfo other) {
            return IsEqualToOrRestrictedThan(UriList, other.UriList) && Validation >= other.Validation;
        }
        internal WildcardInfo TryIntersectWith(WildcardInfo other) {
            if (object.ReferenceEquals(this, other)) return this;
            if (IsEqualToOrRestrictedThan(UriList, other.UriList)) return new WildcardInfo(UriList, other.Validation, other.KeywordToken);
            if (IsEqualToOrRestrictedThan(other.UriList, UriList)) return other;
            return null;
        }
        internal bool IsIntersectWith(WildcardInfo other) {
            if (object.ReferenceEquals(this, other)) return true;
            if (IsEqualToOrRestrictedThan(UriList, other.UriList)) return true;
            if (IsEqualToOrRestrictedThan(other.UriList, UriList)) return true;
            return false;
        }
        internal WildcardInfo TryUniteWith(WildcardInfo other) {
            var uriSet = new HashSet<WildcardUriInfo>(UriList.Concat(other.UriList));
            var hasOther = false;
            var hasUnqualified = false;
            foreach (var uri in uriSet) {
                if (uri.Kind == WildcardUriKind.Other) hasOther = true;
                else if (uri.Kind == WildcardUriKind.Unqualified) hasUnqualified = true;
            }
            if (hasOther && hasUnqualified) return null;
            return new WildcardInfo(uriSet, other.Validation, other.KeywordToken);
        }
        private ExpressionSyntax _exp;
        internal ExpressionSyntax GetExp(ObjectInfo parent) {
            if (_exp != null) return _exp;
            _exp = CS.MemberAccessExpr(parent.ThisInfoExp, "Wildcard");
            //WildcardInfo(program, uris, validation)
            return CS.NewObjExpr(CSEX.WildcardInfoName, CSEX.XProgramInfoInstanceExp,
                CS.NewArrExpr(CSEX.WildcardUriInfoArrayType, UriList.Select(i => CSEX.Literal(i))), CSEX.Literal(Validation));
        }
    }
}