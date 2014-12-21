//
//Schema-lized Document Object Model(SDOM)
//DO NOT EDIT unless you know what you are doing
//Visit https://github.com/knat/Metah for more information
//
using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using SType = System.Type;
using STimeSpan = System.TimeSpan;
using SDateTime = System.DateTime;
using Metah.X.Extensions;

namespace Metah.X {
    public enum DiagnosticSeverity { Error = 0, Warning, Info }
    public enum DiagnosticCode {
        InvalidObjectClrType = 1,
        //simple type
        SimpleTypeValueRequired,
        InvalidSimpleTypeValueClrType,
        SimpleTypeValueNotMatchWithAnyUnionMemberTypes,
        CannotResolveFullNameValue,
        InvalidSimpleTypeLiteral,
        //facet
        LengthNotEqualTo,
        LengthNotGreaterThanOrEqualTo,
        LengthNotLessThanOrEqualTo,
        TotalDigitsNotLessThanOrEqualTo,
        FractionDigitsNotLessThanOrEqualTo,
        ValueNotGreaterThanOrEqualTo,
        ValueNotGreaterThan,
        ValueNotLessThanOrEqualTo,
        ValueNotLessThan,
        ValueNotInEnumerations,
        CanonicalStringNotMatchWithPattern,
        //complex type
        ComplexTypeDeclarationAbstract,
        CannotSetSimpleChildAndComplexChildBoth,
        AttributeSetNotAllowed,
        SimpleChildNotAllowed,
        SimpleChildRequired,
        ComplexChildNotAllowed,
        ComplexChildRequired,
        //attribute
        InvalidAttributeName,
        AttributeTypeRequired,
        AttributeValueNotEqualToFixedValue,
        RequiredAttributeNotFound,
        AttributeDeclarationNotFound,
        AttributeNamespaceUriNotMatchWithWildcard,
        //element
        ElementNameRequired,
        ElementTypeRequired,
        NullElementCannotHasChildren,
        ElementDeclarationNotFound,
        ExtensionInstanceProhibited,
        RestrictionInstanceProhibited,
        SubstitutionInstanceProhibited,
        ElememntDeclarationAbstract,
        ElementDeclarationNotNullable,
        TypeNameNotAllowedForSpecificElement,
        //ElementDeclarationHasFixedValueCannotBeNull,
        ElementNamespaceUriNotMatchWithWildcard,
        InvalidTypeName,
        TypeOfTypeNameNotEqualToOrDeriveFromDeclaredType,
        ComplexTypeRequired,
        ElementValueNotEqualToFixedValue,
        OnlyTextChildAllowedIfElementDeclarationHasFixedValue,
        //identity constraint
        DuplicateId,
        InvalidIdRef,
        ReferentialIdentityConstraintNotFound,
        IdentityConstraintValuePathExpressionCanReturnAtMostOneEntityObject,
        KeyIdentityConstraintValuePathExpressionMustReturnOneEntityObject,
        KeyIdentityConstraintEntityObjectMustHasNonNullValue,
        DuplicateKeyOrUniqueValue,
        InvalidKeyRefValue,
        //child container
        TextChildNotAllowed,
        ChildListCountNotGreaterThanOrEqualToMinOccurs,
        ChildListCountNotLessThanOrEqualToMaxOccurs,
        RequiredChildMemberNotFound,
        RedundantChildMember,
        ChoiceChildContainerEmpty,
        ChoiceChildContainerNotMatched,
        OnlyContentChildAllowed,
        ComplexChildNotMatched,
        //
        LoadFromXmlReaderException,
        //
        Extended = 1000
    }
    [Serializable]
    public class Diagnostic {
        public Diagnostic(object source, Location? location, DiagnosticSeverity severity, int rawCode, string message) {
            if (string.IsNullOrEmpty(message)) throw new ArgumentNullException("message");
            _source = source;
            if (location == null) {
                var obj = source as Object;
                if (obj != null) location = obj.Location;
                else {
                    var xmlException = source as XmlException;
                    if (xmlException != null) location = X.Location.From(xmlException);
                }
            }
            _location = location;
            _severity = severity;
            _rawCode = rawCode;
            _code = rawCode < (int)DiagnosticCode.Extended ? (DiagnosticCode)rawCode : DiagnosticCode.Extended;
            _message = message;
        }
        public Diagnostic(object source, Location? location, DiagnosticSeverity severity, int rawCode, Func<int, string> formatStringGetter, params object[] messageArgs)
            : this(source, location, severity, rawCode, formatStringGetter(rawCode).InvariantFormat(messageArgs)) { }
        public Diagnostic(Context context, object source, Location? location, DiagnosticSeverity severity, int rawCode, params object[] messageArgs)
            : this(source, location, severity, rawCode, context.DiagnosticMessageFormatStringGetter, messageArgs) {
            context.Diagnostics.Add(this);
        }
        public Diagnostic(Context context, object source, DiagnosticSeverity severity, DiagnosticCode code, params object[] messageArgs)
            : this(context, source, null, severity, (int)code, messageArgs) { }
        public Diagnostic(Context context, object source, DiagnosticCode code, params object[] messageArgs)
            : this(context, source, DiagnosticSeverity.Error, code, messageArgs) { }
        //
        private readonly object _source;
        public object Source { get { return _source; } }//opt
        public Object ObjectSource { get { return _source as Object; } }
        public IEntityObject EntityObjectSource { get { return _source as IEntityObject; } }
        public IReadOnlyList<IEntityObject> EntityObjectSources { get { return _source as IReadOnlyList<IEntityObject>; } }
        public Exception ExceptionSource { get { return _source as Exception; } }
        private readonly Location? _location;
        public Location? Location { get { return _location; } }//opt
        private readonly DiagnosticSeverity _severity;
        public DiagnosticSeverity Severity { get { return _severity; } }
        public bool IsError { get { return _severity == DiagnosticSeverity.Error; } }
        public bool IsWarning { get { return _severity == DiagnosticSeverity.Warning; } }
        public bool IsInfo { get { return _severity == DiagnosticSeverity.Info; } }
        private readonly int _rawCode;
        public int RawCode { get { return _rawCode; } }
        private readonly DiagnosticCode _code;
        public DiagnosticCode Code { get { return _code; } }
        private readonly string _message;
        public string Message { get { return _message; } }
        //
        private string _string;
        public override string ToString() {
            if (_string == null) {
                string str = null;
                if (_location != null) {
                    var location = _location.Value;
                    str = "{0}({1}, {2}): ".InvariantFormat(location.SourceUri, location.Line, location.Column);
                }
                str += "{0} {1}: {2}".InvariantFormat(_severity, _rawCode, _message);
                _string = str;
            }
            return _string;
        }
        //
        public static string GetMessageFormatString(int rawCode) {
            switch ((DiagnosticCode)rawCode) {
                case DiagnosticCode.InvalidObjectClrType: return "Invalid object clr type '{0}', expecting '{1}' or it's derived type";
                //simple type
                case DiagnosticCode.SimpleTypeValueRequired: return "Simple type value required";
                case DiagnosticCode.InvalidSimpleTypeValueClrType: return "Invalid simple type value clr type '{0}', expecting '{1}'. If it is Decimal or Int64 or ... Byte, make sure it's value is in the target type's value space";
                case DiagnosticCode.SimpleTypeValueNotMatchWithAnyUnionMemberTypes: return "Simple type value not match with any union member types";
                case DiagnosticCode.CannotResolveFullNameValue: return "Cannot resolve full name value '{0}'";
                case DiagnosticCode.InvalidSimpleTypeLiteral: return "Invalid simple type '{0}' literal '{1}'";
                //facet
                case DiagnosticCode.LengthNotEqualTo: return "Length '{0}' not equal to '{1}'";
                case DiagnosticCode.LengthNotGreaterThanOrEqualTo: return "Length '{0}' not greater than or equal to '{1}'";
                case DiagnosticCode.LengthNotLessThanOrEqualTo: return "Length '{0}' not less than or equal to '{1}'";
                case DiagnosticCode.TotalDigitsNotLessThanOrEqualTo: return "Total digits '{0}' not less than or equal to '{1}'";
                case DiagnosticCode.FractionDigitsNotLessThanOrEqualTo: return "Fraction digits '{0}' not less than or equal to '{1}'";
                case DiagnosticCode.ValueNotGreaterThanOrEqualTo: return "Value '{0}' not greater than or equal to '{1}'";
                case DiagnosticCode.ValueNotGreaterThan: return "Value '{0}' not greater than '{1}'";
                case DiagnosticCode.ValueNotLessThanOrEqualTo: return "Value '{0}' not less than or equal to '{1}'";
                case DiagnosticCode.ValueNotLessThan: return "Value '{0}' not less than '{1}'";
                case DiagnosticCode.ValueNotInEnumerations: return "Value '{0}' not in enumerations '{1}'";
                case DiagnosticCode.CanonicalStringNotMatchWithPattern: return "Canonical string '{0}' not match with pattern '{1}'";
                //complex type
                case DiagnosticCode.ComplexTypeDeclarationAbstract: return "Complex type declaration abstract";
                case DiagnosticCode.CannotSetSimpleChildAndComplexChildBoth: return "Cannot set simple child and complex child both";
                case DiagnosticCode.AttributeSetNotAllowed: return "Attribute set not allowed";
                case DiagnosticCode.SimpleChildNotAllowed: return "Simple child not allowed";
                case DiagnosticCode.SimpleChildRequired: return "Simple child required";
                case DiagnosticCode.ComplexChildNotAllowed: return "Complex child not allowed";
                case DiagnosticCode.ComplexChildRequired: return "Complex child required";
                //attribute
                case DiagnosticCode.InvalidAttributeName: return "Invalid attribute name '{0}', expecting '{1}'";
                case DiagnosticCode.AttributeTypeRequired: return "Attribute type required";
                case DiagnosticCode.AttributeValueNotEqualToFixedValue: return "Attribute value '{0}' not equal to fixed value '{1}'";
                case DiagnosticCode.RequiredAttributeNotFound: return "Required attribute '{0}' not found";
                case DiagnosticCode.AttributeDeclarationNotFound: return "Attribute '{0}' declaration not found";
                case DiagnosticCode.AttributeNamespaceUriNotMatchWithWildcard: return "Attribute namespace uri '{0}' not match with wildcard '{1}'";
                //element
                case DiagnosticCode.ElementNameRequired: return "Element name required";
                case DiagnosticCode.ElementTypeRequired: return "Element type required";
                case DiagnosticCode.NullElementCannotHasChildren: return "Null element cannot has children";
                case DiagnosticCode.ElementDeclarationNotFound: return "Element '{0}' declaration not found";
                case DiagnosticCode.ExtensionInstanceProhibited: return "Extension instance prohibited";
                case DiagnosticCode.RestrictionInstanceProhibited: return "Restriction instance prohibited";
                case DiagnosticCode.SubstitutionInstanceProhibited: return "Substitution instance prohibited";
                case DiagnosticCode.ElememntDeclarationAbstract: return "Elememnt declaration abstract";
                case DiagnosticCode.ElementDeclarationNotNullable: return "Element declaration not nullable";
                case DiagnosticCode.TypeNameNotAllowedForSpecificElement: return "Type name not allowed for specific element";
                //case DiagnosticCode.ElementDeclarationHasFixedValueCannotBeNull: return "Element declaration has fixed value cannot be null";
                case DiagnosticCode.ElementNamespaceUriNotMatchWithWildcard: return "Element namespace uri '{0}' not match with wildcard '{1}'";
                case DiagnosticCode.InvalidTypeName: return "Invalid type name '{0}'";
                case DiagnosticCode.TypeOfTypeNameNotEqualToOrDeriveFromDeclaredType: return "Type of type name not equal to or derive from declared type";
                case DiagnosticCode.ComplexTypeRequired: return "Complex type required";
                case DiagnosticCode.ElementValueNotEqualToFixedValue: return "Element value '{0}' not equal to fixed value '{1}'";
                case DiagnosticCode.OnlyTextChildAllowedIfElementDeclarationHasFixedValue: return "Only text child allowed if element declaration has fixed value";
                //identity constraint
                case DiagnosticCode.DuplicateId: return "Duplicate id '{0}'";
                case DiagnosticCode.InvalidIdRef: return "Invalid id ref '{0}'";
                case DiagnosticCode.ReferentialIdentityConstraintNotFound: return "Referential identity constraint '{0}' not found";
                case DiagnosticCode.IdentityConstraintValuePathExpressionCanReturnAtMostOneEntityObject: return "Identity constraint value path expression can return at most one entity object";
                case DiagnosticCode.KeyIdentityConstraintValuePathExpressionMustReturnOneEntityObject: return "Key identity constraint value path expression must return one entity object";
                case DiagnosticCode.KeyIdentityConstraintEntityObjectMustHasNonNullValue: return "Key identity constraint entity object must has non null value";
                case DiagnosticCode.DuplicateKeyOrUniqueValue: return "Duplicate key or unique value '{0}'";
                case DiagnosticCode.InvalidKeyRefValue: return "Invalid key ref value '{0}'";
                //child container
                case DiagnosticCode.TextChildNotAllowed: return "Text child not allowed";
                case DiagnosticCode.ChildListCountNotGreaterThanOrEqualToMinOccurs: return "Child list count '{0}' not greater than or equal to min occurs '{1}'";
                case DiagnosticCode.ChildListCountNotLessThanOrEqualToMaxOccurs: return "Child list count '{0}' not less than or equal to max occurs '{1}'";
                case DiagnosticCode.RequiredChildMemberNotFound: return "Required child member with member name '{0}' not found";
                case DiagnosticCode.RedundantChildMember: return "Redundant child {0}";
                case DiagnosticCode.ChoiceChildContainerEmpty: return "Choice child container empty";
                case DiagnosticCode.ChoiceChildContainerNotMatched: return "Choice child container not matched";
                case DiagnosticCode.OnlyContentChildAllowed: return "Only content child(element or text) allowed";
                case DiagnosticCode.ComplexChildNotMatched: return "Complex child not matched";
                //
                case DiagnosticCode.LoadFromXmlReaderException: return "Load from XmlReader exception: {0}";
                default: throw new ArgumentException("Invalid code '{0}'".InvariantFormat(rawCode));
            }
        }
        public static readonly Func<int, string> MessageFormatStringGetter = GetMessageFormatString;
    }
    [Serializable]
    public struct Location {
        public Location(string sourceUri, int line, int column) {
            _sourceUri = sourceUri;
            _line = line;
            _column = column;
        }
        private readonly string _sourceUri;
        public string SourceUri { get { return _sourceUri; } }
        private readonly int _line;
        public int Line { get { return _line; } }//1-based
        private readonly int _column;
        public int Column { get { return _column; } }//1-based
        //
        public static Location From(XmlException xmlException) {
            if (xmlException == null) throw new ArgumentNullException("xmlException");
            return new Location(xmlException.SourceUri, xmlException.LineNumber, xmlException.LinePosition);
        }
    }
    [Serializable]
    public class Context {
        public Context() { }
        //
        private List<Diagnostic> _diagnostics;
        public List<Diagnostic> Diagnostics { get { return _diagnostics ?? (_diagnostics = new List<Diagnostic>()); } }
        public bool HasDiagnostics { get { return _diagnostics != null && _diagnostics.Count > 0; } }
        public bool HasErrorDiagnostics { get { return HasErrorDiagnosticsCore(0); } }
        private bool HasErrorDiagnosticsCore(int startIndex) {
            if (_diagnostics != null)
                for (var i = startIndex; i < _diagnostics.Count; i++)
                    if (_diagnostics[i].IsError) return true;
            return false;
        }
        public struct DiagnosticsMarker {
            internal DiagnosticsMarker(Context context) {
                Context = context;
                DiagnosticsIndex = context._diagnostics == null ? 0 : context._diagnostics.Count;
            }
            public readonly Context Context;
            public readonly int DiagnosticsIndex;
            public bool HasErrors { get { return Context.HasErrorDiagnosticsCore(DiagnosticsIndex); } }
            public void Restore() {
                var diagnostics = Context._diagnostics;
                if (diagnostics != null)
                    diagnostics.RemoveRange(DiagnosticsIndex, diagnostics.Count - DiagnosticsIndex);
            }
        }
        public DiagnosticsMarker MarkDiagnostics() { return new DiagnosticsMarker(this); }
        //
        private Func<int, string> _diagnosticMessageFormatStringGetter;
        public Func<int, string> DiagnosticMessageFormatStringGetter {
            get { return _diagnosticMessageFormatStringGetter ?? Diagnostic.MessageFormatStringGetter; }
            set { _diagnosticMessageFormatStringGetter = value; }
        }
        public virtual void Reset() {
            if (_diagnostics != null) _diagnostics.Clear();
            if (_idDict != null) _idDict.Clear();
            if (_idRefList != null) _idRefList.Clear();
        }
        //
        private Dictionary<object, Id> _idDict;//key: Id.Value
        private Dictionary<object, Id> IdDict { get { return _idDict ?? (_idDict = new Dictionary<object, Id>(SimpleType.ValueEqualityComparer)); } }
        internal bool TryAddId(Id id) {
            if (id == null) throw new ArgumentNullException("id");
            var idValue = id.Value;
            if (IdDict.ContainsKey(idValue)) {
                new Diagnostic(this, id, DiagnosticCode.DuplicateId, idValue);
                return false;
            }
            IdDict.Add(idValue, id);
            return true;
        }
        private List<IIdRefObject> _idRefList;
        internal List<IIdRefObject> IdRefList { get { return _idRefList ?? (_idRefList = new List<IIdRefObject>()); } }
        public bool TryGetIdDictAndIdRefList(out Dictionary<object, Id> resultIdDict, out List<IIdRefObject> resultIdRefList) {
            resultIdDict = null;
            resultIdRefList = null;
            if (_idRefList != null && _idRefList.Count > 0) {
                var dMarker = MarkDiagnostics();
                foreach (var iidRef in _idRefList) {
                    var idRef = iidRef as IdRef;
                    if (idRef != null) {
                        idRef.Referential = TryResolveIdRef(idRef.Value, idRef);
                    }
                    else {
                        var idRefs = (IdRefs)iidRef;
                        idRefs.ReferentialIdList.Clear();
                        foreach (var idRefValue in idRefs)
                            idRefs.ReferentialIdList.Add(TryResolveIdRef(idRefValue, idRefs));
                    }
                }
                if (dMarker.HasErrors) return false;
            }
            resultIdDict = _idDict;
            resultIdRefList = _idRefList;
            _idDict = null;
            _idRefList = null;
            return true;
        }
        private Id TryResolveIdRef(string idRefValue, Object idRef) {
            Id id;
            if (IdDict.TryGetValue(idRefValue, out id)) return id;
            new Diagnostic(this, idRef, DiagnosticCode.InvalidIdRef, idRefValue);
            return null;
        }
    }
    public interface IIdRefObject { }//IdRef and IdRefs impls this interface
    public interface IEntityObject {//Attribute and Element impls this interface
        XName Name { get; }
        Type Type { get; }
        object Value { get; }
    }

    [Serializable]
    public abstract class Object : IXmlLineInfo {
        protected Object() { }
        private Object _parent;
        public Object Parent { get { return _parent; } }
        public bool HasParent { get { return _parent != null; } }
        private Object SetParent(Object parent) {
            if (parent == null) throw new ArgumentNullException("parent");
            for (var i = parent; i != null; i = i._parent)
                if (object.ReferenceEquals(this, i)) throw new InvalidOperationException("Circular reference detected");
            Object obj;
            if (_parent == null) obj = this;
            else obj = DeepClone();
            obj._parent = parent;
            return obj;
        }
        protected T SetParentTo<T>(T obj, bool allowNull = true) where T : Object {
            if (obj == null) {
                if (!allowNull) throw new ArgumentNullException("obj");
                return null;
            }
            return (T)obj.SetParent(this);
        }
        public T GetAncestor<T>(bool @try = true, bool testSelf = false) where T : class {
            for (var obj = testSelf ? this : _parent; obj != null; obj = obj._parent) {
                var res = obj as T;
                if (res != null) return res;
            }
            if (!@try) throw new InvalidOperationException("Cannot get ancestor of type: " + typeof(T).FullName);
            return null;
        }
        private Location? _location;
        public virtual Location? Location { get { return _location; } set { _location = value; } }
        bool IXmlLineInfo.HasLineInfo() { return Location != null; }
        int IXmlLineInfo.LineNumber { get { return Location.Line(); } }
        int IXmlLineInfo.LinePosition { get { return Location.Column(); } }
        //
        public virtual Object DeepClone() {
            var obj = (Object)MemberwiseClone();
            obj._parent = null;
            return obj;
        }
        public T DeepClone<T>() where T : Object { return (T)DeepClone(); }
        public virtual ObjectInfo ObjectInfo { get { return null; } }
        public bool IsSpecific { get { return ObjectInfo != null; } }
        public bool TryValidate(Context context) {
            if (context == null) throw new ArgumentNullException("context");
            var success = TryValidating(context, true);
            if (success) success = TryValidateCore(context);
            return TryValidated(context, success);
        }
        protected virtual bool TryValidating(Context context, bool fromValidate) { return true; }
        protected virtual bool TryValidateCore(Context context) { return true; }
        protected virtual bool TryValidated(Context context, bool success) { return success; }
        internal bool InvokeTryValidatePair(Context context) { return TryValidated(context, TryValidating(context, false)); }
    }
    [Serializable]
    public abstract class Type : Object {
        protected Type() { }
        public TypeInfo TypeInfo { get { return (TypeInfo)ObjectInfo; } }
        public static readonly TypeInfo ThisInfo = new TypeInfo(typeof(Type), TypeKind.Type, TypeKind.Type.ToSystemName(), null);
    }
    [Serializable]
    public class SimpleType : Type, IEquatable<SimpleType> {
        public SimpleType() { }
        public SimpleType(object value, bool direct = false) { SetValue(value, direct); }
        public Attribute AttributeParent { get { return base.Parent as Attribute; } }
        public Element ElementParent { get { return base.Parent as Element; } }
        public IEntityObject EntityAncestor { get { return GetAncestor<IEntityObject>(); } }
        public Element ElementAncestor { get { return GetAncestor<Element>(); } }
        private object _value;
        public object Value { get { return _value; } set { SetValue(value); } }
        public object GenericValue { get { return _value; } set { SetValue(value); } }
        public object SetValue(object value, bool direct = false) {
            if (!direct) value = CloneValue(value);
            _value = value;
            return value;
        }
        public bool HasValue { get { return _value != null; } }
        public bool IsValueNullOrEmptyString {
            get {
                if (_value == null) return true;
                return (_value as string) == "";
            }
        }
        public bool IsValueNullOrWhitespaceString {
            get {
                if (_value == null) return true;
                var s = _value as string;
                if (s == null) return false;
                return TrimWhitespaces(s).Length == 0;
            }
        }
        public bool TryGetTypedValue<T>(out T result) where T : class { return TryGetTypedValue(_value, out result); }
        public bool TryGetTypedValue<T>(out T? result) where T : struct { return TryGetTypedValue(_value, out result); }
        public bool TryGetTypedValue(out decimal? result) { return TryGetTypedValue(_value, out result); }
        public bool TryGetTypedValue(out long? result) { return TryGetTypedValue(_value, out result); }
        public bool TryGetTypedValue(out ulong? result) { return TryGetTypedValue(_value, out result); }
        public bool TryGetTypedValue(out int? result) { return TryGetTypedValue(_value, out result); }
        public bool TryGetTypedValue(out uint? result) { return TryGetTypedValue(_value, out result); }
        public bool TryGetTypedValue(out short? result) { return TryGetTypedValue(_value, out result); }
        public bool TryGetTypedValue(out ushort? result) { return TryGetTypedValue(_value, out result); }
        public bool TryGetTypedValue(out sbyte? result) { return TryGetTypedValue(_value, out result); }
        public bool TryGetTypedValue(out byte? result) { return TryGetTypedValue(_value, out result); }
        public override Object DeepClone() {
            var obj = (SimpleType)base.DeepClone();
            obj.Value = _value;
            return obj;
        }
        public bool Equals(SimpleType other) {
            if (object.ReferenceEquals(this, other)) return true;
            if (object.ReferenceEquals(other, null)) return false;
            var simpleTypeInfo = SimpleTypeInfo;
            var otherSimpleTypeInfo = other.SimpleTypeInfo;
            if (simpleTypeInfo == null) {
                if (otherSimpleTypeInfo != null) return false;
                return ValueEquals(_value, other._value);
            }
            if (otherSimpleTypeInfo == null) return false;
            return simpleTypeInfo.Kind == otherSimpleTypeInfo.Kind && ValueEquals(_value, other._value);
        }
        public override sealed bool Equals(object obj) { return Equals(obj as SimpleType); }
        public override int GetHashCode() { return GetValueHashCode(_value); }
        public static bool operator ==(SimpleType left, SimpleType right) {
            if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
            return left.Equals(right);
        }
        public static bool operator !=(SimpleType left, SimpleType right) { return !(left == right); }
        public override string ToString() { return ToString(_value, SimpleTypeInfo); }
        public static object CloneValue(object value) {
            var cloneable = value as ICloneable;
            if (cloneable != null) return cloneable.Clone();
            return value;
        }
        public static bool ValueEquals(object x, object y) {
            if (object.Equals(x, y)) return true;
            var bx = x as byte[];//IStructuralEquatable?
            if (bx != null) {
                var by = y as byte[];
                if (by == null) return false;
                if (bx.Length != by.Length) return false;
                for (var i = 0; i < bx.Length; i++)
                    if (bx[i] != by[i]) return false;
                return true;
            }
            return false;
        }
        public static int GetValueHashCode(object value) {
            if (value == null) return 0;
            var bytes = value as byte[];
            if (bytes != null) {
                var hash = 17;
                var length = Math.Min(bytes.Length, 13);
                for (var i = 0; i < length; i++)
                    hash = ExtensionMethods.AggregateHash(hash, bytes[i]);
                return hash;
            }
            return value.GetHashCode();
        }
        public static bool ValuesEquals(IReadOnlyList<object> x, IReadOnlyList<object> y) {
            if (x == y) return true;
            if (x == null) return y == null;
            if (y == null) return false;
            var xCount = x.Count;
            if (xCount != y.Count) return false;
            for (var i = 0; i < xCount; i++)
                if (!ValueEquals(x[i], y[i])) return false;
            return true;
        }
        public static int GetValuesHashCode(IReadOnlyList<object> values) {
            if (values == null) return 0;
            var hash = 17;
            var count = Math.Min(values.Count, 7);
            for (var i = 0; i < count; i++)
                hash = ExtensionMethods.AggregateHash(hash, GetValueHashCode(values[i]));
            return hash;
        }
        public static IEqualityComparer<object> ValueEqualityComparer { get { return ValueEqualityComparerClass.Instance; } }
        private sealed class ValueEqualityComparerClass : IEqualityComparer<object> {
            private ValueEqualityComparerClass() { }
            internal static readonly IEqualityComparer<object> Instance = new ValueEqualityComparerClass();
            bool IEqualityComparer<object>.Equals(object x, object y) { return ValueEquals(x, y); }
            int IEqualityComparer<object>.GetHashCode(object obj) { return GetValueHashCode(obj); }
        }
        public static IEqualityComparer<IReadOnlyList<object>> ValuesEqualityComparer { get { return ValuesEqualityComparerClass.Instance; } }
        private sealed class ValuesEqualityComparerClass : IEqualityComparer<IReadOnlyList<object>> {
            private ValuesEqualityComparerClass() { }
            internal static readonly IEqualityComparer<IReadOnlyList<object>> Instance = new ValuesEqualityComparerClass();
            bool IEqualityComparer<IReadOnlyList<object>>.Equals(IReadOnlyList<object> x, IReadOnlyList<object> y) { return ValuesEquals(x, y); }
            int IEqualityComparer<IReadOnlyList<object>>.GetHashCode(IReadOnlyList<object> values) { return GetValuesHashCode(values); }
        }
        public static int CompareValue(object x, object y) {
            var comparable = x as IComparable;
            if (comparable == null) throw new ArgumentException("x not comparable");
            return comparable.CompareTo(y);
        }
        public static bool TryGetTypedValue<T>(object value, out T result) where T : class {
            result = value as T;
            return result != null;
        }
        public static bool TryGetTypedValue<T>(object value, out T? result) where T : struct {
            result = value as T?;
            return result != null;
        }
        public static bool TryGetTypedValue(object value, out decimal? result) {
            result = value as decimal?;
            if (result == null && value != null) {
                switch (SType.GetTypeCode(value.GetType())) {
                    case TypeCode.Int64: result = (long)value; break;
                    case TypeCode.Int32: result = (int)value; break;
                    case TypeCode.Int16: result = (short)value; break;
                    case TypeCode.SByte: result = (sbyte)value; break;
                    case TypeCode.UInt64: result = (ulong)value; break;
                    case TypeCode.UInt32: result = (uint)value; break;
                    case TypeCode.UInt16: result = (ushort)value; break;
                    case TypeCode.Byte: result = (byte)value; break;
                }
            }
            return result != null;
        }
        public static bool TryGetTypedValue(object value, out long? result) {
            result = value as long?;
            if (result == null && value != null) {
                switch (SType.GetTypeCode(value.GetType())) {
                    case TypeCode.Decimal: {
                            var decimalValue = (decimal)value;
                            if (decimalValue >= long.MinValue && decimalValue <= long.MaxValue) result = (long)decimalValue;
                        }
                        break;
                    case TypeCode.Int32: result = (int)value; break;
                    case TypeCode.Int16: result = (short)value; break;
                    case TypeCode.SByte: result = (sbyte)value; break;
                    case TypeCode.UInt64: {
                            var ulongValue = (ulong)value;
                            if (ulongValue <= long.MaxValue) result = (long)ulongValue;
                        }
                        break;
                    case TypeCode.UInt32: result = (uint)value; break;
                    case TypeCode.UInt16: result = (ushort)value; break;
                    case TypeCode.Byte: result = (byte)value; break;
                }
            }
            return result != null;
        }
        public static bool TryGetTypedValue(object value, out ulong? result) {
            result = value as ulong?;
            if (result == null && value != null) {
                switch (SType.GetTypeCode(value.GetType())) {
                    case TypeCode.Decimal: {
                            var decimalValue = (decimal)value;
                            if (decimalValue >= 0 && decimalValue <= ulong.MaxValue) result = (ulong)decimalValue;
                        }
                        break;
                    case TypeCode.Int64: {
                            var longValue = (long)value;
                            if (longValue >= 0) result = (ulong)longValue;
                        }
                        break;
                    case TypeCode.Int32: {
                            var intValue = (int)value;
                            if (intValue >= 0) result = (ulong)intValue;
                        }
                        break;
                    case TypeCode.Int16: {
                            var shortValue = (short)value;
                            if (shortValue >= 0) result = (ulong)shortValue;
                        }
                        break;
                    case TypeCode.SByte: {
                            var sbyteValue = (sbyte)value;
                            if (sbyteValue >= 0) result = (ulong)sbyteValue;
                        }
                        break;
                    case TypeCode.UInt32: result = (uint)value; break;
                    case TypeCode.UInt16: result = (ushort)value; break;
                    case TypeCode.Byte: result = (byte)value; break;
                }
            }
            return result != null;
        }
        public static bool TryGetTypedValue(object value, out int? result) {
            result = value as int?;
            if (result == null && value != null) {
                switch (SType.GetTypeCode(value.GetType())) {
                    case TypeCode.Decimal: {
                            var decimalValue = (decimal)value;
                            if (decimalValue >= int.MinValue && decimalValue <= int.MaxValue) result = (int)decimalValue;
                        }
                        break;
                    case TypeCode.Int64: {
                            var longValue = (long)value;
                            if (longValue >= int.MinValue && longValue <= int.MaxValue) result = (int)longValue;
                        }
                        break;
                    case TypeCode.Int16: result = (short)value; break;
                    case TypeCode.SByte: result = (sbyte)value; break;
                    case TypeCode.UInt64: {
                            var ulongValue = (ulong)value;
                            if (ulongValue <= int.MaxValue) result = (int)ulongValue;
                        }
                        break;
                    case TypeCode.UInt32: {
                            var uintValue = (uint)value;
                            if (uintValue <= int.MaxValue) result = (int)uintValue;
                        }
                        break;
                    case TypeCode.UInt16: result = (ushort)value; break;
                    case TypeCode.Byte: result = (byte)value; break;
                }
            }
            return result != null;
        }
        public static bool TryGetTypedValue(object value, out uint? result) {
            result = value as uint?;
            if (result == null && value != null) {
                switch (SType.GetTypeCode(value.GetType())) {
                    case TypeCode.Decimal: {
                            var decimalValue = (decimal)value;
                            if (decimalValue >= 0 && decimalValue <= uint.MaxValue) result = (uint)decimalValue;
                        }
                        break;
                    case TypeCode.Int64: {
                            var longValue = (long)value;
                            if (longValue >= 0 && longValue <= uint.MaxValue) result = (uint)longValue;
                        }
                        break;
                    case TypeCode.Int32: {
                            var intValue = (int)value;
                            if (intValue >= 0) result = (uint)intValue;
                        }
                        break;
                    case TypeCode.Int16: {
                            var shortValue = (short)value;
                            if (shortValue >= 0) result = (uint)shortValue;
                        }
                        break;
                    case TypeCode.SByte: {
                            var sbyteValue = (sbyte)value;
                            if (sbyteValue >= 0) result = (uint)sbyteValue;
                        }
                        break;
                    case TypeCode.UInt64: {
                            var ulongValue = (ulong)value;
                            if (ulongValue <= uint.MaxValue) result = (uint)ulongValue;
                        }
                        break;
                    case TypeCode.UInt16: result = (ushort)value; break;
                    case TypeCode.Byte: result = (byte)value; break;
                }
            }
            return result != null;
        }
        public static bool TryGetTypedValue(object value, out short? result) {
            result = value as short?;
            if (result == null && value != null) {
                switch (SType.GetTypeCode(value.GetType())) {
                    case TypeCode.Decimal: {
                            var decimalValue = (decimal)value;
                            if (decimalValue >= short.MinValue && decimalValue <= short.MaxValue) result = (short)decimalValue;
                        }
                        break;
                    case TypeCode.Int64: {
                            var longValue = (long)value;
                            if (longValue >= short.MinValue && longValue <= short.MaxValue) result = (short)longValue;
                        }
                        break;
                    case TypeCode.Int32: {
                            var intValue = (int)value;
                            if (intValue >= short.MinValue && intValue <= short.MaxValue) result = (short)intValue;
                        }
                        break;
                    case TypeCode.SByte: result = (sbyte)value; break;
                    case TypeCode.UInt64: {
                            var ulongValue = (ulong)value;
                            if (ulongValue <= (ulong)short.MaxValue) result = (short)ulongValue;
                        }
                        break;
                    case TypeCode.UInt32: {
                            var uintValue = (uint)value;
                            if (uintValue <= short.MaxValue) result = (short)uintValue;
                        }
                        break;
                    case TypeCode.UInt16: {
                            var ushortValue = (ushort)value;
                            if (ushortValue <= short.MaxValue) result = (short)ushortValue;
                        }
                        break;
                    case TypeCode.Byte: result = (byte)value; break;
                }
            }
            return result != null;
        }
        public static bool TryGetTypedValue(object value, out ushort? result) {
            result = value as ushort?;
            if (result == null && value != null) {
                switch (SType.GetTypeCode(value.GetType())) {
                    case TypeCode.Decimal: {
                            var decimalValue = (decimal)value;
                            if (decimalValue >= 0 && decimalValue <= ushort.MaxValue) result = (ushort)decimalValue;
                        }
                        break;
                    case TypeCode.Int64: {
                            var longValue = (long)value;
                            if (longValue >= 0 && longValue <= ushort.MaxValue) result = (ushort)longValue;
                        }
                        break;
                    case TypeCode.Int32: {
                            var intValue = (int)value;
                            if (intValue >= 0 && intValue <= ushort.MaxValue) result = (ushort)intValue;
                        }
                        break;
                    case TypeCode.Int16: {
                            var shortValue = (short)value;
                            if (shortValue >= 0) result = (ushort)shortValue;
                        }
                        break;
                    case TypeCode.SByte: {
                            var sbyteValue = (sbyte)value;
                            if (sbyteValue >= 0) result = (ushort)sbyteValue;
                        }
                        break;
                    case TypeCode.UInt64: {
                            var ulongValue = (ulong)value;
                            if (ulongValue <= ushort.MaxValue) result = (ushort)ulongValue;
                        }
                        break;
                    case TypeCode.UInt32: {
                            var uintValue = (uint)value;
                            if (uintValue <= ushort.MaxValue) result = (ushort)uintValue;
                        }
                        break;
                    case TypeCode.Byte: result = (byte)value; break;
                }
            }
            return result != null;
        }
        public static bool TryGetTypedValue(object value, out sbyte? result) {
            result = value as sbyte?;
            if (result == null && value != null) {
                switch (SType.GetTypeCode(value.GetType())) {
                    case TypeCode.Decimal: {
                            var decimalValue = (decimal)value;
                            if (decimalValue >= sbyte.MinValue && decimalValue <= sbyte.MaxValue) result = (sbyte)decimalValue;
                        }
                        break;
                    case TypeCode.Int64: {
                            var longValue = (long)value;
                            if (longValue >= sbyte.MinValue && longValue <= sbyte.MaxValue) result = (sbyte)longValue;
                        }
                        break;
                    case TypeCode.Int32: {
                            var intValue = (int)value;
                            if (intValue >= sbyte.MinValue && intValue <= sbyte.MaxValue) result = (sbyte)intValue;
                        }
                        break;
                    case TypeCode.Int16: {
                            var shortValue = (short)value;
                            if (shortValue >= sbyte.MinValue && shortValue <= sbyte.MaxValue) result = (sbyte)shortValue;
                        }
                        break;
                    case TypeCode.UInt64: {
                            var ulongValue = (ulong)value;
                            if (ulongValue <= (ulong)sbyte.MaxValue) result = (sbyte)ulongValue;
                        }
                        break;
                    case TypeCode.UInt32: {
                            var uintValue = (uint)value;
                            if (uintValue <= sbyte.MaxValue) result = (sbyte)uintValue;
                        }
                        break;
                    case TypeCode.UInt16: {
                            var ushortValue = (ushort)value;
                            if (ushortValue <= sbyte.MaxValue) result = (sbyte)ushortValue;
                        }
                        break;
                    case TypeCode.Byte: {
                            var byteValue = (byte)value;
                            if (byteValue <= sbyte.MaxValue) result = (sbyte)byteValue;
                        }
                        break;
                }
            }
            return result != null;
        }
        public static bool TryGetTypedValue(object value, out byte? result) {
            result = value as byte?;
            if (result == null && value != null) {
                switch (SType.GetTypeCode(value.GetType())) {
                    case TypeCode.Decimal: {
                            var decimalValue = (decimal)value;
                            if (decimalValue >= 0 && decimalValue <= byte.MaxValue) result = (byte)decimalValue;
                        }
                        break;
                    case TypeCode.Int64: {
                            var longValue = (long)value;
                            if (longValue >= 0 && longValue <= byte.MaxValue) result = (byte)longValue;
                        }
                        break;
                    case TypeCode.Int32: {
                            var intValue = (int)value;
                            if (intValue >= 0 && intValue <= byte.MaxValue) result = (byte)intValue;
                        }
                        break;
                    case TypeCode.Int16: {
                            var shortValue = (short)value;
                            if (shortValue >= 0 && shortValue <= byte.MaxValue) result = (byte)shortValue;
                        }
                        break;
                    case TypeCode.SByte: {
                            var sbyteValue = (sbyte)value;
                            if (sbyteValue >= 0) result = (byte)sbyteValue;
                        }
                        break;
                    case TypeCode.UInt64: {
                            var ulongValue = (ulong)value;
                            if (ulongValue <= byte.MaxValue) result = (byte)ulongValue;
                        }
                        break;
                    case TypeCode.UInt32: {
                            var uintValue = (uint)value;
                            if (uintValue <= byte.MaxValue) result = (byte)uintValue;
                        }
                        break;
                    case TypeCode.UInt16: {
                            var ushortValue = (ushort)value;
                            if (ushortValue <= byte.MaxValue) result = (byte)ushortValue;
                        }
                        break;
                }
            }
            return result != null;
        }
        public SimpleTypeInfo SimpleTypeInfo { get { return (SimpleTypeInfo)ObjectInfo; } }
        new public static readonly SimpleTypeInfo ThisInfo = new SimpleTypeInfo(typeof(SimpleType), TypeKind.SimpleType, TypeKind.SimpleType.ToSystemName(), Type.ThisInfo, typeof(object), null);
        protected override sealed bool TryValidateCore(Context context) {
            if (!base.TryValidateCore(context)) return false;
            return TryValidateValue(ref _value, SimpleTypeInfo, context, this);
        }
        public bool TrySpecialize<T>(SimpleTypeInfo simpleTypeInfo, Context context, out T result) where T : SimpleType {
            if (simpleTypeInfo == null) throw new ArgumentNullException("simpleTypeInfo");
            if (context == null) throw new ArgumentNullException("context");
            result = null;
            object value;
            var direct = false;
            var literal = _value as string;
            if (literal != null) {
                if (!TryParseValue(literal, simpleTypeInfo, context, out value, this)) return false;
                direct = true;
            }
            else value = _value;
            if (!TryValidateValue(ref value, simpleTypeInfo, context, this)) return false;
            var obj = simpleTypeInfo.CreateInstance<T>(Location);
            obj.SetValue(value, direct);
            if (!obj.InvokeTryValidatePair(context)) return false;
            result = obj;
            return true;
        }
        private static readonly Dictionary<SType, TypeKind> _valueClrTypeDefaultDict = new Dictionary<SType, TypeKind> {
            {typeof(string), TypeKind.String},
            {typeof(bool), TypeKind.Boolean},
            {typeof(decimal), TypeKind.Decimal},
            {typeof(long), TypeKind.Int64},
            {typeof(int), TypeKind.Int32},
            {typeof(short), TypeKind.Int16},
            {typeof(sbyte), TypeKind.SByte},
            {typeof(ulong), TypeKind.UInt64},
            {typeof(uint), TypeKind.UInt32},
            {typeof(ushort), TypeKind.UInt16},
            {typeof(byte), TypeKind.Byte},
            {typeof(double), TypeKind.Double},
            {typeof(float), TypeKind.Single},
            {typeof(byte[]), TypeKind.Base64Binary},
            {typeof(STimeSpan), TypeKind.TimeSpan},
            {typeof(SDateTime), TypeKind.DateTime},
            {typeof(XNamespace), TypeKind.Uri},
            {typeof(FullNameValue), TypeKind.FullName},
            {typeof(ListedSimpleTypeValue), TypeKind.ListedSimpleType},
            {typeof(UnitedSimpleTypeValue), TypeKind.UnitedSimpleType},
        };
        private const string _valueClrTypeNames = "System.String, System.Boolean, System.Decimal, System.Int64, System.Int32, System.Int16, System.SByte, System.UInt64, System.UInt32, System.UInt16, System.Byte, System.Double, System.Single, System.Byte[], System.TimeSpan, System.DateTime, System.Xml.Linq.XNamespace, Metah.X.FullNameValue, Metah.X.ListedSimpleTypeValue, Metah.X.UnitedSimpleTypeValue";
        public static bool TryValidateValue(ref object value, ISimpleTypeInfo simpleTypeInfo, Context context, SimpleType simpleType = null, DateTimeStyles dts = DateTimeStyles.None) {
            if (context == null) throw new ArgumentNullException("context");
            if (value == null) {
                new Diagnostic(context, simpleType, DiagnosticCode.SimpleTypeValueRequired);
                return false;
            }
            var typeKind = TypeKind.SimpleType;
            if (simpleTypeInfo != null) {
                typeKind = simpleTypeInfo.Kind;
                if (typeKind == TypeKind.SimpleType) simpleTypeInfo = null;
                else {
                    var ok = false;
                    var targetType = simpleTypeInfo.ValueClrType;
                    if (targetType.IsAssignableFrom(value.GetType())) ok = true;
                    else {
                        switch (SType.GetTypeCode(targetType)) {
                            case TypeCode.Decimal: {
                                    decimal? r;
                                    if (TryGetTypedValue(value, out r)) {
                                        value = r;
                                        ok = true;
                                    }
                                }
                                break;
                            case TypeCode.Int64: {
                                    long? r;
                                    if (TryGetTypedValue(value, out r)) {
                                        value = r;
                                        ok = true;
                                    }
                                }
                                break;
                            case TypeCode.Int32: {
                                    int? r;
                                    if (TryGetTypedValue(value, out r)) {
                                        value = r;
                                        ok = true;
                                    }
                                }
                                break;
                            case TypeCode.Int16: {
                                    short? r;
                                    if (TryGetTypedValue(value, out r)) {
                                        value = r;
                                        ok = true;
                                    }
                                }
                                break;
                            case TypeCode.SByte: {
                                    sbyte? r;
                                    if (TryGetTypedValue(value, out r)) {
                                        value = r;
                                        ok = true;
                                    }
                                }
                                break;
                            case TypeCode.UInt64: {
                                    ulong? r;
                                    if (TryGetTypedValue(value, out r)) {
                                        value = r;
                                        ok = true;
                                    }
                                }
                                break;
                            case TypeCode.UInt32: {
                                    uint? r;
                                    if (TryGetTypedValue(value, out r)) {
                                        value = r;
                                        ok = true;
                                    }
                                }
                                break;
                            case TypeCode.UInt16: {
                                    ushort? r;
                                    if (TryGetTypedValue(value, out r)) {
                                        value = r;
                                        ok = true;
                                    }
                                }
                                break;
                            case TypeCode.Byte: {
                                    byte? r;
                                    if (TryGetTypedValue(value, out r)) {
                                        value = r;
                                        ok = true;
                                    }
                                }
                                break;
                        }
                    }
                    if (!ok) {
                        new Diagnostic(context, simpleType, DiagnosticCode.InvalidSimpleTypeValueClrType, value.GetType().FullName, targetType.FullName);
                        return false;
                    }
                }
            }
            if (simpleTypeInfo == null && !_valueClrTypeDefaultDict.TryGetValue(value.GetType(), out typeKind)) {
                new Diagnostic(context, simpleType, DiagnosticCode.InvalidSimpleTypeValueClrType, value.GetType().FullName, _valueClrTypeNames);
                return false;
            }
            //var effValue = value;
            switch (typeKind) {
                case TypeKind.ListedSimpleType: {
                        var listValue = (ListedSimpleTypeValue)value;
                        var itemTypeInfo = simpleTypeInfo != null ? simpleTypeInfo.ItemType : null;
                        var ok = true;
                        for (var i = 0; i < listValue.Count; i++) {
                            var item = listValue[i];
                            if (TryValidateValue(ref item, itemTypeInfo, context, simpleType, dts))
                                listValue.Set(i, item, true);
                            else ok = false;
                        }
                        if (!ok) return false;
                    }
                    break;
                case TypeKind.UnitedSimpleType: {
                        var unionValue = (UnitedSimpleTypeValue)value;
                        unionValue.ResovledIndex = null;
                        if (simpleTypeInfo == null) {
                            var value2 = unionValue.Value;
                            if (TryValidateValue(ref value2, null, context, simpleType, dts))
                                unionValue.SetValue(value2, true);
                            else return false;
                        }
                        else {
                            var memberInfos = simpleTypeInfo.Members;
                            var found = false;
                            var dMarker = context.MarkDiagnostics();
                            for (var i = 0; i < memberInfos.Count; i++) {
                                object value2;
                                bool ok;
                                if (unionValue.Literal != null)
                                    ok = TryParseAndValidateValue(unionValue.Literal, memberInfos[i].Type, context, out value2, simpleType, dts);
                                else {
                                    value2 = unionValue.Value;
                                    ok = TryValidateValue(ref value2, memberInfos[i].Type, context, simpleType, dts);
                                }
                                if (ok) {
                                    unionValue.SetValue(value2, true);
                                    unionValue.Literal = null;
                                    unionValue.ResovledIndex = i;
                                    found = true;
                                    break;
                                }
                            }
                            dMarker.Restore();
                            if (!found) {
                                new Diagnostic(context, simpleType, DiagnosticCode.SimpleTypeValueNotMatchWithAnyUnionMemberTypes);
                                return false;
                            }
                        }
                        //effValue = unionValue.Value;
                    }
                    break;
                //case TypeKind.Notation:
                case TypeKind.FullName: {
                        var fnValue = (FullNameValue)value;
                        if (!fnValue.IsResolved) {
                            string uriValue = null;
                            if (simpleType != null) {
                                var elementAncestor = simpleType.ElementAncestor;
                                if (elementAncestor != null) uriValue = elementAncestor.TryGetNamespaceUri(fnValue.Prefix);
                            }
                            if (uriValue != null) fnValue.Uri = XNamespace.Get(uriValue);
                            else {
                                new Diagnostic(context, simpleType, DiagnosticCode.CannotResolveFullNameValue, fnValue.ToString());
                                return false;
                            }
                        }
                    }
                    break;
            }
            var facetSetInfo = simpleTypeInfo != null ? simpleTypeInfo.FacetSet : null;
            if (facetSetInfo != null) {
                if (facetSetInfo.WhitespaceNormalization != null) {
                    switch (facetSetInfo.WhitespaceNormalization.Value) {
                        case WhitespaceNormalization.Replace: value = ReplaceWhitespaces((string)value); break;
                        case WhitespaceNormalization.Collapse: value = CollapseWhitespaces((string)value); break;
                    }
                }
                var dMarker = context.MarkDiagnostics();
                if (facetSetInfo.MinLength != null || facetSetInfo.MaxLength != null) {
                    var length = GetValueLength(value);
                    if (facetSetInfo.MinLength == facetSetInfo.MaxLength) {
                        if (length != facetSetInfo.MinLength)
                            new Diagnostic(context, simpleType, DiagnosticCode.LengthNotEqualTo, length, facetSetInfo.MinLength);
                    }
                    else if (length < facetSetInfo.MinLength)
                        new Diagnostic(context, simpleType, DiagnosticCode.LengthNotGreaterThanOrEqualTo, length, facetSetInfo.MinLength);
                    else if (length > facetSetInfo.MaxLength)
                        new Diagnostic(context, simpleType, DiagnosticCode.LengthNotLessThanOrEqualTo, length, facetSetInfo.MaxLength);
                }
                if (facetSetInfo.TotalDigits != null || facetSetInfo.FractionDigits != null) {
                    byte fractionDigits;
                    var totalDigits = GetTotalDigits(value, out fractionDigits);
                    if (totalDigits > facetSetInfo.TotalDigits)
                        new Diagnostic(context, simpleType, DiagnosticCode.TotalDigitsNotLessThanOrEqualTo, totalDigits, facetSetInfo.TotalDigits);
                    if (fractionDigits > facetSetInfo.FractionDigits)
                        new Diagnostic(context, simpleType, DiagnosticCode.FractionDigitsNotLessThanOrEqualTo, fractionDigits, facetSetInfo.FractionDigits);
                }
                string valueString = null;
                if (facetSetInfo.LowerValue != null || facetSetInfo.UpperValue != null) {
                    if (facetSetInfo.LowerValue != null) {
                        var r = CompareValue(value, facetSetInfo.LowerValue);
                        if (facetSetInfo.LowerValueInclusive) {
                            if (r < 0)
                                new Diagnostic(context, simpleType, DiagnosticCode.ValueNotGreaterThanOrEqualTo,
                                    GetValueString(ref valueString, value, simpleTypeInfo), facetSetInfo.LowerValueText);
                        }
                        else if (r <= 0)
                            new Diagnostic(context, simpleType, DiagnosticCode.ValueNotGreaterThan,
                                GetValueString(ref valueString, value, simpleTypeInfo), facetSetInfo.LowerValueText);
                    }
                    if (facetSetInfo.UpperValue != null) {
                        var r = CompareValue(value, facetSetInfo.UpperValue);
                        if (facetSetInfo.UpperValueInclusive) {
                            if (r > 0)
                                new Diagnostic(context, simpleType, DiagnosticCode.ValueNotLessThanOrEqualTo,
                                    GetValueString(ref valueString, value, simpleTypeInfo), facetSetInfo.UpperValueText);
                        }
                        else if (r >= 0)
                            new Diagnostic(context, simpleType, DiagnosticCode.ValueNotLessThan,
                                GetValueString(ref valueString, value, simpleTypeInfo), facetSetInfo.UpperValueText);
                    }
                }
                if (facetSetInfo.Enumerations != null) {
                    if (!facetSetInfo.EnumerationsContains(value))
                        new Diagnostic(context, simpleType, DiagnosticCode.ValueNotInEnumerations,
                            GetValueString(ref valueString, value, simpleTypeInfo), facetSetInfo.EnumerationsText);
                }
                if (facetSetInfo.Patterns != null) {
                    GetValueString(ref valueString, value, simpleTypeInfo);
                    foreach (var patternInfo in facetSetInfo.Patterns) {
                        var match = patternInfo.Regex.Match(valueString);
                        if (!(match.Success && match.Index == 0 && match.Length == valueString.Length))
                            new Diagnostic(context, simpleType, DiagnosticCode.CanonicalStringNotMatchWithPattern, valueString, patternInfo.Pattern);
                    }
                }
                return !dMarker.HasErrors;
            }
            return true;
        }
        private static ulong GetValueLength(object value) {
            var str = value as string;
            if (str != null) return (ulong)str.Length;
            var bytes = value as byte[];
            if (bytes != null) return (ulong)bytes.LongLength;
            var listValue = value as ListedSimpleTypeValue;
            if (listValue != null) return (ulong)listValue.Count;
            var xNamespace = value as XNamespace;
            if (xNamespace != null) return (ulong)xNamespace.NamespaceName.Length;
            var fnValue = value as FullNameValue;
            if (fnValue != null) return (ulong)fnValue.ToString().Length;
            throw new InvalidOperationException();
        }
        private static byte GetTotalDigits(object value, out byte fractionDigits) {
            var sqlDecimal = new System.Data.SqlTypes.SqlDecimal(Convert.ToDecimal(value));
            fractionDigits = sqlDecimal.Scale;
            return sqlDecimal.Precision;
        }
        private static string GetValueString(ref string valueString, object value, ISimpleTypeInfo simpleTypeInfo) {
            if (valueString == null) {
                valueString = ToString(value, simpleTypeInfo);
                if (valueString == null) throw new InvalidOperationException();
            }
            return valueString;
        }
        public static string ReplaceWhitespaces(string s) {
            if (s == null) throw new ArgumentNullException("s");
            var sb = new StringBuilder(s.Length);
            for (var i = 0; i < s.Length; i++) {
                var c = s[i];
                if (/*c == ' ' ||*/ c == '\n' || c == '\r' || c == '\t') sb.Append(' ');
                else sb.Append(c);
            }
            return sb.ToString();
        }
        public static string CollapseWhitespaces(string s) {//todo: more efficient impl
            if (s == null) throw new ArgumentNullException("s");
            s = ReplaceWhitespaces(s);
            var sb = new StringBuilder(s.Length);
            var lastChar = '\0';
            for (var i = 0; i < s.Length; i++) {
                var c = s[i];
                if (c == ' ' && lastChar == ' ') continue;
                sb.Append(c);
                lastChar = c;
            }
            return sb.ToString().Trim(' ');
        }
        public static bool IsNullOrWhitespace(string s) { return s == null || TrimWhitespaces(s).Length == 0; }
        public static string TrimWhitespaces(string s) {
            if (s == null) throw new ArgumentNullException("s");
            return s.Trim(_whitespaceChars);
        }
        private static readonly char[] _whitespaceChars = new[] { ' ', '\n', '\r', '\t' };
        //
        public static string ToSeparatedString(IReadOnlyList<object> values, ISimpleTypeInfo simpleTypeInfo = null, string separator = ", ") {
            if (values == null) return null;
            var count = values.Count;
            if (count == 0) return "";
            if (count == 1) return ToString(values[0], simpleTypeInfo);
            var sb = new StringBuilder();
            for (var i = 0; i < count; i++) {
                if (i > 0) sb.Append(separator);
                sb.Append(ToString(values[i], simpleTypeInfo));
            }
            return sb.ToString();
        }
        public static string ToString(object value, ISimpleTypeInfo simpleTypeInfo = null) {
            if (value == null) return null;
            TypeKind? typeKind = null;
            if (simpleTypeInfo != null) {
                typeKind = simpleTypeInfo.Kind;
                if (typeKind == TypeKind.SimpleType) typeKind = null;
            }
            if (typeKind == null) {
                TypeKind tk;
                if (_valueClrTypeDefaultDict.TryGetValue(value.GetType(), out tk)) typeKind = tk;
            }
            if (typeKind == null) {
                var formattable = value as IFormattable;
                if (formattable != null) return formattable.ToString(null, CultureInfo.InvariantCulture);
                return value.ToString();
            }
            switch (typeKind.Value) {
                case TypeKind.ListedSimpleType: return ((ListedSimpleTypeValue)value).ToString(simpleTypeInfo);
                case TypeKind.UnitedSimpleType: return ((UnitedSimpleTypeValue)value).ToString(simpleTypeInfo);
                case TypeKind.String:
                case TypeKind.NormalizedString:
                case TypeKind.Token:
                case TypeKind.Language:
                case TypeKind.NameToken:
                case TypeKind.Name:
                case TypeKind.NonColonizedName:
                case TypeKind.Id:
                case TypeKind.IdRef:
                case TypeKind.Entity: return (string)value;
                case TypeKind.Uri:
                //case TypeKind.Notation:
                case TypeKind.FullName: return value.ToString();
                case TypeKind.Decimal:
                case TypeKind.Integer:
                case TypeKind.NonPositiveInteger:
                case TypeKind.NegativeInteger:
                case TypeKind.NonNegativeInteger:
                case TypeKind.PositiveInteger:
                case TypeKind.Int64:
                case TypeKind.Int32:
                case TypeKind.Int16:
                case TypeKind.SByte:
                case TypeKind.UInt64:
                case TypeKind.UInt32:
                case TypeKind.UInt16:
                case TypeKind.Byte: return ((IFormattable)value).ToString(null, NumberFormatInfo.InvariantInfo);
                case TypeKind.Double: return Double.ToString((double)value);
                case TypeKind.Single: return Single.ToString((float)value);
                case TypeKind.Boolean: return Boolean.ToString((bool)value);
                case TypeKind.Base64Binary: return Base64Binary.ToString((byte[])value);
                case TypeKind.HexBinary: return HexBinary.ToString((byte[])value);
                case TypeKind.TimeSpan: return TimeSpan.ToString((STimeSpan)value);
                case TypeKind.DateTime: return DateTime.ToString((SDateTime)value);
                case TypeKind.Date: return Date.ToString((SDateTime)value);
                case TypeKind.Time: return Time.ToString((SDateTime)value);
                case TypeKind.YearMonth: return YearMonth.ToString((SDateTime)value);
                case TypeKind.Year: return Year.ToString((SDateTime)value);
                case TypeKind.MonthDay: return MonthDay.ToString((SDateTime)value);
                case TypeKind.Month: return Month.ToString((SDateTime)value);
                case TypeKind.Day: return Day.ToString((SDateTime)value);
                default: throw new ArgumentException("Invalid type kind: " + typeKind);
            }
        }
        public static bool TryParseValue(string literal, ISimpleTypeInfo simpleTypeInfo, Context context, out object result, SimpleType simpleType = null, DateTimeStyles dts = DateTimeStyles.None) {
            if (literal == null) throw new ArgumentNullException("literal");
            if (context == null) throw new ArgumentNullException("context");
            result = null;
            if (simpleTypeInfo == null) simpleTypeInfo = ThisInfo;
            var typeKind = simpleTypeInfo.Kind;
            switch (typeKind) {
                case TypeKind.ListedSimpleType: {
                        var itemTypeInfo = simpleTypeInfo.ItemType;
                        var listValue = new ListedSimpleTypeValue();
                        var itemStrings = literal.Split(_whitespaceChars, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var itemString in itemStrings) {
                            object item;
                            if (!TryParseValue(itemString, itemTypeInfo, context, out item, simpleType, dts)) return false;
                            listValue.Add(item, true);
                        }
                        result = listValue;
                        return true;
                    }
                case TypeKind.UnitedSimpleType:
                    result = new UnitedSimpleTypeValue(null, true, literal);
                    return true;
                case TypeKind.SimpleType:
                case TypeKind.String:
                case TypeKind.NormalizedString:
                case TypeKind.Token:
                case TypeKind.Language:
                case TypeKind.NameToken:
                case TypeKind.Name:
                case TypeKind.NonColonizedName:
                case TypeKind.Id:
                case TypeKind.IdRef:
                case TypeKind.Entity: result = literal; return true;
                case TypeKind.Uri: {
                        XNamespace r;
                        if (Uri.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                //case TypeKind.Notation:
                case TypeKind.FullName: {
                        FullNameValue r;
                        if (FullNameValue.TryParse(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.Decimal:
                case TypeKind.Integer:
                case TypeKind.NonPositiveInteger:
                case TypeKind.NegativeInteger:
                case TypeKind.NonNegativeInteger:
                case TypeKind.PositiveInteger: {
                        decimal r;
                        if (Decimal.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.Int64: {
                        long r;
                        if (Int64.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.Int32: {
                        int r;
                        if (Int32.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.Int16: {
                        short r;
                        if (Int16.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.SByte: {
                        sbyte r;
                        if (SByte.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.UInt64: {
                        ulong r;
                        if (UInt64.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.UInt32: {
                        uint r;
                        if (UInt32.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.UInt16: {
                        ushort r;
                        if (UInt16.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.Byte: {
                        byte r;
                        if (Byte.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.Double: {
                        double r;
                        if (Double.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.Single: {
                        float r;
                        if (Single.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.Boolean: {
                        bool r;
                        if (Boolean.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.Base64Binary: {
                        byte[] r;
                        if (Base64Binary.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.HexBinary: {
                        byte[] r;
                        if (HexBinary.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.TimeSpan: {
                        STimeSpan r;
                        if (TimeSpan.TryParseValue(literal, out r)) { result = r; return true; }
                    }
                    break;
                case TypeKind.DateTime: {
                        SDateTime r;
                        if (DateTime.TryParseValue(literal, out r, dts)) { result = r; return true; }
                    }
                    break;
                case TypeKind.Date: {
                        SDateTime r;
                        if (Date.TryParseValue(literal, out r, dts)) { result = r; return true; }
                    }
                    break;
                case TypeKind.Time: {
                        SDateTime r;
                        if (Time.TryParseValue(literal, out r, dts)) { result = r; return true; }
                    }
                    break;
                case TypeKind.YearMonth: {
                        SDateTime r;
                        if (YearMonth.TryParseValue(literal, out r, dts)) { result = r; return true; }
                    }
                    break;
                case TypeKind.Year: {
                        SDateTime r;
                        if (Year.TryParseValue(literal, out r, dts)) { result = r; return true; }
                    }
                    break;
                case TypeKind.MonthDay: {
                        SDateTime r;
                        if (MonthDay.TryParseValue(literal, out r, dts)) { result = r; return true; }
                    }
                    break;
                case TypeKind.Month: {
                        SDateTime r;
                        if (Month.TryParseValue(literal, out r, dts)) { result = r; return true; }
                    }
                    break;
                case TypeKind.Day: {
                        SDateTime r;
                        if (Day.TryParseValue(literal, out r, dts)) { result = r; return true; }
                    }
                    break;
                default: throw new ArgumentException("Invalid type kind: " + typeKind);
            }
            new Diagnostic(context, simpleType, DiagnosticCode.InvalidSimpleTypeLiteral, typeKind, literal);
            return false;
        }
        public static bool TryParseAndValidateValue(string literal, ISimpleTypeInfo simpleTypeInfo, Context context, out object result, SimpleType simpleType = null, DateTimeStyles dts = DateTimeStyles.None) {
            if (TryParseValue(literal, simpleTypeInfo, context, out result, simpleType, dts))
                return TryValidateValue(ref result, simpleTypeInfo, context, simpleType, dts);
            return false;
        }
    }
    [Serializable]
    public sealed class ListedSimpleTypeValue : IList<object>, IReadOnlyList<object>, IEquatable<ListedSimpleTypeValue>, ICloneable {
        public ListedSimpleTypeValue() { _itemList = new List<object>(); }
        public ListedSimpleTypeValue(IEnumerable<object> items, bool direct = false) : this() { if (items != null) AddRange(items, direct); }
        //
        private readonly List<object> _itemList;
        public int Count { get { return _itemList.Count; } }
        public int IndexOf(object item) {
            for (var i = 0; i < _itemList.Count; i++)
                if (SimpleType.ValueEquals(_itemList[i], item)) return i;
            return -1;
        }
        public bool Contains(object item) { return IndexOf(item) != -1; }
        public object Add(object item, bool direct = false) {
            if (!direct) item = SimpleType.CloneValue(item);
            _itemList.Add(item);
            return item;
        }
        void ICollection<object>.Add(object item) { Add(item); }
        public void AddRange(IEnumerable<object> items, bool direct = false) {
            if (items == null) throw new ArgumentNullException("items");
            foreach (var item in items) Add(item, direct);
        }
        public object Insert(int index, object item, bool direct = false) {
            if (!direct) item = SimpleType.CloneValue(item);
            _itemList.Insert(index, item);
            return item;
        }
        void IList<object>.Insert(int index, object item) { Insert(index, item); }
        public object this[int index] {
            get { return _itemList[index]; }
            set { Set(index, value); }
        }
        public object Set(int index, object item, bool direct = false) {
            if (!direct) item = SimpleType.CloneValue(item);
            _itemList[index] = item;
            return item;
        }
        public bool Remove(object item) {
            var idx = IndexOf(item);
            if (idx == -1) return false;
            _itemList.RemoveAt(idx);
            return true;
        }
        public void RemoveAt(int index) { _itemList.RemoveAt(index); }
        public void Clear() { _itemList.Clear(); }
        public IEnumerator<object> GetEnumerator() { return _itemList.GetEnumerator(); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }
        public void CopyTo(object[] array, int arrayIndex) { _itemList.CopyTo(array, arrayIndex); }
        public bool IsReadOnly { get { return false; } }
        public object Clone() { return new ListedSimpleTypeValue(this); }
        public string ToString(ISimpleTypeInfo simpleTypeInfo) {
            return SimpleType.ToSeparatedString(_itemList, simpleTypeInfo != null ? simpleTypeInfo.ItemType : null, " ");
        }
        public bool Equals(ListedSimpleTypeValue other) {
            if (object.ReferenceEquals(this, other)) return true;
            if (object.ReferenceEquals(other, null)) return false;
            return SimpleType.ValuesEquals(_itemList, other._itemList);
        }
        public override bool Equals(object obj) { return Equals(obj as ListedSimpleTypeValue); }
        public override int GetHashCode() { return SimpleType.GetValuesHashCode(_itemList); }
        public static bool operator ==(ListedSimpleTypeValue left, ListedSimpleTypeValue right) {
            if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
            return left.Equals(right);
        }
        public static bool operator !=(ListedSimpleTypeValue left, ListedSimpleTypeValue right) { return !(left == right); }
    }
    [Serializable]
    public abstract class ListedSimpleType<T> : SimpleType, IList<T>, IReadOnlyList<T> {
        protected ListedSimpleType() { }
        protected ListedSimpleType(ListedSimpleTypeValue value, bool direct = false) : base(value, direct) { }
        public static implicit operator ListedSimpleTypeValue(ListedSimpleType<T> obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        new public ListedSimpleTypeValue Value { get { return GenericValue as ListedSimpleTypeValue; } set { GenericValue = value; } }
        public ListedSimpleTypeValue EnsureValue() {
            var value = Value;
            if (value == null) {
                value = new ListedSimpleTypeValue();
                SetValue(value, true);
            }
            return value;
        }
        public ListedSimpleTypeInfo ListedSimpleTypeInfo { get { return (ListedSimpleTypeInfo)ObjectInfo; } }
        //
        public int Count { get { return EnsureValue().Count; } }
        public bool Contains(T item) { return EnsureValue().Contains(item); }
        public int IndexOf(T item) { return EnsureValue().IndexOf(item); }
        public T Add(T item, bool direct = false) { return (T)EnsureValue().Add(item, direct); }
        void ICollection<T>.Add(T item) { Add(item); }
        public void AddRange(IEnumerable<T> items, bool direct = false) {
            if (items == null) throw new ArgumentNullException("items");
            EnsureValue();
            foreach (var item in items) Add(item, direct);
        }
        public T Insert(int index, T item, bool direct = false) { return (T)EnsureValue().Insert(index, item, direct); }
        void IList<T>.Insert(int index, T item) { Insert(index, item); }
        public T this[int index] {
            get { return TryGetTypedItem(EnsureValue()[index]); }
            set { EnsureValue()[index] = value; }
        }
        public T Set(int index, T item, bool direct = false) { return (T)EnsureValue().Set(index, item, direct); }
        public bool Remove(T item) { return EnsureValue().Remove(item); }
        public void RemoveAt(int index) { EnsureValue().RemoveAt(index); }
        public void Clear() { EnsureValue().Clear(); }
        public IEnumerator<T> GetEnumerator() {
            using (var enumerator = EnsureValue().GetEnumerator())
                while (enumerator.MoveNext())
                    yield return TryGetTypedItem(enumerator.Current);
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }
        public void CopyTo(T[] array, int arrayIndex) { ExtensionMethods.CopyTo(this, array, arrayIndex); }
        bool ICollection<T>.IsReadOnly { get { return EnsureValue().IsReadOnly; } }
        protected abstract T TryGetTypedItem(object itemValue);
    }
    [Serializable]
    public class IdRefs : ListedSimpleType<string>, IIdRefObject {
        public IdRefs() { }
        public IdRefs(ListedSimpleTypeValue value, bool direct = false) : base(value, direct) { }
        public static implicit operator IdRefs(ListedSimpleTypeValue value) {
            if (value == null) return null;
            return new IdRefs(value);
        }
        protected override string TryGetTypedItem(object itemValue) { return itemValue as string; }
        private List<Id> _referentialIdList;
        internal List<Id> ReferentialIdList { get { return _referentialIdList ?? (_referentialIdList = new List<Id>()); } }
        public IReadOnlyList<Id> ReferentialIds { get { return ReferentialIdList; } }
        protected override bool TryValidated(Context context, bool success) {
            if (success) context.IdRefList.Add(this);
            return success;
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly ListedSimpleTypeInfo ThisInfo = new ListedSimpleTypeInfo(typeof(IdRefs), TypeKind.IdRefs.ToSystemName(), IdRef.ThisInfo);
    }
    [Serializable]
    public class Entities : ListedSimpleType<string> {
        public Entities() { }
        public Entities(ListedSimpleTypeValue value, bool direct = false) : base(value, direct) { }
        public static implicit operator Entities(ListedSimpleTypeValue value) {
            if (value == null) return null;
            return new Entities(value);
        }
        protected override string TryGetTypedItem(object itemValue) { return itemValue as string; }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly ListedSimpleTypeInfo ThisInfo = new ListedSimpleTypeInfo(typeof(Entities), TypeKind.Entities.ToSystemName(), Entity.ThisInfo);
    }
    [Serializable]
    public class NameTokens : ListedSimpleType<string> {
        public NameTokens() { }
        public NameTokens(ListedSimpleTypeValue value, bool direct = false) : base(value, direct) { }
        public static implicit operator NameTokens(ListedSimpleTypeValue value) {
            if (value == null) return null;
            return new NameTokens(value);
        }
        protected override string TryGetTypedItem(object itemValue) { return itemValue as string; }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly ListedSimpleTypeInfo ThisInfo = new ListedSimpleTypeInfo(typeof(NameTokens), TypeKind.NameTokens.ToSystemName(), NameToken.ThisInfo);
    }
    [Serializable]
    public sealed class UnitedSimpleTypeValue : IEquatable<UnitedSimpleTypeValue>, ICloneable {
        public UnitedSimpleTypeValue() { }
        public UnitedSimpleTypeValue(object value, bool direct = false, string literal = null, int? resovledIndex = null) {
            SetValue(value, direct);
            _literal = literal;
            _resovledIndex = resovledIndex;
        }
        private object _value;
        public object SetValue(object value, bool direct = false) {
            if (!direct) value = SimpleType.CloneValue(value);
            _value = value;
            _resovledIndex = null;
            return value;
        }
        public object Value { get { return _value; } set { SetValue(value); } }
        public bool HasValue { get { return _value != null; } }
        private string _literal;
        public string Literal { get { return _literal; } internal set { _literal = value; } }
        private int? _resovledIndex;
        public int? ResovledIndex { get { return _resovledIndex; } internal set { _resovledIndex = value; } }
        public bool IsResolved { get { return _resovledIndex != null; } }
        public object Clone() { return new UnitedSimpleTypeValue(_value, false, _literal, _resovledIndex); }
        public string ToString(ISimpleTypeInfo simpleTypeInfo) {
            if (IsResolved && simpleTypeInfo != null) return SimpleType.ToString(_value, simpleTypeInfo.Members[_resovledIndex.Value].Type);
            if (_value != null) return SimpleType.ToString(_value, null);
            return _literal;
        }
        public bool Equals(UnitedSimpleTypeValue other) {
            if (object.ReferenceEquals(this, other)) return true;
            if (object.ReferenceEquals(other, null)) return false;
            if (_value != null) return SimpleType.ValueEquals(_value, other._value);
            return _literal == other._literal;
        }
        public override bool Equals(object obj) { return Equals(obj as UnitedSimpleTypeValue); }
        public override int GetHashCode() {
            if (_value != null) return SimpleType.GetValueHashCode(_value);
            return _literal == null ? 0 : _literal.GetHashCode();
        }
        public static bool operator ==(UnitedSimpleTypeValue left, UnitedSimpleTypeValue right) {
            if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
            return left.Equals(right);
        }
        public static bool operator !=(UnitedSimpleTypeValue left, UnitedSimpleTypeValue right) { return !(left == right); }
    }
    [Serializable]
    public abstract class UnitedSimpleType : SimpleType {
        protected UnitedSimpleType() { }
        protected UnitedSimpleType(UnitedSimpleTypeValue value, bool direct = false) : base(value, direct) { }
        public static implicit operator UnitedSimpleTypeValue(UnitedSimpleType obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        new public UnitedSimpleTypeValue Value { get { return GenericValue as UnitedSimpleTypeValue; } set { GenericValue = value; } }
        public UnitedSimpleTypeValue EnsureValue() {
            var value = Value;
            if (value == null) {
                value = new UnitedSimpleTypeValue();
                SetValue(value, true);
            }
            return value;
        }
        public object SetNetValue(object netValue, bool direct = false) { return EnsureValue().SetValue(netValue, direct); }
        public object NetValue {
            get {
                var value = Value;
                return value == null ? null : value.Value;
            }
            set { EnsureValue().Value = value; }
        }
        public bool HasNetValue { get { return NetValue != null; } }
        public int? ResovledIndex {
            get {
                var value = Value;
                return value == null ? null : value.ResovledIndex;
            }
        }
        public bool IsResoved {
            get {
                var value = Value;
                return value == null ? false : value.IsResolved;
            }
        }
        public UnitedSimpleTypeInfo UnitedSimpleTypeInfo { get { return (UnitedSimpleTypeInfo)ObjectInfo; } }
    }
    #region Atomic simple types
    [Serializable]
    public abstract class AtomicSimpleType : SimpleType {
        protected AtomicSimpleType() { }
        protected AtomicSimpleType(object value, bool direct = false) : base(value, direct) { }
        public AtomicSimpleTypeInfo AtomicSimpleTypeInfo { get { return (AtomicSimpleTypeInfo)ObjectInfo; } }
    }
    [Serializable]
    public class String : AtomicSimpleType {
        public String() { }
        public String(string value) : base(value) { }
        new public string Value { get { return GenericValue as string; } set { GenericValue = value; } }
        public static implicit operator String(string value) {
            if (value == null) return null;
            return new String(value);
        }
        public static implicit operator string(String obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(String), TypeKind.String, TypeKind.String.ToSystemName(),
             SimpleType.ThisInfo, typeof(string), null);
    }
    [Serializable]
    public class NormalizedString : String {
        public NormalizedString() { }
        public NormalizedString(string value) : base(value) { }
        public static implicit operator NormalizedString(string value) {
            if (value == null) return null;
            return new NormalizedString(value);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(NormalizedString), TypeKind.NormalizedString, TypeKind.NormalizedString.ToSystemName(),
            String.ThisInfo, typeof(string), new FacetSetInfo(whitespaceNormalization: WhitespaceNormalization.Replace));
    }
    [Serializable]
    public class Token : NormalizedString {
        public Token() { }
        public Token(string value) : base(value) { }
        public static implicit operator Token(string value) {
            if (value == null) return null;
            return new Token(value);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Token), TypeKind.Token, TypeKind.Token.ToSystemName(),
            NormalizedString.ThisInfo, typeof(string), new FacetSetInfo(whitespaceNormalization: WhitespaceNormalization.Collapse));
    }
    [Serializable]
    public class Language : Token {
        public Language() { }
        public Language(string value) : base(value) { }
        public static implicit operator Language(string value) {
            if (value == null) return null;
            return new Language(value);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Language), TypeKind.Language, TypeKind.Language.ToSystemName(),
            Token.ThisInfo, typeof(string), new FacetSetInfo(whitespaceNormalization: WhitespaceNormalization.Collapse, patterns: new[] { new PatternItemInfo(@"^[a-zA-Z]{1,8}(-[a-zA-Z0-9]{1,8})*$") }));
    }
    [Serializable]
    public class NameToken : Token {
        public NameToken() { }
        public NameToken(string value) : base(value) { }
        public static implicit operator NameToken(string value) {
            if (value == null) return null;
            return new NameToken(value);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(NameToken), TypeKind.NameToken, TypeKind.NameToken.ToSystemName(),
            Token.ThisInfo, typeof(string), new FacetSetInfo(whitespaceNormalization: WhitespaceNormalization.Collapse, patterns: new[] { new PatternItemInfo(@"^\p{_xmlC}+$") }));
    }
    [Serializable]
    public class Name : Token {
        public Name() { }
        public Name(string value) : base(value) { }
        public static implicit operator Name(string value) {
            if (value == null) return null;
            return new Name(value);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Name), TypeKind.Name, TypeKind.Name.ToSystemName(),
            Token.ThisInfo, typeof(string), new FacetSetInfo(whitespaceNormalization: WhitespaceNormalization.Collapse, patterns: new[] { new PatternItemInfo(@"^\p{_xmlI}\p{_xmlC}*$") }));
    }
    [Serializable]
    public class NonColonizedName : Name {
        public NonColonizedName() { }
        public NonColonizedName(string value) : base(value) { }
        public static implicit operator NonColonizedName(string value) {
            if (value == null) return null;
            return new NonColonizedName(value);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(NonColonizedName), TypeKind.NonColonizedName, TypeKind.NonColonizedName.ToSystemName(),
            Name.ThisInfo, typeof(string), new FacetSetInfo(whitespaceNormalization: WhitespaceNormalization.Collapse, patterns: new[] { new PatternItemInfo(@"^[\p{_xmlI}-[:]][\p{_xmlC}-[:]]*$") }));
    }
    [Serializable]
    public class Id : NonColonizedName {
        public Id() { }
        public Id(string value) : base(value) { }
        public static implicit operator Id(string value) {
            if (value == null) return null;
            return new Id(value);
        }
        protected override bool TryValidated(Context context, bool success) {
            if (success) success = context.TryAddId(this);
            return success;
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Id), TypeKind.Id, TypeKind.Id.ToSystemName(),
            NonColonizedName.ThisInfo, typeof(string), NonColonizedName.ThisInfo.FacetSet);
    }
    [Serializable]
    public class IdRef : NonColonizedName, IIdRefObject {
        public IdRef() { }
        public IdRef(string value) : base(value) { }
        public static implicit operator IdRef(string value) {
            if (value == null) return null;
            return new IdRef(value);
        }
        private Id _referential;
        public Id Referential { get { return _referential; } internal set { _referential = value; } }
        protected override bool TryValidated(Context context, bool success) {
            if (success) context.IdRefList.Add(this);
            return success;
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(IdRef), TypeKind.IdRef, TypeKind.IdRef.ToSystemName(),
            NonColonizedName.ThisInfo, typeof(string), NonColonizedName.ThisInfo.FacetSet);
    }
    [Serializable]
    public class Entity : NonColonizedName {
        public Entity() { }
        public Entity(string value) : base(value) { }
        public static implicit operator Entity(string value) {
            if (value == null) return null;
            return new Entity(value);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Entity), TypeKind.Entity, TypeKind.Entity.ToSystemName(),
            NonColonizedName.ThisInfo, typeof(string), NonColonizedName.ThisInfo.FacetSet);
    }
    [Serializable]
    public class Uri : AtomicSimpleType {
        public Uri() { }
        public Uri(XNamespace value) : base(value) { }
        new public XNamespace Value { get { return GenericValue as XNamespace; } set { GenericValue = value; } }
        public static implicit operator Uri(XNamespace value) {
            if (value == null) return null;
            return new Uri(value);
        }
        public static implicit operator XNamespace(Uri obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        public static bool TryParseValue(string literal, out XNamespace result) {
            result = XNamespace.Get(SimpleType.TrimWhitespaces(literal));
            return true;
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Uri), TypeKind.Uri, TypeKind.Uri.ToSystemName(),
            SimpleType.ThisInfo, typeof(XNamespace), null);
    }
    [Serializable]
    public sealed class FullNameValue : IEquatable<FullNameValue>, ICloneable {
        public FullNameValue(XNamespace uri, string localName, string prefix = "") {
            _uri = uri;
            _localName = XmlConvert.VerifyNCName(localName);
            if (string.IsNullOrEmpty(prefix)) _prefix = "";
            else _prefix = XmlConvert.VerifyNCName(prefix);
        }
        private XNamespace _uri;
        public XNamespace Uri { get { return _uri; } set { _uri = value; } }
        public string UriValue { get { return _uri == null ? null : _uri.NamespaceName; } }
        public bool IsResolved { get { return _uri != null; } }
        private readonly string _localName;
        public string LocalName { get { return _localName; } }
        private readonly string _prefix;
        public string Prefix { get { return _prefix; } }
        public object Clone() { return base.MemberwiseClone(); }
        public XName ToXName() {
            if (_uri == null) throw new InvalidOperationException("Uri not set");
            return _uri.GetName(_localName);
        }
        public static FullNameValue From(XName xName) {
            if (xName == null) throw new ArgumentNullException("xName");
            return new FullNameValue(xName.Namespace, xName.LocalName);
        }
        public static bool TryParse(string literal, out FullNameValue result) {
            literal = SimpleType.TrimWhitespaces(literal);
            result = null;
            if (literal.Length == 0) return false;
            string uriValue = null;
            string prefix = null;
            int seperatorIdx;
            if (literal[0] == '{') {
                seperatorIdx = literal.LastIndexOf('}');
                if (seperatorIdx == -1 || seperatorIdx == literal.Length - 1) return false;
                uriValue = SimpleType.TrimWhitespaces(literal.Substring(1, seperatorIdx - 1));
            }
            else {
                seperatorIdx = literal.IndexOf(':');
                if (seperatorIdx == 0 || seperatorIdx == literal.Length - 1) return false;
                if (seperatorIdx != -1) prefix = literal.Substring(0, seperatorIdx);
            }
            try {
                result = new FullNameValue(uriValue == null ? null : XNamespace.Get(uriValue), literal.Substring(seperatorIdx + 1), prefix);
                return true;
            }
            catch (Exception) { return false; }
        }
        public override string ToString() {
            if (_uri != null) return "{" + _uri.NamespaceName + "}" + _localName;
            if (_prefix.Length > 0) return _prefix + ":" + _localName;
            return _localName;
        }
        public bool Equals(FullNameValue other) {
            if (object.ReferenceEquals(this, other)) return true;
            if (object.ReferenceEquals(other, null)) return false;
            if (_uri != null) return _uri == other._uri && _localName == other._localName;
            return _localName == other._localName && _prefix == other._prefix;
        }
        public override bool Equals(object obj) { return Equals(obj as FullNameValue); }
        public override int GetHashCode() { return _localName.GetHashCode(); }
        public static bool operator ==(FullNameValue left, FullNameValue right) {
            if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
            return left.Equals(right);
        }
        public static bool operator !=(FullNameValue left, FullNameValue right) { return !(left == right); }
    }
    [Serializable]
    public class FullName : AtomicSimpleType {
        public FullName() { }
        public FullName(FullNameValue value) : base(value) { }
        new public FullNameValue Value { get { return GenericValue as FullNameValue; } set { GenericValue = value; } }
        public static implicit operator FullName(FullNameValue value) {
            if (value == null) return null;
            return new FullName(value);
        }
        public static implicit operator FullNameValue(FullName obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(FullName), TypeKind.FullName, TypeKind.FullName.ToSystemName(),
            SimpleType.ThisInfo, typeof(FullNameValue), null);
    }
    //[Serializable]
    //public class Notation : AtomicSimpleType {
    //    public Notation() { }
    //    public Notation(FullNameValue value) : base(value) { }
    //    new public FullNameValue Value { get { return GenericValue as FullNameValue; } set { GenericValue = value; } }
    //    public static implicit operator Notation(FullNameValue value) {
    //        if (value == null) return null;
    //        return new Notation(value);
    //    }
    //    public static implicit operator FullNameValue(Notation obj) {
    //        if (obj == null) return null;
    //        return obj.Value;
    //    }
    //    public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
    //    new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(TypeKind.Notation, TypeKind.Notation.ToSystemName(),
    //        typeof(Notation), typeof(FullNameValue), SimpleType.ThisInfo, null);
    //}
    [Serializable]
    public class Decimal : AtomicSimpleType {
        public Decimal() { }
        public Decimal(decimal? value) : base(value) { }
        public static implicit operator Decimal(decimal? value) {
            if (value == null) return null;
            return new Decimal(value);
        }
        public static implicit operator decimal?(Decimal obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        new public decimal? Value {
            get {
                decimal? r;
                TryGetTypedValue(GenericValue, out r);
                return r;
            }
            set { GenericValue = value; }
        }
        //public static string ToString(decimal value) {
        //    var s = value.ToString("G29", NumberFormatInfo.InvariantInfo);
        //    //if (s.IndexOf('.') == -1) s += ".0";
        //    return s;
        //}
        public static bool TryParseValue(string literal, out decimal result) {
            return decimal.TryParse(TrimWhitespaces(literal), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, NumberFormatInfo.InvariantInfo, out result);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Decimal), TypeKind.Decimal, TypeKind.Decimal.ToSystemName(),
            SimpleType.ThisInfo, typeof(decimal), null);
    }
    [Serializable]
    public class Integer : Decimal {
        public Integer() { }
        public Integer(decimal? value) : base(value) { }
        public static implicit operator Integer(decimal? value) {
            if (value == null) return null;
            return new Integer(value);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Integer), TypeKind.Integer, TypeKind.Integer.ToSystemName(),
            Decimal.ThisInfo, typeof(decimal), new FacetSetInfo(fractionDigits: 0));
    }
    [Serializable]
    public class NonPositiveInteger : Integer {
        public NonPositiveInteger() { }
        public NonPositiveInteger(decimal? value) : base(value) { }
        public static implicit operator NonPositiveInteger(decimal? value) {
            if (value == null) return null;
            return new NonPositiveInteger(value);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(NonPositiveInteger), TypeKind.NonPositiveInteger, TypeKind.NonPositiveInteger.ToSystemName(),
            Integer.ThisInfo, typeof(decimal), new FacetSetInfo(fractionDigits: 0, upperValue: 0M, upperValueInclusive: true, upperValueText: "0"));
    }
    [Serializable]
    public class NegativeInteger : NonPositiveInteger {
        public NegativeInteger() { }
        public NegativeInteger(decimal? value) : base(value) { }
        public static implicit operator NegativeInteger(decimal? value) {
            if (value == null) return null;
            return new NegativeInteger(value);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(NegativeInteger), TypeKind.NegativeInteger, TypeKind.NegativeInteger.ToSystemName(),
            NonPositiveInteger.ThisInfo, typeof(decimal), new FacetSetInfo(fractionDigits: 0, upperValue: 0M, upperValueInclusive: false, upperValueText: "0"));
    }
    [Serializable]
    public class NonNegativeInteger : Integer {
        public NonNegativeInteger() { }
        public NonNegativeInteger(decimal? value) : base(value) { }
        public static implicit operator NonNegativeInteger(decimal? value) {
            if (value == null) return null;
            return new NonNegativeInteger(value);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(NonNegativeInteger), TypeKind.NonNegativeInteger, TypeKind.NonNegativeInteger.ToSystemName(),
            Integer.ThisInfo, typeof(decimal), new FacetSetInfo(fractionDigits: 0, lowerValue: 0M, lowerValueInclusive: true, lowerValueText: "0"));
    }
    [Serializable]
    public class PositiveInteger : NonNegativeInteger {
        public PositiveInteger() { }
        public PositiveInteger(decimal? value) : base(value) { }
        public static implicit operator PositiveInteger(decimal? value) {
            if (value == null) return null;
            return new PositiveInteger(value);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(PositiveInteger), TypeKind.PositiveInteger, TypeKind.PositiveInteger.ToSystemName(),
            NonNegativeInteger.ThisInfo, typeof(decimal), new FacetSetInfo(fractionDigits: 0, lowerValue: 0M, lowerValueInclusive: false, lowerValueText: "0"));
    }
    [Serializable]
    public class Int64 : Integer {
        public Int64() { }
        public Int64(long? value) { Value = value; }
        new public long? Value {
            get {
                long? r;
                TryGetTypedValue(GenericValue, out r);
                return r;
            }
            set { GenericValue = value; }
        }
        public static implicit operator Int64(long? value) {
            if (value == null) return null;
            return new Int64(value);
        }
        public static implicit operator long?(Int64 obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        public static bool TryParseValue(string literal, out long result) {
            return long.TryParse(TrimWhitespaces(literal), NumberStyles.AllowLeadingSign, NumberFormatInfo.InvariantInfo, out result);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Int64), TypeKind.Int64, TypeKind.Int64.ToSystemName(),
            Integer.ThisInfo, typeof(long), null);
    }
    [Serializable]
    public class Int32 : Int64 {
        public Int32() { }
        public Int32(int? value) { Value = value; }
        new public int? Value {
            get {
                int? r;
                TryGetTypedValue(GenericValue, out r);
                return r;
            }
            set { GenericValue = value; }
        }
        public static implicit operator Int32(int? value) {
            if (value == null) return null;
            return new Int32(value);
        }
        public static implicit operator int?(Int32 obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        public static bool TryParseValue(string literal, out int result) {
            return int.TryParse(TrimWhitespaces(literal), NumberStyles.AllowLeadingSign, NumberFormatInfo.InvariantInfo, out result);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Int32), TypeKind.Int32, TypeKind.Int32.ToSystemName(),
            Int64.ThisInfo, typeof(int), null);
    }
    [Serializable]
    public class Int16 : Int32 {
        public Int16() { }
        public Int16(short? value) { Value = value; }
        new public short? Value {
            get {
                short? r;
                TryGetTypedValue(GenericValue, out r);
                return r;
            }
            set { GenericValue = value; }
        }
        public static implicit operator Int16(short? value) {
            if (value == null) return null;
            return new Int16(value);
        }
        public static implicit operator short?(Int16 obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        public static bool TryParseValue(string literal, out short result) {
            return short.TryParse(TrimWhitespaces(literal), NumberStyles.AllowLeadingSign, NumberFormatInfo.InvariantInfo, out result);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Int16), TypeKind.Int16, TypeKind.Int16.ToSystemName(),
            Int32.ThisInfo, typeof(short), null);
    }
    [Serializable]
    public class SByte : Int16 {
        public SByte() { }
        public SByte(sbyte? value) { Value = value; }
        new public sbyte? Value {
            get {
                sbyte? r;
                TryGetTypedValue(GenericValue, out r);
                return r;
            }
            set { GenericValue = value; }
        }
        public static implicit operator SByte(sbyte? value) {
            if (value == null) return null;
            return new SByte(value);
        }
        public static implicit operator sbyte?(SByte obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        public static bool TryParseValue(string literal, out sbyte result) {
            return sbyte.TryParse(TrimWhitespaces(literal), NumberStyles.AllowLeadingSign, NumberFormatInfo.InvariantInfo, out result);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(SByte), TypeKind.SByte, TypeKind.SByte.ToSystemName(),
            Int16.ThisInfo, typeof(sbyte), null);
    }
    [Serializable]
    public class UInt64 : NonNegativeInteger {
        public UInt64() { }
        public UInt64(ulong? value) { Value = value; }
        new public ulong? Value {
            get {
                ulong? r;
                TryGetTypedValue(GenericValue, out r);
                return r;
            }
            set { GenericValue = value; }
        }
        public static implicit operator UInt64(ulong? value) {
            if (value == null) return null;
            return new UInt64(value);
        }
        public static implicit operator ulong?(UInt64 obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        public static bool TryParseValue(string literal, out ulong result) {
            return ulong.TryParse(TrimWhitespaces(literal), NumberStyles.AllowLeadingSign, NumberFormatInfo.InvariantInfo, out result);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(UInt64), TypeKind.UInt64, TypeKind.UInt64.ToSystemName(),
            NonNegativeInteger.ThisInfo, typeof(ulong), null);
    }
    [Serializable]
    public class UInt32 : UInt64 {
        public UInt32() { }
        public UInt32(uint? value) { Value = value; }
        new public uint? Value {
            get {
                uint? r;
                TryGetTypedValue(GenericValue, out r);
                return r;
            }
            set { GenericValue = value; }
        }
        public static implicit operator UInt32(uint? value) {
            if (value == null) return null;
            return new UInt32(value);
        }
        public static implicit operator uint?(UInt32 obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        public static bool TryParseValue(string literal, out uint result) {
            return uint.TryParse(TrimWhitespaces(literal), NumberStyles.AllowLeadingSign, NumberFormatInfo.InvariantInfo, out result);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(UInt32), TypeKind.UInt32, TypeKind.UInt32.ToSystemName(),
            UInt64.ThisInfo, typeof(uint), null);
    }
    [Serializable]
    public class UInt16 : UInt32 {
        public UInt16() { }
        public UInt16(ushort? value) { Value = value; }
        new public ushort? Value {
            get {
                ushort? r;
                TryGetTypedValue(GenericValue, out r);
                return r;
            }
            set { GenericValue = value; }
        }
        public static implicit operator UInt16(ushort? value) {
            if (value == null) return null;
            return new UInt16(value);
        }
        public static implicit operator ushort?(UInt16 obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        public static bool TryParseValue(string literal, out ushort result) {
            return ushort.TryParse(TrimWhitespaces(literal), NumberStyles.AllowLeadingSign, NumberFormatInfo.InvariantInfo, out result);
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(UInt16), TypeKind.UInt16, TypeKind.UInt16.ToSystemName(),
            UInt32.ThisInfo, typeof(ushort), null);
    }
    [Serializable]
    public class Byte : UInt16 {
        public Byte() { }
        public Byte(byte? value) { Value = value; }
        new public byte? Value {
            get {
                byte? r;
                TryGetTypedValue(GenericValue, out r);
                return r;
            }
            set { GenericValue = value; }
        }
        public static implicit operator Byte(byte? value) {
            if (value == null) return null;
            return new Byte(value);
        }
        public static implicit operator byte?(Byte obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        public static bool TryParseValue(string literal, out byte result) {
            return byte.TryParse(TrimWhitespaces(literal), NumberStyles.AllowLeadingSign, NumberFormatInfo.InvariantInfo, out result);
        }
        public static string ToString(byte value) { return value.ToString(NumberFormatInfo.InvariantInfo); }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Byte), TypeKind.Byte, TypeKind.Byte.ToSystemName(),
            UInt16.ThisInfo, typeof(byte), null);
    }
    [Serializable]
    public class Double : AtomicSimpleType {
        public Double() { }
        public Double(double? value) : base(value) { }
        new public double? Value { get { return GenericValue as double?; } set { GenericValue = value; } }
        public static implicit operator Double(double? value) {
            if (value == null) return null;
            return new Double(value);
        }
        public static implicit operator double?(Double obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        public static string ToString(double value) {
            if (double.IsNegativeInfinity(value)) return NegativeInfinityLexicalValue;
            else if (double.IsPositiveInfinity(value)) return PositiveInfinityLexicalValue;
            else if (double.IsNaN(value)) return NaNLexicalValue;
            return value.ToString("0.0###############E0", NumberFormatInfo.InvariantInfo);
        }
        public static bool TryParseValue(string literal, out double result) {
            literal = TrimWhitespaces(literal);
            if (TryParseLexicalValue(literal, out result)) return true;
            return double.TryParse(literal, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, NumberFormatInfo.InvariantInfo, out result);
        }
        private static bool TryParseLexicalValue(string literal, out double result) {
            if (literal == NegativeInfinityLexicalValue) result = double.NegativeInfinity;
            else if (literal == PositiveInfinityLexicalValue) result = double.PositiveInfinity;
            else if (literal == NaNLexicalValue) result = double.NaN;
            else { result = default(double); return false; }
            return true;
        }
        public const string NegativeInfinityLexicalValue = "-INF";
        public const string PositiveInfinityLexicalValue = "INF";
        public const string NaNLexicalValue = "NaN";
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Double), TypeKind.Double, TypeKind.Double.ToSystemName(),
            SimpleType.ThisInfo, typeof(double), null);
    }
    [Serializable]
    public class Single : AtomicSimpleType {
        public Single() { }
        public Single(float? value) : base(value) { }
        new public float? Value { get { return GenericValue as float?; } set { GenericValue = value; } }
        public static implicit operator Single(float? value) {
            if (value == null) return null;
            return new Single(value);
        }
        public static implicit operator float?(Single obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        public static string ToString(float value) {
            if (float.IsNegativeInfinity(value)) return Double.NegativeInfinityLexicalValue;
            else if (float.IsPositiveInfinity(value)) return Double.PositiveInfinityLexicalValue;
            else if (float.IsNaN(value)) return Double.NaNLexicalValue;
            return value.ToString("0.0#######E0", NumberFormatInfo.InvariantInfo);
        }
        public static bool TryParseValue(string literal, out float result) {
            literal = TrimWhitespaces(literal);
            if (TryParseLexicalValue(literal, out result)) return true;
            return float.TryParse(literal, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, NumberFormatInfo.InvariantInfo, out result);
        }
        private static bool TryParseLexicalValue(string literal, out float result) {
            if (literal == Double.NegativeInfinityLexicalValue) result = float.NegativeInfinity;
            else if (literal == Double.PositiveInfinityLexicalValue) result = float.PositiveInfinity;
            else if (literal == Double.NaNLexicalValue) result = float.NaN;
            else { result = default(float); return false; }
            return true;
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Single), TypeKind.Single, TypeKind.Single.ToSystemName(),
            SimpleType.ThisInfo, typeof(float), null);
    }
    [Serializable]
    public class Boolean : AtomicSimpleType {
        public Boolean() { }
        public Boolean(bool? value) : base(value) { }
        new public bool? Value { get { return GenericValue as bool?; } set { GenericValue = value; } }
        public static implicit operator Boolean(bool? value) {
            if (value == null) return null;
            return new Boolean(value);
        }
        public static implicit operator bool?(Boolean obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        public static string ToString(bool value) { return value ? "true" : "false"; }
        public static bool TryParseValue(string literal, out bool result) {
            literal = TrimWhitespaces(literal);
            if (literal == "true" || literal == "1") result = true;
            else if (literal == "false" || literal == "0") result = false;
            else { result = default(bool); return false; }
            return true;
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Boolean), TypeKind.Boolean, TypeKind.Boolean.ToSystemName(),
            SimpleType.ThisInfo, typeof(bool), null);
    }
    [Serializable]
    public abstract class Binary : AtomicSimpleType {
        protected Binary() { }
        protected Binary(byte[] value, bool direct = false) : base(value, direct) { }
        //ImmutableArray<byte>?
        new public byte[] Value { get { return GenericValue as byte[]; } set { GenericValue = value; } }
        public static implicit operator byte[](Binary obj) {
            if (obj == null) return null;
            return obj.Value;
        }
    }
    [Serializable]
    public class Base64Binary : Binary {
        public Base64Binary() { }
        public Base64Binary(byte[] value, bool direct = false) : base(value, direct) { }
        public static implicit operator Base64Binary(byte[] value) {
            if (value == null) return null;
            return new Base64Binary(value);
        }
        public static string ToString(byte[] value) {
            if (value == null) return null;
            return Convert.ToBase64String(value);
        }
        public static bool TryParseValue(string literal, out byte[] result) {
            try { result = Convert.FromBase64String(literal); return true; }
            catch (Exception) { result = null; return false; }
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Base64Binary), TypeKind.Base64Binary, TypeKind.Base64Binary.ToSystemName(),
            SimpleType.ThisInfo, typeof(byte[]), null);
    }
    [Serializable]
    public class HexBinary : Binary {
        public HexBinary() { }
        public HexBinary(byte[] value, bool direct = false) : base(value, direct) { }
        public static implicit operator HexBinary(byte[] value) {
            if (value == null) return null;
            return new HexBinary(value);
        }
        public static string ToString(byte[] value) {
            if (value == null) return null;
            var chars = new char[value.Length * 2];
            byte b;
            for (int bx = 0, cx = 0; bx < value.Length; ++bx, ++cx) {
                b = (byte)(value[bx] >> 4);
                chars[cx] = (char)(b > 9 ? b + 0x37 /*+ 0x20*/ : b + 0x30);
                b = (byte)(value[bx] & 0x0F);
                chars[++cx] = (char)(b > 9 ? b + 0x37 /*+ 0x20*/ : b + 0x30);
            }
            return new string(chars);
        }
        public static bool TryParseValue(string literal, out byte[] result) {
            result = null;
            literal = TrimWhitespaces(literal);
            if (literal.Length % 2 != 0) return false;
            var bytes = new byte[literal.Length / 2];
            int v;
            for (int bx = 0, sx = 0; bx < bytes.Length; ++bx, ++sx) {
                if (!TryGetValue(literal[sx], out v)) return false;
                bytes[bx] = (byte)(v << 4);
                if (!TryGetValue(literal[++sx], out v)) return false;
                bytes[bx] |= (byte)v;
            }
            result = bytes;
            return true;
        }
        private static bool TryGetValue(char c, out int r) {
            if (c >= '0' && c <= '9') r = c - '0';
            else if (c >= 'A' && c <= 'F') r = c - 'A' + 10;
            else if (c >= 'a' && c <= 'f') r = c - 'a' + 10;
            else { r = 0; return false; }
            return true;
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(HexBinary), TypeKind.HexBinary, TypeKind.HexBinary.ToSystemName(),
            SimpleType.ThisInfo, typeof(byte[]), null);
    }
    [Serializable]
    public class TimeSpan : AtomicSimpleType {
        public TimeSpan() { }
        public TimeSpan(STimeSpan? value) : base(value) { }
        new public STimeSpan? Value { get { return GenericValue as STimeSpan?; } set { GenericValue = value; } }
        public static implicit operator TimeSpan(STimeSpan? value) {
            if (value == null) return null;
            return new TimeSpan(value);
        }
        public static implicit operator STimeSpan?(TimeSpan obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        public static string ToString(STimeSpan value) { return XmlConvert.ToString(value); }
        public static bool TryParseValue(string literal, out STimeSpan result) {
            try { result = XmlConvert.ToTimeSpan(literal); return true; }
            catch (Exception) { result = default(STimeSpan); return false; }
        }
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(TimeSpan), TypeKind.TimeSpan, TypeKind.TimeSpan.ToSystemName(),
            SimpleType.ThisInfo, typeof(STimeSpan), null);
    }
    [Serializable]
    public abstract class DateTimeBase : AtomicSimpleType {
        protected DateTimeBase() { }
        protected DateTimeBase(SDateTime? value) : base(value) { }
        new public SDateTime? Value { get { return GenericValue as SDateTime?; } set { GenericValue = value; } }
        public static implicit operator SDateTime?(DateTimeBase obj) {
            if (obj == null) return null;
            return obj.Value;
        }
        protected static string ToStringCore(SDateTime value, string[] formats) {
            if (value.Kind == DateTimeKind.Local) value = value.ToUniversalTime();
            return value.ToString(formats[(int)value.Kind], DateTimeFormatInfo.InvariantInfo);
        }
        protected static bool TryParseValueCore(string s, string[] formats, DateTimeStyles style, out SDateTime result) {
            return SDateTime.TryParseExact(TrimWhitespaces(s), formats, DateTimeFormatInfo.InvariantInfo, style & ~DateTimeStyles.AllowWhiteSpaces, out result);
        }
    }
    [Serializable]
    public class DateTime : DateTimeBase {
        public DateTime() { }
        public DateTime(SDateTime? value) : base(value) { }
        public static implicit operator DateTime(SDateTime? value) {
            if (value == null) return null;
            return new DateTime(value);
        }
        public static string ToString(SDateTime value) { return ToStringCore(value, _formats); }
        public static bool TryParseValue(string literal, out SDateTime result, DateTimeStyles style = DateTimeStyles.None) { return TryParseValueCore(literal, _formats, style, out result); }
        private static readonly string[] _formats = new[] { "yyyy-MM-ddTHH:mm:ss.FFFFFFF", "yyyy-MM-ddTHH:mm:ss.FFFFFFFZ", "yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz" };
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(DateTime), TypeKind.DateTime, TypeKind.DateTime.ToSystemName(),
            SimpleType.ThisInfo, typeof(SDateTime), null);
    }
    [Serializable]
    public class Date : DateTimeBase {
        public Date() { }
        public Date(SDateTime? value) : base(value) { }
        public static implicit operator Date(SDateTime? value) {
            if (value == null) return null;
            return new Date(value);
        }
        public static string ToString(SDateTime value) { return ToStringCore(value, _formats); }
        public static bool TryParseValue(string literal, out SDateTime result, DateTimeStyles style = DateTimeStyles.None) { return TryParseValueCore(literal, _formats, style, out result); }
        private static readonly string[] _formats = new[] { "yyyy-MM-dd", "yyyy-MM-ddZ", "yyyy-MM-ddzzz" };
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Date), TypeKind.Date, TypeKind.Date.ToSystemName(),
            SimpleType.ThisInfo, typeof(SDateTime), null);
    }
    [Serializable]
    public class Time : DateTimeBase {
        public Time() { }
        public Time(SDateTime? value) : base(value) { }
        public static implicit operator Time(SDateTime? value) {
            if (value == null) return null;
            return new Time(value);
        }
        public static string ToString(SDateTime value) { return ToStringCore(value, _formats); }
        public static bool TryParseValue(string literal, out SDateTime result, DateTimeStyles style = DateTimeStyles.None) { return TryParseValueCore(literal, _formats, style, out result); }
        private static readonly string[] _formats = new[] { "HH:mm:ss.FFFFFFF", "HH:mm:ss.FFFFFFFZ", "HH:mm:ss.FFFFFFFzzz" };
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Time), TypeKind.Time, TypeKind.Time.ToSystemName(),
            SimpleType.ThisInfo, typeof(SDateTime), null);
    }
    [Serializable]
    public class YearMonth : DateTimeBase {
        public YearMonth() { }
        public YearMonth(SDateTime? value) : base(value) { }
        public static implicit operator YearMonth(SDateTime? value) {
            if (value == null) return null;
            return new YearMonth(value);
        }
        public static string ToString(SDateTime value) { return ToStringCore(value, _formats); }
        public static bool TryParseValue(string literal, out SDateTime result, DateTimeStyles style = DateTimeStyles.None) { return TryParseValueCore(literal, _formats, style, out result); }
        private static readonly string[] _formats = new[] { "yyyy-MM", "yyyy-MMZ", "yyyy-MMzzz" };
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(YearMonth), TypeKind.YearMonth, TypeKind.YearMonth.ToSystemName(),
            SimpleType.ThisInfo, typeof(SDateTime), null);
    }
    [Serializable]
    public class Year : DateTimeBase {
        public Year() { }
        public Year(SDateTime? value) : base(value) { }
        public static implicit operator Year(SDateTime? value) {
            if (value == null) return null;
            return new Year(value);
        }
        public static string ToString(SDateTime value) { return ToStringCore(value, _formats); }
        public static bool TryParseValue(string literal, out SDateTime result, DateTimeStyles style = DateTimeStyles.None) { return TryParseValueCore(literal, _formats, style, out result); }
        private static readonly string[] _formats = new[] { "yyyy", "yyyyZ", "yyyyzzz" };
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Year), TypeKind.Year, TypeKind.Year.ToSystemName(),
            SimpleType.ThisInfo, typeof(SDateTime), null);
    }
    [Serializable]
    public class MonthDay : DateTimeBase {
        public MonthDay() { }
        public MonthDay(SDateTime? value) : base(value) { }
        public static implicit operator MonthDay(SDateTime? value) {
            if (value == null) return null;
            return new MonthDay(value);
        }
        public static string ToString(SDateTime value) { return ToStringCore(value, _formats); }
        public static bool TryParseValue(string literal, out SDateTime result, DateTimeStyles style = DateTimeStyles.None) { return TryParseValueCore(literal, _formats, style, out result); }
        private static readonly string[] _formats = new[] { "--MM-dd", "--MM-ddZ", "--MM-ddzzz" };
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(MonthDay), TypeKind.MonthDay, TypeKind.MonthDay.ToSystemName(),
            SimpleType.ThisInfo, typeof(SDateTime), null);
    }
    [Serializable]
    public class Month : DateTimeBase {
        public Month() { }
        public Month(SDateTime? value) : base(value) { }
        public static implicit operator Month(SDateTime? value) {
            if (value == null) return null;
            return new Month(value);
        }
        public static string ToString(SDateTime value) { return ToStringCore(value, _formats); }
        public static bool TryParseValue(string literal, out SDateTime result, DateTimeStyles style = DateTimeStyles.None) { return TryParseValueCore(literal, _formats, style, out result); }
        private static readonly string[] _formats = new[] { "--MM", "--MMZ", "--MMzzz" };
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Month), TypeKind.Month, TypeKind.Month.ToSystemName(),
            SimpleType.ThisInfo, typeof(SDateTime), null);
    }
    [Serializable]
    public class Day : DateTimeBase {
        public Day() { }
        public Day(SDateTime? value) : base(value) { }
        public static implicit operator Day(SDateTime? value) {
            if (value == null) return null;
            return new Day(value);
        }
        public static string ToString(SDateTime value) { return ToStringCore(value, _formats); }
        public static bool TryParseValue(string literal, out SDateTime result, DateTimeStyles style = DateTimeStyles.None) { return TryParseValueCore(literal, _formats, style, out result); }
        private static readonly string[] _formats = new[] { "---dd", "---ddZ", "---ddzzz" };
        public override ObjectInfo ObjectInfo { get { return ThisInfo; } }
        new public static readonly AtomicSimpleTypeInfo ThisInfo = new AtomicSimpleTypeInfo(typeof(Day), TypeKind.Day, TypeKind.Day.ToSystemName(),
            SimpleType.ThisInfo, typeof(SDateTime), null);
    }
    #endregion
    [Serializable]
    public class ComplexType : Type {
        public ComplexType() { }
        public ComplexType(AttributeSet attributeSet) { AttributeSet = attributeSet; }
        public ComplexType(AttributeSet attributeSet, SimpleType simpleChild) : this(attributeSet) { SimpleChild = simpleChild; }
        public ComplexType(AttributeSet attributeSet, ChildContainer complexChild) : this(attributeSet) { ComplexChild = complexChild; }
        //
        private AttributeSet _attributeSet;
        public AttributeSet AttributeSet { get { return _attributeSet; } set { _attributeSet = SetParentTo(value); } }
        public AttributeSet GenericAttributeSet { get { return _attributeSet; } set { AttributeSet = value; } }
        public bool HasAttributes { get { return _attributeSet != null && _attributeSet.Count > 0; } }
        public T EnsureAttributeSet<T>(bool @try = false) where T : AttributeSet {
            var obj = _attributeSet as T;
            if (obj != null) return obj;
            var complexTypeInfo = ComplexTypeInfo;
            if (complexTypeInfo == null) {
                if (ExtensionMethods.IsAssignableTo(typeof(T), typeof(AttributeSet), @try)) obj = (T)new AttributeSet { Location = Location };
            }
            else {
                if (complexTypeInfo.AttributeSet == null) {
                    if (@try) return null;
                    throw new InvalidOperationException("Attribute set not allowed");
                }
                obj = complexTypeInfo.AttributeSet.CreateInstance<T>(Location, @try);
            }
            if (obj == null) return null;
            AttributeSet = obj;
            return obj;
        }
        public AttributeSet EnsureAttributeSet(bool @try = false) { return EnsureAttributeSet<AttributeSet>(@try); }
        public int TryAddDefaultAttributes(bool force = false) {
            var attributeSet = EnsureAttributeSet(true);
            if (attributeSet == null) return 0;
            return attributeSet.TryAddDefaultAttributes(force);
        }
        //
        private SimpleType _simpleChild;
        public SimpleType SimpleChild { get { return _simpleChild; } set { _simpleChild = SetParentTo(value); } }
        public SimpleType GenericSimpleChild { get { return _simpleChild; } set { SimpleChild = value; } }
        public bool HasSimpleChild { get { return _simpleChild != null; } }
        public T EnsureSimpleChild<T>(bool @try = false) where T : SimpleType {
            var obj = _simpleChild as T;
            if (obj != null) return obj;
            var complexTypeInfo = ComplexTypeInfo;
            if (complexTypeInfo == null) {
                if (ExtensionMethods.IsAssignableTo(typeof(T), typeof(SimpleType), @try)) obj = (T)new SimpleType { Location = Location };
            }
            else {
                if (complexTypeInfo.SimpleChild == null) {
                    if (@try) return null;
                    throw new InvalidOperationException("Simple child not allowed");
                }
                obj = complexTypeInfo.SimpleChild.CreateInstance<T>(Location, @try);
            }
            if (obj == null) return null;
            SimpleChild = obj;
            return obj;
        }
        public SimpleType EnsureSimpleChild(bool @try = false) { return EnsureSimpleChild<SimpleType>(@try); }
        public object Value {
            get { return _simpleChild == null ? null : _simpleChild.Value; }
            set { EnsureSimpleChild().Value = value; }
        }
        public object GenericValue { get { return Value; } set { Value = value; } }
        //
        private ChildContainer _complexChild;
        public ChildContainer ComplexChild { get { return _complexChild; } set { _complexChild = SetParentTo(value); } }
        public ChildContainer GenericComplexChild { get { return _complexChild; } set { ComplexChild = value; } }
        public bool HasComplexChild { get { return _complexChild != null && _complexChild.Count > 0; } }
        public T EnsureComplexChild<T>(bool @try = false) where T : ChildContainer {
            var obj = _complexChild as T;
            if (obj != null) return obj;
            var complexTypeInfo = ComplexTypeInfo;
            if (complexTypeInfo == null) {
                if (ExtensionMethods.IsAssignableTo(typeof(T), typeof(ChildContainer), @try)) obj = (T)new ChildContainer { Location = Location };
            }
            else {
                if (complexTypeInfo.ComplexChild == null) {
                    if (@try) return null;
                    throw new InvalidOperationException("Complex child not allowed");
                }
                obj = complexTypeInfo.ComplexChild.CreateInstance<T>(Location, @try);
            }
            if (obj == null) return null;
            ComplexChild = obj;
            return obj;
        }
        public ChildContainer EnsureComplexChild(bool @try = false) { return EnsureComplexChild<ChildContainer>(@try); }
        //
        public bool HasChildren { get { return HasSimpleChild || HasComplexChild; } }
        new public Element Parent { get { return (Element)base.Parent; } }
        private bool _isNull;
        public bool IsNull { get { return _isNull; } internal set { _isNull = value; } }
        internal bool HasChildrenEx(bool checkValue) { return _complexChild != null || HasSimpleChildEx(checkValue); }
        private bool HasSimpleChildEx(bool checkValue) {
            if (_simpleChild == null) return false;
            return checkValue ? !_simpleChild.IsValueNullOrEmptyString : true;
        }
        public override Object DeepClone() {
            var obj = (ComplexType)base.DeepClone();
            obj.AttributeSet = _attributeSet;
            obj.SimpleChild = _simpleChild;
            obj.ComplexChild = _complexChild;
            return obj;
        }
        public ComplexTypeInfo ComplexTypeInfo { get { return (ComplexTypeInfo)ObjectInfo; } }
        protected override sealed bool TryValidateCore(Context context) {
            if (!base.TryValidateCore(context)) return false;
            if (_simpleChild != null && _complexChild != null) {
                new Diagnostic(context, this, DiagnosticCode.CannotSetSimpleChildAndComplexChildBoth);
                return false;
            }
            var dMarker = context.MarkDiagnostics();
            var complexTypeInfo = ComplexTypeInfo;
            if (complexTypeInfo == null) {
                if (_attributeSet != null) _attributeSet.TryValidate(context);
                if (_simpleChild != null) _simpleChild.TryValidate(context);
                if (_complexChild != null) _complexChild.TryValidate(context);
            }
            else {
                var attributeSetInfo = complexTypeInfo.AttributeSet;
                if (attributeSetInfo == null) {
                    if (_attributeSet != null) new Diagnostic(context, this, DiagnosticCode.AttributeSetNotAllowed);
                }
                else {
                    if (_attributeSet == null) {
                        AttributeSet = attributeSetInfo.CreateInstance<AttributeSet>(Location);
                        _attributeSet.TryValidate(context);
                    }
                    else if (attributeSetInfo.IsAssignableFrom(_attributeSet, context)) _attributeSet.TryValidate(context);
                }
                //
                var simpleChildInfo = complexTypeInfo.SimpleChild;
                if (simpleChildInfo == null) {
                    if (_simpleChild != null) new Diagnostic(context, this, DiagnosticCode.SimpleChildNotAllowed);
                }
                else {
                    if (_simpleChild == null) {
                        if (!_isNull) new Diagnostic(context, this, DiagnosticCode.SimpleChildRequired);
                    }
                    else if (simpleChildInfo.IsAssignableFrom(_simpleChild, context)) _simpleChild.TryValidate(context);
                }
                //
                var complexChildInfo = complexTypeInfo.ComplexChild;
                if (complexChildInfo == null) {
                    if (_complexChild != null) new Diagnostic(context, this, DiagnosticCode.ComplexChildNotAllowed);
                }
                else {
                    if (_complexChild == null) {
                        if (!_isNull) {
                            ComplexChild = complexChildInfo.CreateInstance<ChildContainer>(Location);
                            _complexChild.TryValidate(context);
                        }
                    }
                    else if (complexChildInfo.IsAssignableFrom(_complexChild, context)) _complexChild.TryValidate(context);
                }
            }
            return !dMarker.HasErrors;
        }
        public bool TrySpecialize<T>(ComplexTypeInfo complexTypeInfo, bool isNull, DefaultOrFixedValueInfo dfValueInfo, Context context, out T result) where T : ComplexType {
            if (complexTypeInfo == null) throw new ArgumentNullException("complexTypeInfo");
            if (context == null) throw new ArgumentNullException("context");
            result = null;
            if (complexTypeInfo.IsAbstract) {
                new Diagnostic(context, this, DiagnosticCode.ComplexTypeDeclarationAbstract);
                return false;
            }
            if (_simpleChild != null && _complexChild != null) {
                new Diagnostic(context, this, DiagnosticCode.CannotSetSimpleChildAndComplexChildBoth);
                return false;
            }
            var dMarker = context.MarkDiagnostics();
            AttributeSet resultAttributeSet = null;
            var attributeSetInfo = complexTypeInfo.AttributeSet;
            if (attributeSetInfo == null) {
                if (_attributeSet != null) new Diagnostic(context, this, DiagnosticCode.AttributeSetNotAllowed);
            }
            else {
                var attributeSet = _attributeSet;
                if (attributeSet == null) attributeSet = new AttributeSet { Location = Location };
                attributeSet.TrySpecialize(attributeSetInfo, context, out resultAttributeSet);
            }
            //
            var checkInvalidSimpleChild = true;
            ChildContainer resultComplexChild = null;
            var complexChildInfo = complexTypeInfo.ComplexChild;
            if (complexChildInfo == null) {
                if (_complexChild != null) new Diagnostic(context, this, DiagnosticCode.ComplexChildNotAllowed);
            }
            else if (!isNull) {
                string mixedText = null;
                var complexChild = _complexChild;
                if (complexChild == null) {
                    if (_simpleChild != null && complexChildInfo.IsEffectiveOptional) {
                        var stringValue = _simpleChild.Value as string;
                        if (stringValue == null && !_simpleChild.HasValue) stringValue = "";
                        if (stringValue != null) {
                            var genComplexChild = false;
                            if (stringValue.Length == 0) {
                                genComplexChild = true;
                                if (dfValueInfo != null) mixedText = (string)dfValueInfo.Value;
                            }
                            else {
                                if (complexChildInfo.IsMixed) {
                                    mixedText = stringValue;
                                    checkInvalidSimpleChild = false;
                                    genComplexChild = true;
                                }
                                else if (complexChildInfo.HasElementBases && SimpleType.IsNullOrWhitespace(stringValue)) {
                                    checkInvalidSimpleChild = false;
                                    genComplexChild = true;
                                }
                            }
                            if (genComplexChild) complexChild = new ChildContainer { Location = _simpleChild.Location };
                        }
                    }
                }
                if (complexChild == null) new Diagnostic(context, this, DiagnosticCode.ComplexChildRequired);
                else {
                    if (!string.IsNullOrEmpty(mixedText) && complexChild.Count == 0) complexChild.Add(new Text(mixedText));
                    complexChild.TrySpecialize(complexChildInfo, context, out resultComplexChild);
                }
            }
            //
            SimpleType resultSimpleChild = null;
            var simpleChildInfo = complexTypeInfo.SimpleChild;
            if (simpleChildInfo == null) {
                if (_simpleChild != null && checkInvalidSimpleChild && !_simpleChild.IsValueNullOrEmptyString)
                    new Diagnostic(context, this, DiagnosticCode.SimpleChildNotAllowed);
            }
            else if (!isNull) {
                var simpleChild = _simpleChild;
                if (simpleChild == null) new Diagnostic(context, this, DiagnosticCode.SimpleChildRequired);
                else {
                    if (dfValueInfo != null && simpleChild.IsValueNullOrEmptyString) simpleChild.Value = dfValueInfo.Value;
                    simpleChild.TrySpecialize(simpleChildInfo, context, out resultSimpleChild);
                }
            }
            //
            if (dMarker.HasErrors) return false;
            var resultComplexType = complexTypeInfo.CreateInstance<T>(Location);
            resultComplexType.AttributeSet = resultAttributeSet;
            resultComplexType.SimpleChild = resultSimpleChild;
            resultComplexType.ComplexChild = resultComplexChild;
            resultComplexType._isNull = isNull;
            if (!resultComplexType.InvokeTryValidatePair(context)) return false;
            result = resultComplexType;
            return true;
        }
    }
    [Serializable]
    public class Attribute : Object, IEntityObject {
        protected Attribute() {
            var name = GetName();
            if (name == null) throw new InvalidOperationException("GetName() returns null");
            _name = name;
        }
        public Attribute(XName name) {
            if (name == null) throw new ArgumentNullException("name");
            _name = name;
        }
        public Attribute(XName name, SimpleType type) : this(name) { Type = type; }
        public Attribute(XName name, object value) : this(name) { Value = value; }
        //
        private readonly XName _name;
        public XName Name { get { return _name; } }
        public string LocalName { get { return _name.LocalName; } }
        protected virtual XName GetName() { throw new InvalidOperationException(); }
        private Attribute _referentialAttribute;
        public Attribute ReferentialAttribute {
            get { return _referentialAttribute; }
            set {
                if (value != null) {
                    if (value._name != _name) throw new InvalidOperationException("Referential attribute name '{0}' not equal to '{1}'".InvariantFormat(value._name, _name));
                    for (var i = value; i != null; i = i._referentialAttribute)
                        if (object.ReferenceEquals(this, i)) throw new InvalidOperationException("Circular reference detected");
                }
                _referentialAttribute = SetParentTo(value);
            }
        }
        public Attribute GenericReferentialAttribute {
            get { return _referentialAttribute; }
            set { ReferentialAttribute = value; }
        }
        public bool IsReference { get { return _referentialAttribute != null; } }
        public Attribute EffectiveAttribute { get { return _referentialAttribute == null ? this : _referentialAttribute.EffectiveAttribute; } }
        public override sealed Location? Location {
            get {
                if (_referentialAttribute != null) return _referentialAttribute.Location;
                return base.Location;
            }
            set {
                if (_referentialAttribute != null) _referentialAttribute.Location = value;
                else base.Location = value;
            }
        }
        private SimpleType _type;
        private void SetType(SimpleType type) { _type = SetParentTo(type); }
        public SimpleType Type {
            get { return EffectiveAttribute._type; }
            set {
                if (_referentialAttribute != null) _referentialAttribute.Type = value;
                else SetType(value);
            }
        }
        public override Object DeepClone() {
            var obj = (Attribute)base.DeepClone();
            obj.SetType(_type);
            obj.ReferentialAttribute = _referentialAttribute;
            return obj;
        }
        public bool HasType { get { return EffectiveAttribute._type != null; } }
        public SimpleType GenericType { get { return Type; } set { Type = value; } }
        Type IEntityObject.Type { get { return Type; } }
        public T EnsureType<T>(bool @try = false) where T : SimpleType {
            if (_referentialAttribute != null) return _referentialAttribute.EnsureType<T>(@try);
            var obj = _type as T;
            if (obj != null) return obj;
            var attributeInfo = AttributeInfo;
            if (attributeInfo == null) {
                if (ExtensionMethods.IsAssignableTo(typeof(T), typeof(SimpleType), @try)) obj = (T)new SimpleType { Location = Location };
            }
            else obj = attributeInfo.Type.CreateInstance<T>(Location, @try);
            if (obj == null) return null;
            Type = obj;
            return obj;
        }
        public SimpleType EnsureType() { return EnsureType<SimpleType>(); }
        public object Value {
            get {
                var type = Type;
                return type == null ? null : type.Value;
            }
            set { EnsureType().Value = value; }
        }
        public object GenericValue { get { return Value; } set { Value = value; } }
        public bool TrySetToDefaultValue(bool force = false) {
            var attributeInfo = AttributeInfo;
            if (attributeInfo != null && attributeInfo.DefaultOrFixedValue != null) {
                var type = EnsureType();
                if (attributeInfo.Type.IsAssignableFrom(type) && (!type.HasValue || force)) {
                    type.Value = attributeInfo.DefaultOrFixedValue.Value;
                    return true;
                }
            }
            return false;
        }
        public AttributeSet AttributeSetAncestor { get { return GetAncestor<AttributeSet>(); } }
        public Element ElementAncestor { get { return GetAncestor<Element>(); } }
        public AttributeInfo AttributeInfo { get { return (AttributeInfo)ObjectInfo; } }
        protected override bool TryValidating(Context context, bool fromValidate) {
            if (_referentialAttribute != null) return _referentialAttribute.TryValidating(context, fromValidate);
            return true;
        }
        protected override bool TryValidated(Context context, bool success) {
            if (_referentialAttribute != null) success = _referentialAttribute.TryValidated(context, success);
            return success;
        }
        protected override sealed bool TryValidateCore(Context context) {
            if (!base.TryValidateCore(context)) return false;
            var type = Type;
            var attributeInfo = AttributeInfo;
            if (attributeInfo == null) {
                if (type != null) return type.TryValidate(context);
                return true;
            }
            if (_name != attributeInfo.Name) {
                new Diagnostic(context, this, DiagnosticCode.InvalidAttributeName, _name, attributeInfo.Name);
                return false;
            }
            if (type == null) {
                new Diagnostic(context, this, DiagnosticCode.AttributeTypeRequired);
                return false;
            }
            if (!attributeInfo.Type.IsAssignableFrom(type, context)) return false;
            if (!type.TryValidate(context)) return false;
            return CheckFixedValue(type, attributeInfo.DefaultOrFixedValue, context);
        }
        private bool CheckFixedValue(SimpleType type, DefaultOrFixedValueInfo dfValueInfo, Context context) {
            if (dfValueInfo != null && dfValueInfo.IsFixed) {
                if (!SimpleType.ValueEquals(type.Value, dfValueInfo.Value)) {
                    new Diagnostic(context, this, DiagnosticCode.AttributeValueNotEqualToFixedValue, type.ToString(), dfValueInfo.ValueText);
                    return false;
                }
            }
            return true;
        }
        public bool TrySpecialize<T>(AttributeInfo attributeInfo, Context context, out T result) where T : Attribute {
            if (attributeInfo == null) throw new ArgumentNullException("attributeInfo");
            if (context == null) throw new ArgumentNullException("context");
            result = null;
            if (_name != attributeInfo.Name) {
                new Diagnostic(context, this, DiagnosticCode.InvalidAttributeName, _name, attributeInfo.Name);
                return false;
            }
            var type = Type;
            if (type == null) {
                new Diagnostic(context, this, DiagnosticCode.AttributeTypeRequired);
                return false;
            }
            SimpleType resultType;
            if (!type.TrySpecialize(attributeInfo.Type, context, out resultType)) return false;
            if (!CheckFixedValue(resultType, attributeInfo.DefaultOrFixedValue, context)) return false;
            var resultAttribute = attributeInfo.CreateInstance<T>(Location);
            if (attributeInfo.IsReference)
                resultAttribute.ReferentialAttribute = attributeInfo.ReferentialAttribute.CreateInstance<Attribute>(Location);
            resultAttribute.Type = resultType;
            if (!resultAttribute.InvokeTryValidatePair(context)) return false;
            result = resultAttribute;
            return true;
        }
    }
    [Serializable]
    public class AttributeSet : Object, ICollection<Attribute>, IReadOnlyCollection<Attribute> {
        public AttributeSet() { _attributeDict = new Dictionary<XName, Attribute>(); }
        public AttributeSet(IEnumerable<Attribute> attributes) : this() { AddRange(attributes); }
        //
        private Dictionary<XName, Attribute> _attributeDict;
        public int Count { get { return _attributeDict.Count; } }
        public bool Contains(XName name) { return _attributeDict.ContainsKey(name); }
        public bool Contains(Attribute attribute) {
            if (attribute == null) throw new ArgumentNullException("attribute");
            return Contains(attribute.Name);
        }
        public bool TryGet(XName name, out Attribute attribute) { return _attributeDict.TryGetValue(name, out attribute); }
        public Attribute TryGet(XName name) { return _attributeDict.TryGetValue(name); }
        public void Add(Attribute attribute) {
            if (attribute == null) throw new ArgumentNullException("attribute");
            _attributeDict.Add(attribute.Name, SetParentTo(attribute));
        }
        public void AddRange(IEnumerable<Attribute> attributes) {
            if (attributes == null) throw new ArgumentNullException("attributes");
            foreach (var attribute in attributes) Add(attribute);
        }
        public void AddOrSet(Attribute attribute) {
            if (attribute == null) throw new ArgumentNullException("attribute");
            _attributeDict[attribute.Name] = SetParentTo(attribute);
        }
        public bool Remove(XName name) { return _attributeDict.Remove(name); }
        public bool Remove(Attribute attribute) {
            if (attribute == null) throw new ArgumentNullException("attribute");
            return Remove(attribute.Name);
        }
        public void Clear() { _attributeDict.Clear(); }
        public IEnumerator<Attribute> GetEnumerator() { return _attributeDict.Values.GetEnumerator(); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }
        public void CopyTo(Attribute[] array, int arrayIndex) { _attributeDict.Values.CopyTo(array, arrayIndex); }
        bool ICollection<Attribute>.IsReadOnly { get { return false; } }
        //
        public IEnumerable<T> Attributes<T>(Func<T, bool> filter = null) where T : Attribute {
            foreach (var i in this) {
                var attribute = i as T;
                if (attribute != null)
                    if (filter == null || filter(attribute))
                        yield return attribute;
            }
        }
        public IEnumerable<T> Attributes<T>(XName name) where T : Attribute {
            var attribute = TryGet(name) as T;
            if (attribute != null) yield return attribute;
        }
        public IEnumerable<Attribute> WildcardAttributes {
            get {
                var attributeSetInfo = AttributeSetInfo;
                if (attributeSetInfo != null) {
                    var attributeDict = attributeSetInfo.AttributeDict;
                    foreach (var attribute in this) {
                        if (!attributeDict.ContainsKey(attribute.Name))
                            yield return attribute;
                    }
                }
            }
        }
        //
        protected T CreateAttribute<T>(XName name, bool @try = false) where T : Attribute {
            var attributeInfo = AttributeSetInfo.AttributeDict.TryGetValue(name);
            if (attributeInfo == null) {
                if (@try) return null;
                throw new InvalidOperationException("Attribute '{0}' not exists in the attribute set".InvariantFormat(name));
            }
            return attributeInfo.CreateInstance<T>(Location, @try);
        }
        public int TryAddDefaultAttributes(bool force = false) { return TryAddDefaultAttributes(null, true, force); }
        private int TryAddDefaultAttributes(AttributeSetInfo attributeSetInfo, bool genSpecificObject, bool force = false) {
            var count = 0;
            if (attributeSetInfo == null) attributeSetInfo = AttributeSetInfo;
            if (attributeSetInfo != null) {
                foreach (var attributeInfo in attributeSetInfo.Attributes) {
                    if (attributeInfo.DefaultOrFixedValue != null && (!Contains(attributeInfo.Name) || force)) {
                        var type = genSpecificObject ? attributeInfo.Type.CreateInstance<SimpleType>(Location) : new SimpleType { Location = Location };
                        type.Value = attributeInfo.DefaultOrFixedValue.Value;
                        var attribute = genSpecificObject ? attributeInfo.CreateInstance<Attribute>(Location) : new Attribute(attributeInfo.Name) { Location = Location };
                        if (genSpecificObject && attributeInfo.IsReference)
                            attribute.ReferentialAttribute = attributeInfo.ReferentialAttribute.CreateInstance<Attribute>(Location);
                        attribute.Type = type;
                        AddOrSet(attribute);
                        count++;
                    }
                }
            }
            return count;
        }
        new public ComplexType Parent { get { return (ComplexType)base.Parent; } }
        public override Object DeepClone() {
            var obj = (AttributeSet)base.DeepClone();
            obj._attributeDict = new Dictionary<XName, Attribute>();
            foreach (var attribute in this) obj.Add(attribute);
            return obj;
        }
        public AttributeSetInfo AttributeSetInfo { get { return (AttributeSetInfo)ObjectInfo; } }
        protected override sealed bool TryValidateCore(Context context) {
            if (!base.TryValidateCore(context)) return false;
            var dMarker = context.MarkDiagnostics();
            var attributeSetInfo = AttributeSetInfo;
            if (attributeSetInfo == null)
                foreach (var attribute in this) attribute.TryValidate(context);
            else {
                TryAddDefaultAttributes(attributeSetInfo, true);
                var attributeDict = new Dictionary<XName, Attribute>(_attributeDict);
                foreach (var attributeInfo in attributeSetInfo.Attributes) {
                    Attribute attribute;
                    if (attributeDict.TryGetValue(attributeInfo.Name, out attribute)) {
                        if (attributeInfo.IsAssignableFrom(attribute, context)) attribute.TryValidate(context);
                        attributeDict.Remove(attribute.Name);
                    }
                    else if (!attributeInfo.IsOptional)
                        new Diagnostic(context, this, DiagnosticCode.RequiredAttributeNotFound, attributeInfo.Name);
                }
                if (attributeDict.Count > 0) {
                    var wildcardInfo = attributeSetInfo.Wildcard;
                    if (wildcardInfo != null) {
                        foreach (var attribute in attributeDict.Values) {
                            if (wildcardInfo.IsMatch(attribute.Name.Namespace, context, this, DiagnosticCode.AttributeNamespaceUriNotMatchWithWildcard)) {
                                if (wildcardInfo.Validation == WildcardValidation.SkipValidate) attribute.TryValidate(context);
                                else {
                                    var globalAttributeInfo = wildcardInfo.Program.GetGlobalAttribute(attribute.Name);
                                    if (globalAttributeInfo != null) {
                                        if (globalAttributeInfo.IsAssignableFrom(attribute, context))
                                            attribute.TryValidate(context);
                                    }
                                    else {
                                        if (wildcardInfo.Validation == WildcardValidation.MustValidate && attribute.Name.IsQualified())
                                            new Diagnostic(context, this, DiagnosticCode.AttributeDeclarationNotFound, attribute.Name);
                                        else attribute.TryValidate(context);
                                    }
                                }
                            }
                        }
                    }
                    else {
                        foreach (var attribute in attributeDict.Values)
                            new Diagnostic(context, this, DiagnosticCode.AttributeDeclarationNotFound, attribute.Name);
                    }
                }
            }
            return !dMarker.HasErrors;
        }
        public bool TrySpecialize<T>(AttributeSetInfo attributeSetInfo, Context context, out T result) where T : AttributeSet {
            if (attributeSetInfo == null) throw new ArgumentNullException("attributeSetInfo");
            if (context == null) throw new ArgumentNullException("context");
            result = null;
            var dMarker = context.MarkDiagnostics();
            TryAddDefaultAttributes(attributeSetInfo, false);
            var resultAttributeSet = attributeSetInfo.CreateInstance<T>(Location);
            var attributeDict = new Dictionary<XName, Attribute>(_attributeDict);
            foreach (var attributeInfo in attributeSetInfo.Attributes) {
                Attribute attribute;
                if (attributeDict.TryGetValue(attributeInfo.Name, out attribute)) {
                    Attribute resultAttribute;
                    if (attribute.TrySpecialize(attributeInfo, context, out resultAttribute))
                        resultAttributeSet.Add(resultAttribute);
                    attributeDict.Remove(attribute.Name);
                }
                else if (!attributeInfo.IsOptional)
                    new Diagnostic(context, this, DiagnosticCode.RequiredAttributeNotFound, attributeInfo.Name);
            }
            if (attributeDict.Count > 0) {
                var wildcardInfo = attributeSetInfo.Wildcard;
                if (wildcardInfo != null) {
                    foreach (var attribute in attributeDict.Values) {
                        if (wildcardInfo.IsMatch(attribute.Name.Namespace, context, this, DiagnosticCode.AttributeNamespaceUriNotMatchWithWildcard)) {
                            if (wildcardInfo.Validation == WildcardValidation.SkipValidate) {
                                if (attribute.TryValidate(context)) resultAttributeSet.Add(attribute);
                            }
                            else {
                                var globalAttributeInfo = wildcardInfo.Program.GetGlobalAttribute(attribute.Name);
                                if (globalAttributeInfo != null) {
                                    Attribute resultAttribute;
                                    if (attribute.TrySpecialize(globalAttributeInfo, context, out resultAttribute))
                                        resultAttributeSet.Add(resultAttribute);
                                }
                                else {
                                    if (wildcardInfo.Validation == WildcardValidation.MustValidate && attribute.Name.IsQualified())
                                        new Diagnostic(context, this, DiagnosticCode.AttributeDeclarationNotFound, attribute.Name);
                                    else if (attribute.TryValidate(context)) resultAttributeSet.Add(attribute);
                                }
                            }
                        }
                    }
                }
                else {
                    foreach (var attribute in attributeDict.Values)
                        new Diagnostic(context, this, DiagnosticCode.AttributeDeclarationNotFound, attribute.Name);
                }
            }
            if (dMarker.HasErrors) return false;
            if (!resultAttributeSet.InvokeTryValidatePair(context)) return false;
            result = resultAttributeSet;
            return true;
        }
    }
    [Serializable]
    public abstract class Child : Object {
        protected Child() { }
        public ChildContainer ChildContainerAncestor { get { return GetAncestor<ChildContainer>(); } }
        public Element ElementAncestor { get { return GetAncestor<Element>(); } }
        public IEnumerable<T> ElementAncestors<T>(Func<Element, bool> filter = null) where T : Element {
            for (var obj = base.Parent; obj != null; obj = obj.Parent) {
                var element = obj as T;
                if (element != null)
                    if (filter == null || filter(element))
                        yield return element;
            }
        }
        public IEnumerable<T> ElementAncestors<T>(XName name) where T : Element { return ElementAncestors<T>(i => i.Name == name); }
        public virtual int ChildOrder { get { return -1; } }
        public virtual int SpecifiedOrder { get { return ChildOrder; } set { throw new InvalidOperationException("For unordered child struct member only"); } }
    }
    [Serializable]
    public abstract class ContentChild : Child {
        protected ContentChild() { }
    }
    public enum IdentityConstraintKind { Key, Unique, KeyRef }
    [Serializable]
    public sealed class IdentityConstraint {
        internal IdentityConstraint(Element containingElement, XName name, IdentityConstraintKind kind, bool isSingleValue,
            IReadOnlyDictionary<object, IdentityValue> keys, IReadOnlyDictionary<IReadOnlyList<object>, IdentityValues> multipleValueKeys,
            IReadOnlyList<KeyRefIdentityValue> keyRefs, IReadOnlyList<KeyRefIdentityValues> multipleValueKeyRefs, IdentityConstraint referentialConstraint) {
            _containingElement = containingElement;
            _name = name;
            _kind = kind;
            _isSingleValue = isSingleValue;
            _keys = keys;
            _multipleValueKeys = multipleValueKeys;
            _keyRefs = keyRefs;
            _multipleValueKeyRefs = multipleValueKeyRefs;
            _referentialConstraint = referentialConstraint;
        }
        private readonly Element _containingElement;
        public Element ContainingElement { get { return _containingElement; } }
        private readonly XName _name;
        public XName Name { get { return _name; } }//IdentityConstraintInfo.Name
        private readonly IdentityConstraintKind _kind;
        public IdentityConstraintKind Kind { get { return _kind; } }
        public bool IsKey { get { return _kind == IdentityConstraintKind.Key; } }
        public bool IsUnique { get { return _kind == IdentityConstraintKind.Unique; } }
        public bool IsKeyRef { get { return _kind == IdentityConstraintKind.KeyRef; } }
        private readonly bool _isSingleValue;
        public bool IsSingleValue { get { return _isSingleValue; } }
        private readonly IReadOnlyDictionary<object, IdentityValue> _keys;
        public IReadOnlyDictionary<object, IdentityValue> Keys { get { return _keys; } }//single value Key or Unique
        public bool HasKeys { get { return _keys != null && _keys.Count > 0; } }
        private readonly IReadOnlyDictionary<IReadOnlyList<object>, IdentityValues> _multipleValueKeys;
        public IReadOnlyDictionary<IReadOnlyList<object>, IdentityValues> MultipleValueKeys { get { return _multipleValueKeys; } }//multiple value Key or Unique
        public bool HasMultipleValueKeys { get { return _multipleValueKeys != null && _multipleValueKeys.Count > 0; } }
        private readonly IReadOnlyList<KeyRefIdentityValue> _keyRefs;
        public IReadOnlyList<KeyRefIdentityValue> KeyRefs { get { return _keyRefs; } }//single value KeyRef
        public bool HasKeyRefs { get { return _keyRefs != null && _keyRefs.Count > 0; } }
        private readonly IReadOnlyList<KeyRefIdentityValues> _multipleValueKeyRefs;
        public IReadOnlyList<KeyRefIdentityValues> MultipleValueKeyRefs { get { return _multipleValueKeyRefs; } }//multiple value KeyRef
        public bool HasMultipleValueKeyRefs { get { return _multipleValueKeyRefs != null && _multipleValueKeyRefs.Count > 0; } }
        private readonly IdentityConstraint _referentialConstraint;
        public IdentityConstraint ReferentialConstraint { get { return _referentialConstraint; } }//for KeyRef
    }
    [Serializable]
    public struct IdentityValue {
        public IdentityValue(object value, Element identityElement, IEntityObject valueEntityObject) {
            _value = value;
            _identityElement = identityElement;
            _valueEntityObject = valueEntityObject;
        }
        private readonly object _value;
        public object Value { get { return _value; } }
        private readonly Element _identityElement;
        public Element IdentityElement { get { return _identityElement; } }
        private readonly IEntityObject _valueEntityObject;
        public IEntityObject ValueEntityObject { get { return _valueEntityObject; } }
    }
    [Serializable]
    public struct IdentityValues {
        public IdentityValues(IReadOnlyList<object> values, Element identityElement, IReadOnlyList<IEntityObject> valueEntityObjects) {
            _values = values;
            _identityElement = identityElement;
            _valueEntityObjects = valueEntityObjects;
        }
        private readonly IReadOnlyList<object> _values;
        public IReadOnlyList<object> Values { get { return _values; } }
        private readonly Element _identityElement;
        public Element IdentityElement { get { return _identityElement; } }
        private readonly IReadOnlyList<IEntityObject> _valueEntityObjects;
        public IReadOnlyList<IEntityObject> ValueEntityObjects { get { return _valueEntityObjects; } }
    }
    [Serializable]
    public struct KeyRefIdentityValue {
        public KeyRefIdentityValue(IdentityValue referenceIdentityValue, IdentityValue referentialIdentityValue) {
            _referenceIdentityValue = referenceIdentityValue;
            _referentialIdentityValue = referentialIdentityValue;
        }
        private readonly IdentityValue _referenceIdentityValue;
        public IdentityValue ReferenceIdentityValue { get { return _referenceIdentityValue; } }
        private readonly IdentityValue _referentialIdentityValue;
        public IdentityValue ReferentialIdentityValue { get { return _referentialIdentityValue; } }
    }
    [Serializable]
    public struct KeyRefIdentityValues {
        public KeyRefIdentityValues(IdentityValues referenceIdentityValues, IdentityValues referentialIdentityValues) {
            _referenceIdentityValues = referenceIdentityValues;
            _referentialIdentityValues = referentialIdentityValues;
        }
        private readonly IdentityValues _referenceIdentityValues;
        public IdentityValues ReferenceIdentityValues { get { return _referenceIdentityValues; } }
        private readonly IdentityValues _referentialIdentityValues;
        public IdentityValues ReferentialIdentityValues { get { return _referentialIdentityValues; } }
    }

    [Serializable]
    public class Element : ContentChild, IEntityObject {
        public Element() { _name = GetName(); }
        public Element(XName name) { _name = name; }
        public Element(XName name, Type type) : this(name) { Type = type; }
        //
        private Element _referentialElement;
        public Element ReferentialElement {
            get { return _referentialElement; }
            set {
                if (value != null) {
                    for (var i = value; i != null; i = i._referentialElement)
                        if (object.ReferenceEquals(this, i)) throw new InvalidOperationException("Circular reference detected");
                }
                _referentialElement = SetParentTo(value);
            }
        }
        public Element GenericReferentialElement {
            get { return _referentialElement; }
            set { ReferentialElement = value; }
        }
        public bool IsReference { get { return _referentialElement != null; } }
        public Element EffectiveElement { get { return _referentialElement == null ? this : _referentialElement.EffectiveElement; } }
        public override sealed Location? Location {
            get {
                if (_referentialElement != null) return _referentialElement.Location;
                return base.Location;
            }
            set {
                if (_referentialElement != null) _referentialElement.Location = value;
                else base.Location = value;
            }
        }
        private XName _name;
        public XName Name {
            get { return EffectiveElement._name; }
            set { EffectiveElement._name = value; }
        }
        public string LocalName {
            get {
                var name = Name;
                return name != null ? name.LocalName : null;
            }
        }
        protected virtual XName GetName() { return null; }
        private bool _isNull;
        public bool IsNull {
            get { return EffectiveElement._isNull; }
            set { EffectiveElement._isNull = value; }
        }
        //begin for generic element
        private XName _typeName;
        public XName TypeName {
            get { return EffectiveElement._typeName; }
            set { EffectiveElement._typeName = value; }
        }
        private Dictionary<string, string> _namespaceUriDict;//key:prefix, value:namespace uri
        public IReadOnlyDictionary<string, string> NamespaceUris { get { return EffectiveElement._namespaceUriDict; } }
        public string TryGetNamespaceUri(string prefix) {
            if (prefix == null) prefix = "";
            string uriValue;
            if (_namespaceUriDict != null && _namespaceUriDict.TryGetValue(prefix, out uriValue)) return uriValue;
            var elementAncestor = ElementAncestor;
            if (elementAncestor != null) return elementAncestor.TryGetNamespaceUri(prefix);
            if (prefix == "") return "";
            return null;
        }
        //end for generic element
        private Type _type;
        private void SetType(Type type) { _type = SetParentTo(type); }
        public Type Type {
            get { return EffectiveElement._type; }
            set {
                if (_referentialElement != null) _referentialElement.Type = value;
                else SetType(value);
            }
        }
        //
        private Dictionary<XName, IdentityConstraint> _identityConstraintDict;
        public IReadOnlyDictionary<XName, IdentityConstraint> IdentityConstraints { get { return EffectiveElement._identityConstraintDict; } }
        public IdentityConstraint TryGetIdentityConstraint(XName name) {
            var ics = IdentityConstraints;
            if (ics != null) return ics.TryGetValue(name);
            return null;
        }
        public bool HasIdentityConstraints {
            get {
                var ics = IdentityConstraints;
                return ics != null && ics.Count > 0;
            }
        }
        private Dictionary<object, Id> _idDict;
        public IReadOnlyDictionary<object, Id> Ids { get { return EffectiveElement._idDict; } }
        public bool HasIds {
            get {
                var ids = Ids;
                return ids != null && ids.Count > 0;
            }
        }
        private List<IIdRefObject> _idRefList;
        public IReadOnlyList<IIdRefObject> IdRefs { get { return EffectiveElement._idRefList; } }
        public bool HasIdRefs {
            get {
                var idrefs = IdRefs;
                return idrefs != null && idrefs.Count > 0;
            }
        }
        private ElementInfo _resolvedElementInfo;
        public ElementInfo ResolvedElementInfo { get { return EffectiveElement._resolvedElementInfo; } }
        //
        public override Object DeepClone() {
            var obj = (Element)base.DeepClone();
            obj.SetType(_type);
            obj.ReferentialElement = _referentialElement;
            return obj;
        }
        public bool HasType { get { return EffectiveElement._type != null; } }
        public Type GenericType { get { return Type; } set { Type = value; } }
        public T EnsureType<T>(bool @try = false) where T : Type { return EnsureType<T>(null, @try); }
        public Type EnsureType(bool @try = false) { return EnsureType<Type>(@try); }
        private T EnsureType<T>(ElementInfo elementInfo, bool @try = false) where T : Type {
            if (_referentialElement != null) return _referentialElement.EnsureType<T>(elementInfo, @try);
            var obj = _type as T;
            if (obj != null) return obj;
            if (elementInfo == null) elementInfo = ElementBaseInfo as ElementInfo;
            if (elementInfo == null || elementInfo.Type == Type.ThisInfo) {
                if (ExtensionMethods.IsAssignableTo(typeof(T), typeof(ComplexType), @try)) obj = (T)(Type)new ComplexType();
            }
            else obj = elementInfo.Type.CreateInstance<T>(Location, @try);
            if (obj == null) return null;
            Type = obj;
            return obj;
        }
        public ComplexType ComplexType { get { return Type as ComplexType; } set { Type = value; } }
        public SimpleType SimpleType { get { return Type as SimpleType; } set { Type = value; } }
        //
        public AttributeSet AttributeSet {
            get {
                var complexType = ComplexType;
                return complexType == null ? null : complexType.AttributeSet;
            }
            set { EnsureType<ComplexType>().AttributeSet = value; }
        }
        public AttributeSet GenericAttributeSet { get { return AttributeSet; } set { AttributeSet = value; } }
        public T EnsureAttributeSet<T>(bool @try = false) where T : AttributeSet {
            var complexType = EnsureType<ComplexType>(@try);
            if (complexType == null) return null;
            return complexType.EnsureAttributeSet<T>(@try);
        }
        public AttributeSet EnsureAttributeSet(bool @try = false) { return EnsureAttributeSet<AttributeSet>(@try); }
        public bool HasAttributes {
            get {
                var complexType = ComplexType;
                return complexType != null && complexType.HasAttributes;
            }
        }
        public int TryAddDefaultAttributes(bool force = false) {
            var complexType = EnsureType<ComplexType>(true);
            if (complexType == null) return 0;
            return complexType.TryAddDefaultAttributes(force);
        }
        public IEnumerable<T> Attributes<T>(Func<T, bool> filter = null) where T : Attribute {
            var attributeSet = AttributeSet;
            if (attributeSet == null) return Enumerable.Empty<T>();
            return attributeSet.Attributes<T>(filter);
        }
        public IEnumerable<T> Attributes<T>(XName name) where T : Attribute {
            var attributeSet = AttributeSet;
            if (attributeSet == null) return Enumerable.Empty<T>();
            return attributeSet.Attributes<T>(name);
        }
        //
        public bool HasChildren {
            get {
                var type = Type;
                if (type == null) return false;
                var complexType = type as ComplexType;
                if (complexType != null) return complexType.HasChildren;
                return true;
            }
        }
        public object Value {
            get {
                var type = Type;
                if (type == null) return null;
                var simpleType = type as SimpleType;
                if (simpleType != null) return simpleType.Value;
                var simpleChild = ((ComplexType)type).SimpleChild;
                return simpleChild != null ? simpleChild.Value : null;
            }
        }
        public bool TrySetToDefaultValue(bool force = false) { return TrySetToDefaultValue(null, force); }
        private bool TrySetToDefaultValue(ElementInfo elementInfo, bool force = false) {
            if (elementInfo == null) elementInfo = ElementBaseInfo as ElementInfo;
            if (elementInfo != null) {
                var dfValueInfo = elementInfo.DefaultOrFixedValue;
                if (dfValueInfo != null) {
                    var type = EnsureType<Type>(elementInfo, true);
                    var typeInfo = elementInfo.Type;
                    if (type != null && typeInfo.IsAssignableFrom(type)) {
                        var simpleType = type as SimpleType;
                        if (simpleType != null) {
                            if (!simpleType.HasValue || force) {
                                simpleType.Value = dfValueInfo.Value;
                                return true;
                            }
                        }
                        else {
                            var complexType = (ComplexType)type;
                            var complexTypeInfo = (ComplexTypeInfo)typeInfo;
                            if (complexTypeInfo.SimpleChild != null) {
                                var simpleChild = complexType.EnsureSimpleChild(true);
                                if (simpleChild != null && complexTypeInfo.SimpleChild.IsAssignableFrom(simpleChild)) {
                                    if (!simpleChild.HasValue || force) {
                                        simpleChild.Value = dfValueInfo.Value;
                                        return true;
                                    }
                                }
                            }
                            else if (complexTypeInfo.ComplexChild != null) {
                                var text = (string)dfValueInfo.Value;
                                if (text.Length > 0) {
                                    var complexChild = complexType.EnsureComplexChild(true);
                                    if (complexChild != null && complexTypeInfo.ComplexChild.IsAssignableFrom(complexChild)) {
                                        if (complexChild.Count == 0 || force) {
                                            complexChild.Clear();
                                            complexChild.Add(new Text(text));
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }
        //
        public SimpleType SimpleChild {
            get {
                var complexType = ComplexType;
                return complexType == null ? null : complexType.SimpleChild;
            }
            set { EnsureType<ComplexType>().SimpleChild = value; }
        }
        public SimpleType GenericSimpleChild { get { return SimpleChild; } set { SimpleChild = value; } }
        public T EnsureSimpleChild<T>(bool @try = false) where T : SimpleType {
            var complexType = EnsureType<ComplexType>(@try);
            if (complexType == null) return null;
            return complexType.EnsureSimpleChild<T>(@try);
        }
        public SimpleType EnsureSimpleChild(bool @try = false) { return EnsureSimpleChild<SimpleType>(@try); }
        public bool HasSimpleChild {
            get {
                var complexType = ComplexType;
                return complexType != null && complexType.HasSimpleChild;
            }
        }
        //
        public ChildContainer ComplexChild {
            get {
                var complexType = ComplexType;
                return complexType == null ? null : complexType.ComplexChild;
            }
            set { EnsureType<ComplexType>().ComplexChild = value; }
        }
        public ChildContainer GenericComplexChild { get { return ComplexChild; } set { ComplexChild = value; } }
        public T EnsureComplexChild<T>(bool @try = false) where T : ChildContainer {
            var complexType = EnsureType<ComplexType>(@try);
            if (complexType == null) return null;
            return complexType.EnsureComplexChild<T>(@try);
        }
        public ChildContainer EnsureComplexChild(bool @try = false) { return EnsureComplexChild<ChildContainer>(@try); }
        public bool HasComplexChild {
            get {
                var complexType = ComplexType;
                return complexType != null && complexType.HasComplexChild;
            }
        }
        public IEnumerable<T> ContentChildren<T>(Func<T, bool> filter = null) where T : ContentChild {
            var complexChild = ComplexChild;
            if (complexChild != null) return complexChild.ContentChildren<T>(filter);
            return Enumerable.Empty<T>();
        }
        public IEnumerable<T> ContentDescendants<T>(Func<T, bool> filter = null) where T : ContentChild {
            var complexChild = ComplexChild;
            if (complexChild != null) return complexChild.ContentDescendants<T>(filter);
            return Enumerable.Empty<T>();
        }
        public IEnumerable<T> SelfAndContentDescendants<T>(Func<T, bool> filter = null) where T : ContentChild {
            var contentChild = this as T;
            if (contentChild != null)
                if (filter == null || filter(contentChild))
                    yield return contentChild;
            foreach (var i in ContentDescendants<T>(filter)) yield return i;
        }
        public IEnumerable<T> ElementChildren<T>(Func<T, bool> filter = null) where T : Element {
            var complexChild = ComplexChild;
            if (complexChild != null) return complexChild.ElementChildren<T>(filter);
            return Enumerable.Empty<T>();
        }
        public IEnumerable<T> ElementDescendants<T>(Func<T, bool> filter = null) where T : Element {
            var complexChild = ComplexChild;
            if (complexChild != null) return complexChild.ElementDescendants<T>(filter);
            return Enumerable.Empty<T>();
        }
        public IEnumerable<T> SelfAndElementDescendants<T>(Func<T, bool> filter = null) where T : Element {
            var element = this as T;
            if (element != null)
                if (filter == null || filter(element))
                    yield return element;
            foreach (var i in ElementDescendants<T>(filter)) yield return i;
        }
        public IEnumerable<T> ElementChildren<T>(XName name) where T : Element {
            var complexChild = ComplexChild;
            if (complexChild != null) return complexChild.ElementChildren<T>(name);
            return Enumerable.Empty<T>();
        }
        public IEnumerable<T> ElementDescendants<T>(XName name) where T : Element {
            var complexChild = ComplexChild;
            if (complexChild != null) return complexChild.ElementDescendants<T>(name);
            return Enumerable.Empty<T>();
        }
        public IEnumerable<T> SelfAndElementDescendants<T>(XName name) where T : Element { return SelfAndElementDescendants<T>(i => i.Name == name); }
        public IEnumerable<T> SelfAndElementAncestors<T>(Func<Element, bool> filter = null) where T : Element {
            var element = this as T;
            if (element != null)
                if (filter == null || filter(element))
                    yield return element;
            foreach (var i in ElementAncestors<T>(filter)) yield return i;
        }
        public IEnumerable<T> SelfAndElementAncestors<T>(XName name) where T : Element { return SelfAndElementAncestors<T>(i => i.Name == name); }
        //
        public ElementBaseInfo ElementBaseInfo { get { return (ElementBaseInfo)ObjectInfo; } }
        protected override bool TryValidating(Context context, bool fromValidate) {
            if (_referentialElement != null) return _referentialElement.TryValidating(context, fromValidate);
            return true;
        }
        protected override bool TryValidated(Context context, bool success) {
            if (_referentialElement != null) success = _referentialElement.TryValidated(context, success);
            return success;
        }
        protected override sealed bool TryValidateCore(Context context) {
            if (!base.TryValidateCore(context)) return false;
            var name = Name;
            if (name == null) {
                new Diagnostic(context, this, DiagnosticCode.ElementNameRequired);
                return false;
            }
            ElementInfo elementInfo = null;
            var elementBaseInfo = ElementBaseInfo;
            if (elementBaseInfo == null) {
                if (!GenericValidate(context, false)) return false;
            }
            else {
                var declElementInfo = elementBaseInfo as ElementInfo;
                if (declElementInfo != null) {
                    elementInfo = declElementInfo.TryGet(name);
                    if (elementInfo == null) {
                        new Diagnostic(context, this, DiagnosticCode.ElementDeclarationNotFound, name);
                        return false;
                    }
                    if (declElementInfo != elementInfo && declElementInfo.InstanceProhibition.IsSet(InstanceProhibition.Substitution)) {
                        new Diagnostic(context, this, DiagnosticCode.SubstitutionInstanceProhibited);
                        return false;
                    }
                    if (!SpecificValidate(elementInfo, context, declElementInfo.InstanceProhibition)) return false;
                }
                else {
                    var wildcardInfo = ((ElementWildcardInfo)elementBaseInfo).Wildcard;
                    if (!wildcardInfo.IsMatch(name.Namespace, context, this, DiagnosticCode.ElementNamespaceUriNotMatchWithWildcard)) return false;
                    if (wildcardInfo.Validation == WildcardValidation.SkipValidate) {
                        if (!GenericValidate(context, false)) return false;
                    }
                    else {
                        elementInfo = wildcardInfo.Program.GetGlobalElement(name);
                        if (elementInfo != null) {
                            if (!SpecificValidate(elementInfo, context, InstanceProhibition.None)) return false;
                        }
                        else {
                            if (wildcardInfo.Validation == WildcardValidation.MustValidate && name.IsQualified()) {
                                new Diagnostic(context, this, DiagnosticCode.ElementDeclarationNotFound, name);
                                return false;
                            }
                            if (!GenericValidate(context, false)) return false;
                        }
                    }
                }
            }
            var effectiveElement = EffectiveElement;
            if (!HasParent && !context.TryGetIdDictAndIdRefList(out effectiveElement._idDict, out effectiveElement._idRefList)) return false;
            effectiveElement._resolvedElementInfo = elementInfo;
            return true;
        }
        private bool GenericValidate(Context context, bool checkValue) {
            var type = Type;
            if (IsNull && HasChildrenEx(type, checkValue)) {
                new Diagnostic(context, this, DiagnosticCode.NullElementCannotHasChildren);
                return false;
            }
            if (type != null) return type.TryValidate(context);
            return true;
        }
        private static bool HasChildrenEx(Type type, bool checkValue) {
            if (type == null) return false;
            var complexType = type as ComplexType;
            if (complexType != null) return complexType.HasChildrenEx(checkValue);
            if (checkValue) return !((SimpleType)type).IsValueNullOrEmptyString;
            return true;
        }
        private bool SpecificValidate(ElementInfo elementInfo, Context context, InstanceProhibition typeInstanceProhibition) {
            if (elementInfo.IsAbstract) {
                new Diagnostic(context, this, DiagnosticCode.ElememntDeclarationAbstract);
                return false;
            }
            if (TypeName != null) {
                new Diagnostic(context, this, DiagnosticCode.TypeNameNotAllowedForSpecificElement);
                return false;
            }
            var isNull = IsNull;
            var type = Type;
            var declTypeInfo = elementInfo.Type;
            var declComplexTypeInfo = declTypeInfo as ComplexTypeInfo;
            if (isNull) {
                if (!elementInfo.IsNullable) {
                    new Diagnostic(context, this, DiagnosticCode.ElementDeclarationNotNullable);
                    return false;
                }
                else if (HasChildrenEx(type, false)) {
                    new Diagnostic(context, this, DiagnosticCode.NullElementCannotHasChildren);
                    return false;
                }
                if (type == null && declComplexTypeInfo != null && declComplexTypeInfo.AttributeSet != null && !declComplexTypeInfo.AttributeSet.IsOptional) {
                    new Diagnostic(context, this, DiagnosticCode.ElementTypeRequired);
                    return false;
                }
                //var dfValueInfo = elementInfo.DefaultOrFixedValue;
                //if (dfValueInfo != null && dfValueInfo.IsFixed) {
                //    new Diagnostic(context, this, DiagnosticCode.ElementDeclarationHasFixedValueCannotBeNull);
                //    return false;
                //}
            }
            else {
                if (type == null) {
                    new Diagnostic(context, this, DiagnosticCode.ElementTypeRequired);
                    return false;
                }
            }
            if (type != null) {
                if (!declTypeInfo.IsAssignableFrom(type, context)) return false;
                if (declComplexTypeInfo != null) {
                    if (!CheckTypeInstanceProhibition((ComplexTypeInfo)type.TypeInfo, declComplexTypeInfo,
                        typeInstanceProhibition | declComplexTypeInfo.InstanceProhibition, context))
                        return false;
                }
                var complexType = type as ComplexType;
                if (complexType != null) complexType.IsNull = isNull;
                if (!type.TryValidate(context)) return false;
                if (!isNull && !CheckFixedValue(type, elementInfo, context)) return false;
            }
            return TryValidateIdentityConstraints(elementInfo, context);
        }
        private bool CheckTypeInstanceProhibition(ComplexTypeInfo instanceInfo, ComplexTypeInfo declInfo, InstanceProhibition instanceProhibition, Context context) {
            if (instanceProhibition.IsSet(InstanceProhibition.Extension | InstanceProhibition.Restriction)) {
                for (; instanceInfo != declInfo; instanceInfo = (ComplexTypeInfo)instanceInfo.BaseType) {
                    if (instanceInfo.IsExtension) {
                        if (instanceProhibition.IsSet(InstanceProhibition.Extension)) {
                            new Diagnostic(context, this, DiagnosticCode.ExtensionInstanceProhibited);
                            return false;
                        }
                    }
                    else if (instanceProhibition.IsSet(InstanceProhibition.Restriction)) {
                        new Diagnostic(context, this, DiagnosticCode.RestrictionInstanceProhibited);
                        return false;
                    }
                }
            }
            return true;
        }
        private bool CheckFixedValue(Type type, ElementInfo elementInfo, Context context) {
            var dfValueInfo = elementInfo.DefaultOrFixedValue;
            if (dfValueInfo != null && dfValueInfo.IsFixed) {
                var simpleType = type as SimpleType;
                if (simpleType != null) {
                    if (!SimpleType.ValueEquals(simpleType.Value, dfValueInfo.Value)) {
                        new Diagnostic(context, this, DiagnosticCode.ElementValueNotEqualToFixedValue, simpleType.ToString(), dfValueInfo.ValueText);
                        return false;
                    }
                }
                else {
                    var complexType = (ComplexType)type;
                    var complexTypeInfo = (ComplexTypeInfo)elementInfo.Type;
                    if (complexTypeInfo.SimpleChild != null) {
                        var simpleChild = complexType.SimpleChild;
                        if (!SimpleType.ValueEquals(simpleChild.Value, dfValueInfo.Value)) {
                            new Diagnostic(context, this, DiagnosticCode.ElementValueNotEqualToFixedValue, simpleChild.ToString(), dfValueInfo.ValueText);
                            return false;
                        }
                    }
                    else {
                        var complexChild = complexType.ComplexChild;
                        string textValue = null;
                        foreach (var contentChild in complexChild.ContentChildren<ContentChild>()) {
                            var text = contentChild as Text;
                            if (text == null) {
                                new Diagnostic(context, this, DiagnosticCode.OnlyTextChildAllowedIfElementDeclarationHasFixedValue);
                                return false;
                            }
                            textValue = textValue == null ? text.Value : textValue + text.Value;
                        }
                        if (textValue == null) textValue = "";
                        if (!SimpleType.ValueEquals(textValue, dfValueInfo.Value)) {
                            new Diagnostic(context, this, DiagnosticCode.ElementValueNotEqualToFixedValue, textValue, dfValueInfo.ValueText);
                            return false;
                        }
                    }
                }
            }
            return true;
        }
        private bool TryValidateIdentityConstraints(ElementInfo elementInfo, Context context) {
            var effectiveElement = EffectiveElement;
            effectiveElement._identityConstraintDict = null;
            var icInfos = elementInfo.IdentityConstraints;
            if (icInfos != null) {
                foreach (var icInfo in icInfos) {
                    IdentityConstraint ic;
                    if (!TryValidateIdentityConstraint(effectiveElement, icInfo, context, out ic)) return false;
                    if (effectiveElement._identityConstraintDict == null) effectiveElement._identityConstraintDict = new Dictionary<XName, IdentityConstraint>();
                    effectiveElement._identityConstraintDict.Add(icInfo.Name, ic);
                }
            }
            return true;
        }
        private IdentityConstraint TryGetIdentityConstraintFromAll(XName name) {
            var ics = IdentityConstraints;
            if (ics != null) {
                var ic = ics.TryGetValue(name);
                if (ic != null) return ic;
            }
            foreach (var childElement in ElementChildren<Element>()) {
                var ic = childElement.TryGetIdentityConstraintFromAll(name);
                if (ic != null) return ic;
            }
            return null;
        }
        private bool TryValidateIdentityConstraint(Element effectiveElement, IdentityConstraintInfo icInfo, Context context, out IdentityConstraint result) {
            result = null;
            IdentityConstraint referentialIC = null;
            var kind = icInfo.Kind;
            var isRef = kind == IdentityConstraintKind.KeyRef;
            if (isRef) {
                referentialIC = TryGetIdentityConstraintFromAll(icInfo.ReferentialName);
                if (referentialIC == null) {
                    new Diagnostic(context, this, DiagnosticCode.ReferentialIdentityConstraintNotFound, icInfo.ReferentialName);
                    return false;
                }
            }
            var valueCount = icInfo.ValuePathExpressions.Count;
            var isSingleValue = valueCount == 1;
            Dictionary<object, IdentityValue> keys = null;
            Dictionary<IReadOnlyList<object>, IdentityValues> multipleValueKeys = null;
            List<KeyRefIdentityValue> keyRefs = null;
            List<KeyRefIdentityValues> multipleValueKeyRefs = null;
            if (isRef) {
                if (isSingleValue) keyRefs = new List<KeyRefIdentityValue>();
                else multipleValueKeyRefs = new List<KeyRefIdentityValues>();
            }
            else {
                if (isSingleValue) keys = new Dictionary<object, IdentityValue>(SimpleType.ValueEqualityComparer);
                else multipleValueKeys = new Dictionary<IReadOnlyList<object>, IdentityValues>(SimpleType.ValuesEqualityComparer);
            }
            foreach (Element identityElement in Query(this, icInfo.IdentityPathExpression)) {
                IEntityObject entityObject = null;
                object value = null;
                IEntityObject[] entityObjects = null;
                object[] values = null;
                var hasValue = false;
                for (var i = 0; i < valueCount; i++) {
                    IEntityObject tempEntityObject = null;
                    foreach (var valueObj in Query(identityElement, icInfo.ValuePathExpressions[i])) {
                        if (tempEntityObject == null) tempEntityObject = valueObj;
                        else {
                            new Diagnostic(context, identityElement, DiagnosticCode.IdentityConstraintValuePathExpressionCanReturnAtMostOneEntityObject);
                            return false;
                        }
                    }
                    var tempValue = tempEntityObject == null ? null : tempEntityObject.Value;
                    if (tempValue != null) hasValue = true;
                    if (kind == IdentityConstraintKind.Key) {
                        if (tempEntityObject == null) {
                            new Diagnostic(context, identityElement, DiagnosticCode.KeyIdentityConstraintValuePathExpressionMustReturnOneEntityObject);
                            return false;
                        }
                        if (tempValue == null) {
                            new Diagnostic(context, tempEntityObject, DiagnosticCode.KeyIdentityConstraintEntityObjectMustHasNonNullValue);
                            return false;
                        }
                    }
                    if (isSingleValue) {
                        entityObject = tempEntityObject;
                        value = tempValue;
                    }
                    else {
                        if (entityObjects == null) {
                            entityObjects = new IEntityObject[valueCount];
                            values = new object[valueCount];
                        }
                        entityObjects[i] = tempEntityObject;
                        values[i] = tempValue;
                    }
                }
                if (!hasValue) continue;
                IdentityValue identityValue;
                IdentityValues identityValues;
                if (isSingleValue) {
                    if (icInfo.IsSplitListValue) {
                        var listedSimpleTypeValue = value as ListedSimpleTypeValue;
                        if (listedSimpleTypeValue != null) {
                            foreach (var itemValue in listedSimpleTypeValue) {
                                var itemIdentityValue = new IdentityValue(itemValue, identityElement, entityObject);
                                IdentityValue referentialIdentiyValue;
                                if (!referentialIC.Keys.TryGetValue(itemValue, out referentialIdentiyValue)) {
                                    new Diagnostic(context, entityObject, DiagnosticCode.InvalidKeyRefValue, SimpleType.ToString(itemValue));
                                    return false;
                                }
                                keyRefs.Add(new KeyRefIdentityValue(itemIdentityValue, referentialIdentiyValue));
                            }
                            continue;
                        }
                    }
                    identityValue = new IdentityValue(value, identityElement, entityObject);
                    identityValues = default(IdentityValues);
                }
                else {
                    identityValue = default(IdentityValue);
                    identityValues = new IdentityValues(values, identityElement, entityObjects);
                }
                if (isRef) {
                    if (isSingleValue) {
                        IdentityValue referentialIdentiyValue;
                        if (!referentialIC.Keys.TryGetValue(value, out referentialIdentiyValue)) {
                            new Diagnostic(context, entityObject, DiagnosticCode.InvalidKeyRefValue, SimpleType.ToString(value));
                            return false;
                        }
                        keyRefs.Add(new KeyRefIdentityValue(identityValue, referentialIdentiyValue));
                    }
                    else {
                        IdentityValues referentialIdentiyValues;
                        if (!referentialIC.MultipleValueKeys.TryGetValue(values, out referentialIdentiyValues)) {
                            new Diagnostic(context, entityObject, DiagnosticCode.InvalidKeyRefValue, SimpleType.ToSeparatedString(values));
                            return false;
                        }
                        multipleValueKeyRefs.Add(new KeyRefIdentityValues(identityValues, referentialIdentiyValues));
                    }
                }
                else {//Key or Unique
                    if (isSingleValue) {
                        if (keys.ContainsKey(value)) {
                            new Diagnostic(context, entityObject, DiagnosticCode.DuplicateKeyOrUniqueValue, SimpleType.ToString(value));
                            return false;
                        }
                        keys.Add(value, identityValue);
                    }
                    else {
                        if (multipleValueKeys.ContainsKey(values)) {
                            new Diagnostic(context, entityObjects, DiagnosticCode.DuplicateKeyOrUniqueValue, SimpleType.ToSeparatedString(values));
                            return false;
                        }
                        multipleValueKeys.Add(values, identityValues);
                    }
                }
            }
            result = new IdentityConstraint(effectiveElement, icInfo.Name, kind, isSingleValue, keys, multipleValueKeys, keyRefs, multipleValueKeyRefs, referentialIC);
            return true;
        }
        private static IEnumerable<IEntityObject> Query(IEntityObject obj, PathExpressionInfo pathExprInfo) {
            foreach (var pathInfo in pathExprInfo.Paths)
                foreach (var i in Query(obj, pathInfo)) yield return i;
        }
        private static IEnumerable<IEntityObject> Query(IEntityObject obj, PathInfo pathInfo) {
            IEnumerable<IEntityObject> objs = null;
            foreach (var stepInfo in pathInfo.Steps) {
                if (obj != null) {
                    objs = Query(obj, stepInfo);
                    obj = null;
                }
                else objs = objs.SelectMany(i => Query(i, stepInfo));
            }
            return objs;
        }
        private static IEnumerable<IEntityObject> Query(IEntityObject obj, StepInfo stepInfo) {
            var element = (Element)obj;
            var kind = stepInfo.Kind;
            if (kind == StepKind.Self) return Enumerable.Repeat(element, 1);
            else if (kind == StepKind.SelfAndDescendants) return element.SelfAndElementDescendants<Element>();
            else if (kind == StepKind.Descendants) return element.ElementDescendants<Element>();
            else {
                if (stepInfo.IsAttribute) {
                    if (kind == StepKind.Name) return element.Attributes<Attribute>(stepInfo.Name);
                    else if (kind == StepKind.Uri) return element.Attributes<Attribute>(i => i.Name.Namespace == stepInfo.Uri);
                    else return element.Attributes<Attribute>();
                }
                else {
                    if (kind == StepKind.Name) return element.ElementChildren<Element>(stepInfo.Name);
                    else if (kind == StepKind.Uri)
                        return element.ElementChildren<Element>(i => {
                            var name = i.Name;
                            return name != null && name.Namespace == stepInfo.Uri;
                        });
                    else return element.ElementChildren<Element>();
                }
            }
        }
        public static bool TryLoadAndSpecialize<T>(XmlReader reader, Context context, ElementBaseInfo elementBaseInfo, out T result) where T : Element {
            Element genericElement;
            if (TryLoad(reader, context, out genericElement))
                return genericElement.TrySpecialize(elementBaseInfo, context, out result);
            result = null;
            return false;
        }
        public bool TrySpecialize<T>(ElementBaseInfo elementBaseInfo, Context context, out T result) where T : Element {
            T obj;
            var csr = TrySkippableSpecialize(elementBaseInfo, context, out obj);
            if (csr == ChildSpecializationResult.Success) {
                result = obj;
                return true;
            }
            result = null;
            if (csr == ChildSpecializationResult.Skipped) {
                var name = Name;
                var elementWildcardInfo = elementBaseInfo as ElementWildcardInfo;
                if (elementWildcardInfo == null)
                    new Diagnostic(context, this, DiagnosticCode.ElementDeclarationNotFound, name);
                else
                    new Diagnostic(context, this, DiagnosticCode.ElementNamespaceUriNotMatchWithWildcard, name.Namespace, elementWildcardInfo.Wildcard.UrisText);
            }
            return false;
        }
        public ChildSpecializationResult TrySkippableSpecialize<T>(ElementBaseInfo elementBaseInfo, Context context, out T result) where T : Element {
            if (elementBaseInfo == null) throw new ArgumentNullException("elementBaseInfo");
            if (context == null) throw new ArgumentNullException("context");
            result = null;
            var name = Name;
            if (name == null) {
                new Diagnostic(context, this, DiagnosticCode.ElementNameRequired);
                return ChildSpecializationResult.Fault;
            }
            Type resultType = Type;
            ElementInfo elementInfo = null;
            var declElementInfo = elementBaseInfo as ElementInfo;
            if (declElementInfo != null) {
                elementInfo = declElementInfo.TryGet(name);
                if (elementInfo == null) return ChildSpecializationResult.Skipped;
                if (declElementInfo != elementInfo && declElementInfo.InstanceProhibition.IsSet(InstanceProhibition.Substitution)) {
                    new Diagnostic(context, this, DiagnosticCode.SubstitutionInstanceProhibited);
                    return ChildSpecializationResult.Fault;
                }
                if (!TrySpecializeCore(elementInfo, context, declElementInfo.InstanceProhibition, out resultType)) return ChildSpecializationResult.Fault;
            }
            else {
                var wildcardInfo = ((ElementWildcardInfo)elementBaseInfo).Wildcard;
                if (!wildcardInfo.IsMatch(name.Namespace)) return ChildSpecializationResult.Skipped;
                if (wildcardInfo.Validation == WildcardValidation.SkipValidate) {
                    if (!GenericValidate(context, true)) return ChildSpecializationResult.Fault;
                }
                else {
                    elementInfo = wildcardInfo.Program.GetGlobalElement(name);
                    if (elementInfo != null) {
                        if (!TrySpecializeCore(elementInfo, context, InstanceProhibition.None, out resultType)) return ChildSpecializationResult.Fault;
                    }
                    else {
                        if (wildcardInfo.Validation == WildcardValidation.MustValidate && name.IsQualified()) {
                            new Diagnostic(context, this, DiagnosticCode.ElementDeclarationNotFound, name);
                            return ChildSpecializationResult.Fault;
                        }
                        if (!GenericValidate(context, true)) return ChildSpecializationResult.Fault;
                    }
                }
            }
            var resultElement = elementBaseInfo.CreateInstance<T>(Location);
            if (elementInfo != null && elementInfo.IsGlobal)
                resultElement.ReferentialElement = elementInfo.CreateInstance<Element>(Location);
            resultElement.Name = name;
            resultElement.Type = resultType;
            var isNull = IsNull;
            resultElement.IsNull = isNull;
            if (isNull && elementInfo == null) {
                var type = resultElement.Type;
                if (type != null) {
                    var complexType = type as ComplexType;
                    if (complexType != null) complexType.SimpleChild = null;
                    else resultElement.Type = null;
                }
            }
            if (elementInfo != null && !resultElement.TryValidateIdentityConstraints(elementInfo, context)) return ChildSpecializationResult.Fault;
            var effectiveElement = resultElement.EffectiveElement;
            if (!HasParent && !context.TryGetIdDictAndIdRefList(out effectiveElement._idDict, out effectiveElement._idRefList)) return ChildSpecializationResult.Fault;
            effectiveElement._resolvedElementInfo = elementInfo;
            if (!resultElement.InvokeTryValidatePair(context)) return ChildSpecializationResult.Fault;
            result = resultElement;
            return ChildSpecializationResult.Success;
        }
        private bool TrySpecializeCore(ElementInfo elementInfo, Context context, InstanceProhibition typeInstanceProhibition, out Type result) {
            result = null;
            if (elementInfo.IsAbstract) {
                new Diagnostic(context, this, DiagnosticCode.ElememntDeclarationAbstract);
                return false;
            }
            var declTypeInfo = elementInfo.Type;
            var typeInfo = declTypeInfo;
            var typeName = TypeName;
            if (typeName != null) {
                typeInfo = elementInfo.Program.TryGetGlobalType(typeName, true, true);
                if (typeInfo == null) {
                    new Diagnostic(context, this, DiagnosticCode.InvalidTypeName, typeName);
                    return false;
                }
                if (!typeInfo.IsEqualToOrDeriveFrom(declTypeInfo)) {
                    new Diagnostic(context, this, DiagnosticCode.TypeOfTypeNameNotEqualToOrDeriveFromDeclaredType);
                    return false;
                }
                if (declTypeInfo != typeInfo) {
                    var declComplexTypeInfo = declTypeInfo as ComplexTypeInfo;
                    if (declComplexTypeInfo != null) {
                        if (!CheckTypeInstanceProhibition((ComplexTypeInfo)typeInfo, declComplexTypeInfo,
                            typeInstanceProhibition | declComplexTypeInfo.InstanceProhibition, context))
                            return false;
                    }
                }
            }
            var isNull = IsNull;
            var type = Type;
            var complexTypeInfo = typeInfo as ComplexTypeInfo;
            if (isNull) {
                if (!elementInfo.IsNullable) {
                    new Diagnostic(context, this, DiagnosticCode.ElementDeclarationNotNullable);
                    return false;
                }
                else if (HasChildrenEx(type, true)) {
                    new Diagnostic(context, this, DiagnosticCode.NullElementCannotHasChildren);
                    return false;
                }
                if (type == null && complexTypeInfo != null && complexTypeInfo.AttributeSet != null && !complexTypeInfo.AttributeSet.IsOptional) {
                    new Diagnostic(context, this, DiagnosticCode.ElementTypeRequired);
                    return false;
                }
                //var dfValueInfo = elementInfo.DefaultOrFixedValue;
                //if (dfValueInfo != null && dfValueInfo.IsFixed) {
                //    new Diagnostic(context, this, DiagnosticCode.ElementDeclarationHasFixedValueCannotBeNull);
                //    return false;
                //}
            }
            else {
                if (type == null) {
                    new Diagnostic(context, this, DiagnosticCode.ElementTypeRequired);
                    return false;
                }
            }
            if (type != null) {
                var dfValueInfo = elementInfo.DefaultOrFixedValue;
                if (isNull) dfValueInfo = null;
                if (complexTypeInfo != null) {
                    var complexType = type as ComplexType;
                    if (complexType == null) {
                        new Diagnostic(context, this, DiagnosticCode.ComplexTypeRequired);
                        return false;
                    }
                    ComplexType obj;
                    if (!complexType.TrySpecialize(complexTypeInfo, isNull, dfValueInfo, context, out obj)) return false;
                    result = obj;
                }
                else {
                    var simpleTypeInfo = typeInfo as SimpleTypeInfo;
                    if (simpleTypeInfo != null) {
                        var simpleType = type as SimpleType;
                        if (simpleType == null) {
                            var complexType = (ComplexType)type;
                            if (complexType.HasAttributes) {
                                new Diagnostic(context, type, DiagnosticCode.AttributeSetNotAllowed);
                                return false;
                            }
                            if (complexType.HasComplexChild) {
                                new Diagnostic(context, type, DiagnosticCode.ComplexChildNotAllowed);
                                return false;
                            }
                            simpleType = complexType.SimpleChild;
                        }
                        if (!isNull) {
                            if (simpleType == null) {
                                new Diagnostic(context, type, DiagnosticCode.SimpleChildRequired);
                                return false;
                            }
                            if (dfValueInfo != null && simpleType.IsValueNullOrEmptyString) simpleType.Value = dfValueInfo.Value;
                            SimpleType obj;
                            if (!simpleType.TrySpecialize(simpleTypeInfo, context, out obj)) return false;
                            result = obj;
                        }
                    }
                    else {//just sys:Type
                        if (!type.TryValidate(context)) return false;
                        result = type;
                    }
                }
            }
            if (result != null && !isNull) return CheckFixedValue(result, elementInfo, context);
            return true;
        }
        public static bool TryLoad(XmlReader reader, Context context, out Element result) {
            if (reader == null) throw new ArgumentNullException("reader");
            if (context == null) throw new ArgumentNullException("context");
            try {
                if (reader.MoveToContent() != XmlNodeType.Element) throw CreateXmlException("Element node expected", GetLocation(reader));
                result = Create(reader);
                return true;
            }
            catch (Exception ex) {
                result = null;
                new Diagnostic(context, ex, DiagnosticCode.LoadFromXmlReaderException, ex.Message);
                return false;
            }
        }
        private static Element Create(XmlReader reader) {
            var name = XName.Get(reader.LocalName, reader.NamespaceURI);
            var location = GetLocation(reader);
            AttributeSet attributeSet = null;
            var isNull = false;
            XName typeName = null;
            Dictionary<string, string> namespaceUriDict = null;
            if (reader.HasAttributes) {
                while (reader.MoveToNextAttribute()) {
                    var attNsUri = reader.NamespaceURI;
                    var attLocalName = reader.LocalName;
                    switch (attNsUri) {
                        case XmlnsPrefixUriValue:
                            if (namespaceUriDict == null) namespaceUriDict = new Dictionary<string, string>();
                            namespaceUriDict.Add(attLocalName == "xmlns" ? "" : attLocalName, ReadAttributeValue(reader));
                            break;
                        case XmlPrefixUriValue: break;
                        case NamespaceInfo.XsiUriValue:
                            if (attLocalName == "nil") {
                                if (!Boolean.TryParseValue(ReadAttributeValue(reader), out isNull))
                                    throw CreateXmlException("Invalid xsi:nil value, expecting xs:boolean", GetLocation(reader));
                            }
                            else if (attLocalName == "type") {
                                FullNameValue typeFullName;
                                if (!FullNameValue.TryParse(ReadAttributeValue(reader), out typeFullName))
                                    throw CreateXmlException("Invalid xsi:type value, expecting xs:QName", GetLocation(reader));
                                var typeNsUri = reader.LookupNamespace(typeFullName.Prefix);
                                if (typeNsUri == null)
                                    throw CreateXmlException("Invalid xsi:type QName prefix: " + typeFullName.Prefix, GetLocation(reader));
                                typeName = XName.Get(typeFullName.LocalName, typeNsUri);
                            }
                            break;
                        default:
                            var attLocation = GetLocation(reader);
                            if (attributeSet == null) attributeSet = new AttributeSet { Location = attLocation };
                            attributeSet.Add(new Attribute(XName.Get(attLocalName, attNsUri), new SimpleType(ReadAttributeValue(reader), true) { Location = attLocation }) { Location = attLocation });
                            break;
                    }
                }
                reader.MoveToElement();
            }
            object content = null;
            List<object> contentList = null;
            var hasSignificantTexts = false;
            Location? contentLocation = null;
            var hasGetContentLocation = false;
            var hasElements = false;
            var isEmptyElement = reader.IsEmptyElement;
            reader.ReadStartElement();
            if (!isEmptyElement) {
                var nodeType = reader.NodeType;
                while (nodeType != XmlNodeType.EndElement) {
                    object value = null;
                    switch (nodeType) {
                        case XmlNodeType.Text:
                        case XmlNodeType.CDATA:
                        case XmlNodeType.SignificantWhitespace:
                        case XmlNodeType.Whitespace:
                            if (!hasGetContentLocation) {
                                contentLocation = GetLocation(reader);
                                hasGetContentLocation = true;
                            }
                            value = reader.Value;
                            if (nodeType != XmlNodeType.Whitespace) hasSignificantTexts = true;
                            break;
                        case XmlNodeType.Element:
                            if (!hasGetContentLocation) {
                                contentLocation = GetLocation(reader);
                                hasGetContentLocation = true;
                            }
                            value = Create(reader);
                            hasElements = true;
                            break;
                        case XmlNodeType.EntityReference:
                            reader.ResolveEntity();
                            break;
                    }
                    if (value != null) {
                        if (content == null) content = value;
                        else {
                            if (contentList == null) {
                                contentList = new List<object>();
                                contentList.Add(content);
                            }
                            contentList.Add(value);
                        }
                    }
                    if (nodeType != XmlNodeType.Element) reader.Read();
                    nodeType = reader.NodeType;
                }
                reader.ReadEndElement();
            }
            SimpleType simpleChild = null;
            ChildContainer complexChild = null;
            if (hasElements) {
                complexChild = new ChildContainer { Location = contentLocation };
                if (contentList != null) {
                    foreach (var c in contentList) {
                        var element = c as Element;
                        if (element != null) complexChild.Add(element);
                        else if (hasSignificantTexts) complexChild.Add(new Text((string)c) { Location = contentLocation });
                    }
                }
                else complexChild.Add((Element)content);
            }
            else {
                string value;
                if (content != null) value = (string)content;
                else if (contentList != null) value = string.Concat(contentList.Cast<string>());
                else {
                    value = "";
                    contentLocation = location;
                }
                simpleChild = new SimpleType(value, true) { Location = contentLocation };
            }
            return new Element {
                _name = name,
                _isNull = isNull,
                _typeName = typeName,
                _namespaceUriDict = namespaceUriDict,
                Location = location,
                Type = new ComplexType { AttributeSet = attributeSet, SimpleChild = simpleChild, ComplexChild = complexChild, Location = location }
            };
        }
        private static string ReadAttributeValue(XmlReader reader) {
            string value = null;
            while (reader.ReadAttributeValue()) {
                switch (reader.NodeType) {
                    case XmlNodeType.Text:
                        value = value == null ? reader.Value : value + reader.Value;
                        break;
                    case XmlNodeType.EntityReference:
                        reader.ResolveEntity();
                        break;
                }
            }
            return value ?? "";
        }
        private static Location? GetLocation(XmlReader reader) {
            var xmlLineInfo = reader as IXmlLineInfo;
            if (xmlLineInfo != null && xmlLineInfo.HasLineInfo())
                return new Location(reader.BaseURI, xmlLineInfo.LineNumber, xmlLineInfo.LinePosition);
            return null;
        }
        private static XmlException CreateXmlException(string msg, Location? lineInfo) {
            return new XmlException(msg, null, lineInfo.Line(), lineInfo.Column());//cannot set SourceUri!
        }
        public const string XmlnsPrefixUriValue = "http://www.w3.org/2000/xmlns/";
        public const string XmlPrefixUriValue = "http://www.w3.org/XML/1998/namespace";
        public void Save(XmlWriter writer) {
            if (writer == null) throw new ArgumentNullException("writer");
            Save(writer, 0);
        }
        private void Save(XmlWriter writer, int depth) {
            var name = Name;
            if (name == null) throw new InvalidOperationException("Element name required");
            var nsUri = name.NamespaceName;
            var prefix = writer.LookupPrefix(nsUri);
            if (prefix == null && nsUri != "") prefix = "e" + depth.ToString(CultureInfo.InvariantCulture);
            writer.WriteStartElement(prefix, name.LocalName, nsUri);
            if (depth == 0) {
                writer.WriteAttributeString("xmlns", "xsi", null, NamespaceInfo.XsiUriValue);
                writer.WriteAttributeString("xmlns", "xs", null, NamespaceInfo.XsUriValue);
            }
            if (IsNull) writer.WriteAttributeString("xsi", "nil", NamespaceInfo.XsiUriValue, "true");
            var type = Type;
            if (type != null) {
                var instanceTypeInfo = type.TypeInfo;
                if (instanceTypeInfo != null) {
                    var typeName = instanceTypeInfo.Name;
                    if (typeName != null) {
                        var typeLocalName = typeName.LocalName;
                        var typeNsUri = typeName.NamespaceName;
                        if (typeNsUri == NamespaceInfo.XsUriValue)
                            typeLocalName = NamespaceInfo.TypeKindToXsNameDict[instanceTypeInfo.Kind];
                        writer.WriteAttributeString("xsi", "type", NamespaceInfo.XsiUriValue, GetQualifiedNameString(writer, typeNsUri, typeLocalName, depth));
                    }
                }
                var complexType = type as ComplexType;
                if (complexType != null) {
                    var attributeSet = complexType.AttributeSet;
                    if (attributeSet != null) {
                        foreach (var attribute in attributeSet) {
                            var attName = attribute.Name;
                            writer.WriteAttributeString(attName.LocalName, attName.NamespaceName, GetSimpleTypeString(writer, attribute.Type, depth));
                        }
                    }
                    var simpleChild = complexType.SimpleChild;
                    if (simpleChild != null) {
                        writer.WriteString(GetSimpleTypeString(writer, simpleChild, depth));
                    }
                    else {
                        var complexChild = complexType.ComplexChild;
                        if (complexChild != null) {
                            foreach (var contentChild in complexChild.ContentChildren<ContentChild>()) {
                                var element = contentChild as Element;
                                if (element != null) element.Save(writer, depth + 1);
                                else writer.WriteString(((Text)contentChild).Value);
                            }
                        }
                    }
                }
                else {
                    writer.WriteString(GetSimpleTypeString(writer, type as SimpleType, depth));
                }
            }
            writer.WriteEndElement();
        }
        private static string GetQualifiedNameString(XmlWriter writer, string nsUri, string localName, int depth) {
            var prefix = writer.LookupPrefix(nsUri);
            if (prefix == null) {
                if (nsUri != "") prefix = "q" + depth.ToString(CultureInfo.InvariantCulture);
                writer.WriteAttributeString("xmlns", prefix, null, nsUri);
            }
            if (string.IsNullOrEmpty(prefix)) return localName;
            return prefix + ":" + localName;
        }
        private static string GetSimpleTypeString(XmlWriter writer, SimpleType simpleType, int depth) {
            if (simpleType != null) {
                var value = simpleType.Value;
                if (value != null) {
                    var fnValue = value as FullNameValue;
                    if (fnValue != null) {
                        if (fnValue.IsResolved) return GetQualifiedNameString(writer, fnValue.UriValue, fnValue.LocalName, depth);
                    }
                    else return simpleType.ToString();
                }
            }
            return null;
        }
    }
    public enum ChildSpecializationResult { Fault, Skipped, Success }
    [Serializable]
    public sealed class Text : ContentChild {
        public Text() { }
        public Text(string value) { _value = value; }
        private string _value;
        public string Value { get { return _value; } set { _value = value; } }
        public static implicit operator Text(string value) {
            if (value == null) return null;
            return new Text(value);
        }
        public static implicit operator string(Text text) {
            if (text == null) return null;
            return text._value;
        }
    }
    [Serializable]
    public class ChildContainer : Child, IList<Child>, IReadOnlyList<Child> {
        public ChildContainer() { _childList = new List<Child>(); }
        public ChildContainer(IEnumerable<Child> children) : this() { AddRange(children); }
        //
        private List<Child> _childList;
        public int Count { get { return _childList.Count; } }
        public bool HasChildren { get { return _childList.Count > 0; } }
        public bool HasNonTextChildren { get { return _childList.Any(i => !(i is Text)); } }
        public int IndexOf(Child child) { return _childList.IndexOf(child); }
        public bool TryGetIndexOf(int order, out int index) {
            int i;
            var found = false;
            for (i = 0; i < _childList.Count; i++) {
                var childOrder = _childList[i].ChildOrder;
                if (childOrder == order) {
                    found = true;
                    break;
                }
                if (childOrder > order) break;
            }
            index = i;
            return found;
        }
        public int IndexAfter(int order) {
            int i;
            for (i = 0; i < _childList.Count; i++) {
                if (_childList[i].ChildOrder > order) break;
            }
            return i;
        }
        public bool Contains(Child child) { return _childList.Contains(child); }
        public bool Contains(int order) {
            int index;
            return TryGetIndexOf(order, out index);
        }
        public void AddRange(IEnumerable<Child> children) {
            if (children == null) throw new ArgumentNullException("children");
            foreach (var child in children) Add(child);
        }
        public void Add(Child child) {
            child = SetParentTo(child, false);
            var childOrder = child.ChildOrder;
            if (childOrder == -1) _childList.Add(child);
            else _childList.Insert(IndexAfter(childOrder), child);
        }
        public void AddOrSet(Child child) {
            child = SetParentTo(child, false);
            var childOrder = child.ChildOrder;
            if (childOrder == -1) _childList.Add(child);
            else {
                int index;
                if (TryGetIndexOf(childOrder, out index)) _childList[index] = child;
                else _childList.Insert(index, child);
            }
        }
        public void Insert(int index, Child child) { _childList.Insert(index, SetParentTo(child, false)); }
        public Child this[int index] {
            get { return _childList[index]; }
            set { _childList[index] = SetParentTo(value, false); }
        }
        public Child TryGet(int order) {
            int index;
            if (TryGetIndexOf(order, out index)) return _childList[index];
            return null;
        }
        public void RemoveAt(int index) { _childList.RemoveAt(index); }
        public bool Remove(Child child) { return _childList.Remove(child); }
        public bool Remove(int order) {
            int index;
            if (TryGetIndexOf(order, out index)) {
                _childList.RemoveAt(index);
                return true;
            }
            return false;
        }
        public void Clear() { _childList.Clear(); }
        public IEnumerator<Child> GetEnumerator() { return _childList.GetEnumerator(); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }
        public void CopyTo(Child[] array, int arrayIndex) { _childList.CopyTo(array, arrayIndex); }
        bool ICollection<Child>.IsReadOnly { get { return false; } }
        //
        public void SortChildren(bool recursive = true) {
            _childList.Sort((x, y) => x.SpecifiedOrder - y.SpecifiedOrder);
            if (recursive)
                foreach (var i in _childList) {
                    var childContainer = i as ChildContainer;
                    if (childContainer != null) childContainer.SortChildren(recursive);
                }
        }
        //
        public IEnumerable<T> ContentChildren<T>(Func<T, bool> filter = null) where T : ContentChild {
            foreach (var child in _childList) {
                var contentChild = child as T;
                if (contentChild != null) {
                    if (filter == null || filter(contentChild))
                        yield return contentChild;
                }
                else {
                    var childContainer = child as ChildContainer;
                    if (childContainer != null) {
                        foreach (var i in childContainer.ContentChildren<T>(filter))
                            yield return i;
                    }
                }
            }
        }
        public IEnumerable<T> ContentDescendants<T>(Func<T, bool> filter = null) where T : ContentChild {
            foreach (var child in _childList) {
                var contentChild = child as T;
                if (contentChild != null) {
                    if (filter == null || filter(contentChild)) {
                        yield return contentChild;
                        var element = child as Element;
                        if (element != null)
                            foreach (var i in element.ContentDescendants<T>(filter))
                                yield return i;
                    }
                }
                else {
                    var childContainer = child as ChildContainer;
                    if (childContainer != null) {
                        foreach (var i in childContainer.ContentDescendants<T>(filter))
                            yield return i;
                    }
                }
            }
        }
        public IEnumerable<T> ElementChildren<T>(Func<T, bool> filter = null) where T : Element { return ContentChildren<T>(filter); }
        public IEnumerable<T> ElementDescendants<T>(Func<T, bool> filter = null) where T : Element { return ContentDescendants<T>(filter); }
        public IEnumerable<T> ElementChildren<T>(XName name) where T : Element { return ElementChildren<T>(i => i.Name == name); }
        public IEnumerable<T> ElementDescendants<T>(XName name) where T : Element { return ElementDescendants<T>(i => i.Name == name); }
        //
        public override Object DeepClone() {
            var obj = (ChildContainer)base.DeepClone();
            obj._childList = new List<Child>();
            foreach (var child in _childList) obj.DirectAdd(child);
            return obj;
        }
        private void DirectAdd(Child child) { _childList.Add(SetParentTo(child, false)); }
        public ChildContainerInfo ChildContainerInfo { get { return (ChildContainerInfo)ObjectInfo; } }
        protected T CreateChildMember<T>(int order, bool @try = false) where T : Child {
            var memberInfo = ((ChildStructInfo)ObjectInfo).Members[order];
            if (memberInfo == null) {
                if (@try) return null;
                throw new InvalidOperationException("Child member #{0} not exists in the child struct".InvariantFormat(order));
            }
            return memberInfo.CreateInstance<T>(Location, @try);
        }
        protected override sealed bool TryValidateCore(Context context) {
            if (!base.TryValidateCore(context)) return false;
            var dMarker = context.MarkDiagnostics();
            var childContainerInfo = ChildContainerInfo;
            if (childContainerInfo == null) {
                foreach (var child in _childList) child.TryValidate(context);
            }
            else {
                var isMixed = childContainerInfo.IsMixed;
                if (childContainerInfo.Kind == ChildContainerKind.List) {
                    var childListInfo = (ChildListInfo)childContainerInfo;
                    var itemInfo = childListInfo.Item;
                    var itemCount = 0UL;
                    var listIdx = 0;
                    while (itemCount <= childListInfo.MaxOccurs) {
                        var child = TryGetNextNonTextChild(ref listIdx, context, isMixed);
                        if (child == null) break;
                        itemCount++;
                        if (itemInfo.IsAssignableFrom(child, context)) child.TryValidate(context);
                    }
                    if (itemCount < childListInfo.MinOccurs) {
                        new Diagnostic(context, this, DiagnosticCode.ChildListCountNotGreaterThanOrEqualToMinOccurs, itemCount, childListInfo.MinOccurs);
                    }
                    else if (TryGetNextNonTextChild(ref listIdx, context, isMixed) != null)
                        new Diagnostic(context, this, DiagnosticCode.ChildListCountNotLessThanOrEqualToMaxOccurs, itemCount + 1, childListInfo.MaxOccurs);
                }
                else {
                    var childStructInfo = (ChildStructInfo)childContainerInfo;
                    if (childStructInfo.Kind == ChildContainerKind.Seq) {
                        var listIdx = 0;
                        Child child = null;
                        foreach (var memberInfo in childStructInfo.NonNullMembers) {
                            if (child == null) child = TryGetNextNonTextChild(ref listIdx, context, isMixed);
                            if (child == null) {
                                if (!memberInfo.IsEffectiveOptional)
                                    new Diagnostic(context, this, DiagnosticCode.RequiredChildMemberNotFound, memberInfo.MemberName);
                            }
                            else {
                                if (!memberInfo.IsAssignableFrom(child)) {
                                    if (!memberInfo.IsEffectiveOptional)
                                        new Diagnostic(context, this, DiagnosticCode.RequiredChildMemberNotFound, memberInfo.MemberName);
                                }
                                else {
                                    child.TryValidate(context);
                                    child = null;
                                }
                            }
                        }
                        child = TryGetNextNonTextChild(ref listIdx, context, isMixed);
                        if (child != null)
                            new Diagnostic(context, this, DiagnosticCode.RedundantChildMember, GetChildDisplayName(child));
                    }
                    else if (childStructInfo.Kind == ChildContainerKind.Choice) {
                        var listIdx = 0;
                        var child = TryGetNextNonTextChild(ref listIdx, context, isMixed);
                        if (child == null) {
                            if (!childStructInfo.IsEffectiveOptional) new Diagnostic(context, this, DiagnosticCode.ChoiceChildContainerEmpty);
                        }
                        else {
                            var found = false;
                            foreach (var memberInfo in childStructInfo.NonNullMembers) {
                                if (memberInfo.IsAssignableFrom(child)) {
                                    child.TryValidate(context);
                                    found = true;
                                    break;
                                }
                            }
                            if (!found) new Diagnostic(context, this, DiagnosticCode.ChoiceChildContainerNotMatched);
                            child = TryGetNextNonTextChild(ref listIdx, context, isMixed);
                            if (child != null)
                                new Diagnostic(context, this, DiagnosticCode.RedundantChildMember, GetChildDisplayName(child));
                        }
                    }
                    else {//Unordered
                        var listIdx = 0;
                        Child child;
                        var memberInfoList = new List<ChildInfo>(childStructInfo.NonNullMembers);
                        while ((child = TryGetNextNonTextChild(ref listIdx, context, isMixed)) != null) {
                            var memberInfoIdx = -1;
                            for (var i = 0; i < memberInfoList.Count; i++) {
                                if (memberInfoList[i].IsAssignableFrom(child)) {
                                    child.TryValidate(context);
                                    memberInfoIdx = i;
                                    break;
                                }
                            }
                            if (memberInfoIdx == -1) new Diagnostic(context, this, DiagnosticCode.RedundantChildMember, GetChildDisplayName(child));
                            else memberInfoList.RemoveAt(memberInfoIdx);
                        }
                        if (memberInfoList.Count > 0) {
                            foreach (var memberInfo in memberInfoList) {
                                if (!memberInfo.IsEffectiveOptional) new Diagnostic(context, this, DiagnosticCode.RequiredChildMemberNotFound, memberInfo.MemberName);
                            }
                        }
                    }
                }
            }
            return !dMarker.HasErrors;
        }
        private static string GetChildDisplayName(Child child) {
            var element = child as Element;
            if (element != null) {
                var name = element.Name;
                return "element '{0}'".InvariantFormat(name != null ? name.ToString() : null);
            }
            var text = child as Text;
            if (text != null) return "text '{0}'".InvariantFormat(text.Value);
            if (child != null) return child.GetType().FullName;
            return null;
        }
        private Child TryGetNextNonTextChild(ref int listIdx, Context context, bool isMixed) {
            Child child = null;
            for (; listIdx < _childList.Count && child == null; listIdx++) {
                var item = _childList[listIdx];
                if (item is Text) {
                    if (!isMixed) new Diagnostic(context, this, DiagnosticCode.TextChildNotAllowed);
                }
                else child = item;
            }
            return child;
        }
        public bool TrySpecialize<T>(ChildStructInfo childStructInfo, Context context, out T result) where T : ChildContainer {
            if (childStructInfo == null) throw new ArgumentNullException("childStructInfo");
            if (childStructInfo.Kind != ChildContainerKind.Seq) throw new ArgumentException("childStructInfo must be seq", "childStructInfo");
            if (context == null) throw new ArgumentNullException("context");
            return new SpecContext(this, context).TrySpecialize(childStructInfo, out result);
        }
        private sealed class SpecContext {
            internal SpecContext(ChildContainer childContainer, Context context) {
                _context = context;
                _childContainer = childContainer;
                _childList = childContainer._childList;
                _childListIndex = 0;
            }
            private readonly Context _context;
            private readonly ChildContainer _childContainer;
            private readonly List<Child> _childList;
            private int _childListIndex;
            private bool IsAtEnd { get { return _childListIndex >= _childList.Count; } }
            private ChildSpecializationResult GetNextContentChild(bool isMixed, out ContentChild result) {
                result = null;
                if (IsAtEnd) return ChildSpecializationResult.Skipped;
                var item = _childList[_childListIndex++];
                result = item as Element;
                if (result != null) return ChildSpecializationResult.Success;
                result = item as Text;
                if (result != null) {
                    if (isMixed) return ChildSpecializationResult.Success;
                    new Diagnostic(_context, _childContainer, DiagnosticCode.TextChildNotAllowed);
                    result = null;
                    return ChildSpecializationResult.Fault;
                }
                new Diagnostic(_context, _childContainer, DiagnosticCode.OnlyContentChildAllowed);
                return ChildSpecializationResult.Fault;
            }
            private ChildSpecializationResult SpecializeMember(ChildInfo childInfo, bool isMixed, List<Child> resultList) {
                if (IsAtEnd) return ChildSpecializationResult.Skipped;
                var elementBaseInfo = childInfo as ElementBaseInfo;
                if (elementBaseInfo != null) {
                    var childListIndex = _childListIndex;
                    var resultListIndex = resultList.Count;
                    while (true) {
                        ContentChild contentChild;
                        var res = GetNextContentChild(isMixed, out contentChild);
                        if (res == ChildSpecializationResult.Success) {
                            var element = contentChild as Element;
                            if (element != null) {
                                Element result;
                                res = element.TrySkippableSpecialize<Element>(elementBaseInfo, _context, out result);
                                if (res == ChildSpecializationResult.Success) resultList.Add(result);
                            }
                            else {
                                resultList.Add(contentChild);
                                continue;
                            }
                        }
                        if (res != ChildSpecializationResult.Success) {
                            _childListIndex = childListIndex;
                            resultList.RemoveRange(resultListIndex, resultList.Count - resultListIndex);
                        }
                        return res;
                    }
                }
                else {
                    ChildContainer result;
                    var res = SpecializeContainer((ChildContainerInfo)childInfo, out result);
                    if (res == ChildSpecializationResult.Success) resultList.Add(result);
                    return res;
                }
            }
            private ChildSpecializationResult SpecializeContainer(ChildContainerInfo childContainerInfo, out ChildContainer result) {
                result = null;
                if (IsAtEnd) return ChildSpecializationResult.Skipped;
                var isMixed = childContainerInfo.IsMixed;
                var memberList = new List<Child>();
                ChildStructInfo childStructInfo = null;
                var isUnordered = false;
                if (childContainerInfo.Kind == ChildContainerKind.List) {
                    var childListInfo = (ChildListInfo)childContainerInfo;
                    var itemInfo = childListInfo.Item;
                    var itemCount = 0UL;
                    while (itemCount <= childListInfo.MaxOccurs) {
                        var res = SpecializeMember(itemInfo, isMixed, memberList);
                        if (res == ChildSpecializationResult.Success) itemCount++;
                        else if (res == ChildSpecializationResult.Fault) return res;
                        else {//Skipped
                            if (memberList.Count == 0) return res;
                            if (itemCount < childListInfo.MinOccurs) {
                                new Diagnostic(_context, _childContainer, DiagnosticCode.ChildListCountNotGreaterThanOrEqualToMinOccurs, itemCount, childListInfo.MinOccurs);
                                return ChildSpecializationResult.Fault;
                            }
                            else break;
                        }
                    }
                }
                else {
                    childStructInfo = (ChildStructInfo)childContainerInfo;
                    if (childStructInfo.Kind == ChildContainerKind.Seq) {
                        foreach (var memberInfo in childStructInfo.NonNullMembers) {
                            var res = SpecializeMember(memberInfo, isMixed, memberList);
                            if (res == ChildSpecializationResult.Fault) return res;
                            else if (res == ChildSpecializationResult.Skipped) {
                                if (!memberInfo.IsEffectiveOptional) {
                                    if (memberList.Count == 0) return res;
                                    new Diagnostic(_context, _childContainer, DiagnosticCode.RequiredChildMemberNotFound, memberInfo.MemberName);
                                    return ChildSpecializationResult.Fault;
                                }
                            }
                        }
                    }
                    else if (childStructInfo.Kind == ChildContainerKind.Choice) {
                        foreach (var memberInfo in childStructInfo.NonNullMembers) {
                            var res = SpecializeMember(memberInfo, isMixed, memberList);
                            if (res == ChildSpecializationResult.Success) break;
                            else if (res == ChildSpecializationResult.Fault) return res;
                        }
                    }
                    else {//Unordered
                        isUnordered = true;
                        var memberInfoList = new List<ChildInfo>(childStructInfo.NonNullMembers);
                        while (memberInfoList.Count > 0) {
                            var memberInfoIdx = -1;
                            for (var i = 0; i < memberInfoList.Count; i++) {
                                var res = SpecializeMember(memberInfoList[i], isMixed, memberList);
                                if (res == ChildSpecializationResult.Success) {
                                    memberInfoIdx = i;
                                    break;
                                }
                                else if (res == ChildSpecializationResult.Fault) return res;
                            }
                            if (memberInfoIdx == -1) {
                                if (memberList.Count == 0) return ChildSpecializationResult.Skipped;
                                foreach (var memberInfo in memberInfoList) {
                                    if (!memberInfo.IsEffectiveOptional) {
                                        new Diagnostic(_context, _childContainer, DiagnosticCode.RequiredChildMemberNotFound, memberInfo.MemberName);
                                        return ChildSpecializationResult.Fault;
                                    }
                                }
                                break;
                            }
                            else memberInfoList.RemoveAt(memberInfoIdx);
                        }
                    }
                }
                if (memberList.Count == 0) return ChildSpecializationResult.Skipped;
                var resultChildContainer = childContainerInfo.CreateInstance<ChildContainer>(_childContainer.Location);
                if (isUnordered) {
                    var order = 0;
                    foreach (var i in memberList) {
                        if (i is Element) i.SpecifiedOrder = order++;
                        resultChildContainer.DirectAdd(i);
                    }
                }
                else {
                    foreach (var i in memberList) resultChildContainer.DirectAdd(i);
                }
                if ((childStructInfo == null || !childStructInfo.IsRoot) && !resultChildContainer.InvokeTryValidatePair(_context)) return ChildSpecializationResult.Fault;
                result = resultChildContainer;
                return ChildSpecializationResult.Success;
            }
            internal bool TrySpecialize<T>(ChildStructInfo childStructInfo, out T result) where T : ChildContainer {
                result = null;
                ChildContainer resultChildContainer;
                var res = SpecializeContainer(childStructInfo, out resultChildContainer);
                if (res == ChildSpecializationResult.Fault) return false;
                if (resultChildContainer == null) {//skipped
                    if (!childStructInfo.IsEffectiveOptional) {
                        new Diagnostic(_context, _childContainer, DiagnosticCode.ComplexChildNotMatched);
                        return false;
                    }
                    resultChildContainer = childStructInfo.CreateInstance<ChildContainer>(_childContainer.Location);
                }
                if (!IsAtEnd) {
                    var isMixed = childStructInfo.IsMixed;
                    ContentChild contentChild;
                    while ((res = GetNextContentChild(isMixed, out contentChild)) != ChildSpecializationResult.Skipped) {
                        if (res == ChildSpecializationResult.Fault) return false;
                        if (contentChild is Element) {
                            new Diagnostic(_context, _childContainer, DiagnosticCode.RedundantChildMember, GetChildDisplayName(contentChild));
                            return false;
                        }
                        resultChildContainer.DirectAdd(contentChild);
                    }
                }
                if (!resultChildContainer.InvokeTryValidatePair(_context)) return false;
                result = (T)resultChildContainer;
                return true;
            }
        }
    }
    [Serializable]
    public abstract class ChildList<T> : ChildContainer, IList<T>, IReadOnlyList<T> where T : Child {
        protected ChildList() { }
        public IList<T> ItemList { get { return this; } }
        public bool Contains(T item) { return base.Contains(item); }
        public int IndexOf(T item) { return base.IndexOf(item); }
        public void Add(T item) { base.Add(item); }
        public void Insert(int index, T item) { base.Insert(index, item); }
        new public T this[int index] {
            get { return base[index] as T; }
            set { base[index] = value; }
        }
        public bool Remove(T item) { return base.Remove(item); }
        new public IEnumerator<T> GetEnumerator() {
            using (var enumerator = base.GetEnumerator())
                while (enumerator.MoveNext())
                    yield return enumerator.Current as T;
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }
        public void CopyTo(T[] array, int arrayIndex) { ExtensionMethods.CopyTo(this, array, arrayIndex); }
        bool ICollection<T>.IsReadOnly { get { return false; } }
        //
        public ChildListInfo ChildListInfo { get { return (ChildListInfo)ObjectInfo; } }
        protected TItem CreateItem<TItem>() where TItem : Child { return ((ChildListInfo)ObjectInfo).Item.CreateInstance<TItem>(Location); }
        protected TItem CreateAndAddItem<TItem>() where TItem : Child {
            var item = CreateItem<TItem>();
            Add(item);
            return item;
        }
    }

    #region metadata
    [Serializable]
    public abstract class ProgramInfo {
        protected ProgramInfo() { }
        private volatile IReadOnlyDictionary<XNamespace, NamespaceInfo> _namespaceDict;
        public IReadOnlyDictionary<XNamespace, NamespaceInfo> NamespaceDict {
            get {
                if (_namespaceDict == null) {
                    var namespaceDict = new Dictionary<XNamespace, NamespaceInfo>();
                    var sysNs = NamespaceInfo.System;
                    namespaceDict.Add(sysNs.Uri, sysNs);
                    var namespaces = GetNamespaces();
                    if (namespaces != null)
                        foreach (var ns in namespaces)
                            namespaceDict.Add(ns.Uri, ns);
                    _namespaceDict = namespaceDict;
                }
                return _namespaceDict;
            }
        }
        protected abstract IEnumerable<NamespaceInfo> GetNamespaces();
        private NamespaceInfo TryGetNamespace(XName name) {
            if (name == null) throw new ArgumentNullException("name");
            return NamespaceDict.TryGetValue(name.Namespace);
        }
        public ObjectInfo GetGlobalObject(XName name, GlobalObjectKind kind, bool @try = true) {
            ObjectInfo obj = null;
            var ns = TryGetNamespace(name);
            if (ns != null)
                switch (kind) {
                    case GlobalObjectKind.Type: obj = ns.TypeDict.TryGetValue(name.LocalName); break;
                    case GlobalObjectKind.Attribute: obj = ns.AttributeDict.TryGetValue(name.LocalName); break;
                    case GlobalObjectKind.Element: obj = ns.ElementDict.TryGetValue(name.LocalName); break;
                    default: throw new ArgumentException("kind");
                }
            if (obj == null && !@try) throw new ArgumentException("Invalid global {0} name: {1}".InvariantFormat(kind.ToString(), name));
            return obj;
        }
        public TypeInfo TryGetGlobalType(XName name, bool @try = true, bool isXsName = false) {
            TypeInfo obj = null;
            var ns = TryGetNamespace(name);
            if (ns != null) {
                var localName = name.LocalName;
                if (isXsName && ns.IsSystem) {
                    TypeKind typeKind;
                    if (NamespaceInfo.TryGetTypeKind(localName, out typeKind)) localName = typeKind.ToString();
                }
                obj = ns.TypeDict.TryGetValue(localName);
            }
            if (obj == null && !@try) throw new ArgumentException("Invalid global type name: " + name);
            return obj;
        }
        public AttributeInfo GetGlobalAttribute(XName name, bool @try = true) {
            return GetGlobalObject(name, GlobalObjectKind.Attribute, @try) as AttributeInfo;
        }
        public ElementInfo GetGlobalElement(XName name, bool @try = true) {
            return GetGlobalObject(name, GlobalObjectKind.Element, @try) as ElementInfo;
        }
    }
    public enum GlobalObjectKind { Type, Attribute, Element }
    [Serializable]
    public sealed class GlobalObjectRefInfo {
        public GlobalObjectRefInfo(ProgramInfo program, XName name, GlobalObjectKind kind) {
            if (program == null) throw new ArgumentNullException("program");
            if (name == null) throw new ArgumentNullException("name");
            Program = program;
            Name = name;
            Kind = kind;
        }
        public readonly ProgramInfo Program;
        public readonly XName Name;
        public readonly GlobalObjectKind Kind;
        private volatile ObjectInfo _value;
        public ObjectInfo GetValue(bool @try = true) { return _value ?? (_value = Program.GetGlobalObject(Name, Kind, @try)); }
    }
    public sealed class NamespaceInfo {
        public NamespaceInfo(XNamespace uri, IEnumerable<TypeInfo> types, IEnumerable<AttributeInfo> attributes, IEnumerable<ElementInfo> elements) {
            if (uri == null) throw new ArgumentNullException("uri");
            Uri = uri;
            var typeDict = new Dictionary<string, TypeInfo>();
            if (types != null)
                foreach (var type in types)
                    typeDict.Add(type.LocalName, type);
            TypeDict = typeDict;
            var attributeDict = new Dictionary<string, AttributeInfo>();
            if (attributes != null)
                foreach (var attribute in attributes)
                    attributeDict.Add(attribute.LocalName, attribute);
            AttributeDict = attributeDict;
            var elementDict = new Dictionary<string, ElementInfo>();
            if (elements != null)
                foreach (var element in elements)
                    elementDict.Add(element.LocalName, element);
            ElementDict = elementDict;
        }
        public readonly XNamespace Uri;
        public bool IsSystem { get { return Uri == SystemUri; } }
        public readonly IReadOnlyDictionary<string, TypeInfo> TypeDict;
        public readonly IReadOnlyDictionary<string, AttributeInfo> AttributeDict;
        public readonly IReadOnlyDictionary<string, ElementInfo> ElementDict;
        //
        public const string XsUriValue = "http://www.w3.org/2001/XMLSchema";
        public const string XsiUriValue = "http://www.w3.org/2001/XMLSchema-instance";
        public static readonly IReadOnlyDictionary<TypeKind, string> TypeKindToXsNameDict = new Dictionary<TypeKind, string> {
            {TypeKind.Type, "anyType"},
            {TypeKind.SimpleType, "anySimpleType"},
            {TypeKind.IdRefs, "IDREFS"},
            {TypeKind.Entities, "ENTITIES"},
            {TypeKind.NameTokens, "NMTOKENS"},
            {TypeKind.String, "string"},
            {TypeKind.NormalizedString, "normalizedString"},
            {TypeKind.Token, "token"},
            {TypeKind.Language, "language"},
            {TypeKind.Name, "Name"},
            {TypeKind.NonColonizedName, "NCName"},
            {TypeKind.Id, "ID"},
            {TypeKind.IdRef, "IDREF"},
            {TypeKind.Entity, "ENTITY"},
            {TypeKind.NameToken, "NMTOKEN"},
            {TypeKind.Uri, "anyURI"},
            {TypeKind.FullName, "QName"},
            //{TypeKind.Notation, "NOTATION"},
            {TypeKind.Decimal, "decimal"},
            {TypeKind.Integer, "integer"},
            {TypeKind.NonPositiveInteger, "nonPositiveInteger"},
            {TypeKind.NegativeInteger, "negativeInteger"},
            {TypeKind.Int64, "long"},
            {TypeKind.Int32, "int"},
            {TypeKind.Int16, "short"},
            {TypeKind.SByte, "byte"},
            {TypeKind.NonNegativeInteger, "nonNegativeInteger"},
            {TypeKind.PositiveInteger, "positiveInteger"},
            {TypeKind.UInt64, "unsignedLong"},
            {TypeKind.UInt32, "unsignedInt"},
            {TypeKind.UInt16, "unsignedShort"},
            {TypeKind.Byte, "unsignedByte"},
            {TypeKind.Single, "float"},
            {TypeKind.Double, "double"},
            {TypeKind.Boolean, "boolean"},
            {TypeKind.Base64Binary, "base64Binary"},
            {TypeKind.HexBinary, "hexBinary"},
            {TypeKind.TimeSpan, "duration"},
            {TypeKind.DateTime, "dateTime"},
            {TypeKind.Date, "date"},
            {TypeKind.Time, "time"},
            {TypeKind.Year, "gYear"},
            {TypeKind.YearMonth, "gYearMonth"},
            {TypeKind.Month, "gMonth"},
            {TypeKind.MonthDay, "gMonthDay"},
            {TypeKind.Day, "gDay"},
        };
        public static bool TryGetTypeKind(string xsTypeLocalName, out TypeKind typeKind) {
            foreach (var pair in TypeKindToXsNameDict) {
                if (pair.Value == xsTypeLocalName) {
                    typeKind = pair.Key;
                    return true;
                }
            }
            typeKind = default(TypeKind);
            return false;
        }
        public static readonly XNamespace SystemUri = XNamespace.Get(XsUriValue);
        private static volatile NamespaceInfo _system;
        public static NamespaceInfo System {
            get {
                return _system ?? (_system = new NamespaceInfo(SystemUri, new TypeInfo[] {
                    Type.ThisInfo,
                    SimpleType.ThisInfo,
                    IdRefs.ThisInfo,
                    Entities.ThisInfo,
                    NameTokens.ThisInfo,
                    String.ThisInfo,
                    NormalizedString.ThisInfo,
                    Token.ThisInfo,
                    Language.ThisInfo,
                    X.Name.ThisInfo,
                    NonColonizedName.ThisInfo,
                    Id.ThisInfo,
                    IdRef.ThisInfo,
                    Entity.ThisInfo,
                    NameToken.ThisInfo,
                    X.Uri.ThisInfo,
                    FullName.ThisInfo,
                    //Notation.ThisInfo,
                    Decimal.ThisInfo,
                    Integer.ThisInfo,
                    NonPositiveInteger.ThisInfo,
                    NegativeInteger.ThisInfo,
                    NonNegativeInteger.ThisInfo,
                    PositiveInteger.ThisInfo,
                    Int64.ThisInfo,
                    Int32.ThisInfo,
                    Int16.ThisInfo,
                    SByte.ThisInfo,
                    UInt64.ThisInfo,
                    UInt32.ThisInfo,
                    UInt16.ThisInfo,
                    Byte.ThisInfo,
                    Single.ThisInfo,
                    Double.ThisInfo,
                    Boolean.ThisInfo,
                    Base64Binary.ThisInfo,
                    HexBinary.ThisInfo,
                    TimeSpan.ThisInfo,
                    DateTime.ThisInfo,
                    Date.ThisInfo,
                    Time.ThisInfo,
                    YearMonth.ThisInfo,
                    Year.ThisInfo,
                    MonthDay.ThisInfo,
                    Month.ThisInfo,
                    Day.ThisInfo,
                }, null, null));
            }
        }
    }
    [Serializable]
    public abstract class ObjectInfo {
        protected ObjectInfo(SType clrType) {
            if (clrType == null) throw new ArgumentNullException("clrType");
            ClrType = clrType;
        }
        public readonly SType ClrType;
        public bool IsAssignableFrom(Object obj) {
            if (obj == null) throw new ArgumentNullException("obj");
            return ClrType.IsAssignableFrom(obj.GetType());
        }
        public bool IsAssignableFrom(Object obj, Context context) {
            if (IsAssignableFrom(obj)) return true;
            new Diagnostic(context, obj, DiagnosticCode.InvalidObjectClrType, obj.GetType().FullName, ClrType.FullName);
            return false;
        }
        private T CreateInstance<T>(bool @try = false) where T : Object {
            if (ClrType.IsAbstract) {
                if (@try) return null;
                throw new InvalidOperationException("Clr type '{0}' is abstract".InvariantFormat(ClrType.FullName));
            }
            if (ExtensionMethods.IsAssignableTo(typeof(T), ClrType, @try)) return (T)ExtensionMethods.CreateInstance(ClrType);
            return null;
        }
        public T CreateInstance<T>(Location? location, bool @try = false) where T : Object {
            var obj = CreateInstance<T>(@try);
            if (obj != null && location != null) obj.Location = location;
            return obj;
        }
    }
    public enum TypeKind {
        //None = 0,
        Type = 990,
        SimpleType,
        IdRefs,
        Entities,
        NameTokens,
        ComplexType = 1000,
        ListedSimpleType,
        UnitedSimpleType,
        //
        String = XmlTypeCode.String,
        NormalizedString = XmlTypeCode.NormalizedString,
        Token = XmlTypeCode.Token,
        Language = XmlTypeCode.Language,
        Name = XmlTypeCode.Name,
        NonColonizedName = XmlTypeCode.NCName,
        Id = XmlTypeCode.Id,
        IdRef = XmlTypeCode.Idref,
        Entity = XmlTypeCode.Entity,
        NameToken = XmlTypeCode.NmToken,
        Uri = XmlTypeCode.AnyUri,
        FullName = XmlTypeCode.QName,
        //Notation = XmlTypeCode.Notation,
        Decimal = XmlTypeCode.Decimal,
        Integer = XmlTypeCode.Integer,
        NonPositiveInteger = XmlTypeCode.NonPositiveInteger,
        NegativeInteger = XmlTypeCode.NegativeInteger,
        NonNegativeInteger = XmlTypeCode.NonNegativeInteger,
        PositiveInteger = XmlTypeCode.PositiveInteger,
        Int64 = XmlTypeCode.Long,
        Int32 = XmlTypeCode.Int,
        Int16 = XmlTypeCode.Short,
        SByte = XmlTypeCode.Byte,
        UInt64 = XmlTypeCode.UnsignedLong,
        UInt32 = XmlTypeCode.UnsignedInt,
        UInt16 = XmlTypeCode.UnsignedShort,
        Byte = XmlTypeCode.UnsignedByte,
        Single = XmlTypeCode.Float,
        Double = XmlTypeCode.Double,
        Boolean = XmlTypeCode.Boolean,
        Base64Binary = XmlTypeCode.Base64Binary,
        HexBinary = XmlTypeCode.HexBinary,
        DateTime = XmlTypeCode.DateTime,
        Date = XmlTypeCode.Date,
        Time = XmlTypeCode.Time,
        TimeSpan = XmlTypeCode.Duration,
        YearMonth = XmlTypeCode.GYearMonth,
        Year = XmlTypeCode.GYear,
        MonthDay = XmlTypeCode.GMonthDay,
        Month = XmlTypeCode.GMonth,
        Day = XmlTypeCode.GDay,
    }
    [Serializable]
    public class TypeInfo : ObjectInfo {
        public TypeInfo(SType clrType, TypeKind kind, XName name, TypeInfo baseType)
            : base(clrType) {
            Kind = kind;
            Name = name;
            BaseType = baseType;
        }
        public TypeKind Kind { get; private set; }
        public readonly XName Name;//null for local type
        public string LocalName { get { return Name == null ? null : Name.LocalName; } }
        public bool IsGlobal { get { return Name != null; } }
        public readonly TypeInfo BaseType;//opt
        public bool IsEqualToOrDeriveFrom(TypeInfo other) {
            if (other == null) throw new ArgumentNullException("other");
            for (var info = this; info != null; info = info.BaseType)
                if (info == other) return true;
            return false;
        }
    }
    [Serializable]
    public sealed class FacetSetInfo {
        public FacetSetInfo(
            ulong? minLength = null, bool minLengthFixed = false,
            ulong? maxLength = null, bool maxLengthFixed = false,
            byte? totalDigits = null, bool totalDigitsFixed = false,
            byte? fractionDigits = null, bool fractionDigitsFixed = false,
            object lowerValue = null, bool lowerValueInclusive = false, bool lowerValueFixed = false, string lowerValueText = null,
            object upperValue = null, bool upperValueInclusive = false, bool upperValueFixed = false, string upperValueText = null,
            IReadOnlyList<EnumerationItemInfo> enumerations = null, string enumerationsText = null,
            IReadOnlyList<PatternItemInfo> patterns = null,
            WhitespaceNormalization? whitespaceNormalization = null, bool whitespaceNormalizationFixed = false) {
            MinLength = minLength; MinLengthFixed = minLengthFixed;
            MaxLength = maxLength; MaxLengthFixed = maxLengthFixed;
            TotalDigits = totalDigits; TotalDigitsFixed = totalDigitsFixed;
            FractionDigits = fractionDigits; FractionDigitsFixed = fractionDigitsFixed;
            LowerValue = lowerValue; LowerValueInclusive = lowerValueInclusive; LowerValueFixed = lowerValueFixed; LowerValueText = lowerValueText;
            UpperValue = upperValue; UpperValueInclusive = upperValueInclusive; UpperValueFixed = upperValueFixed; UpperValueText = upperValueText;
            Enumerations = enumerations; EnumerationsText = enumerationsText;
            Patterns = patterns;
            WhitespaceNormalization = whitespaceNormalization; WhitespaceNormalizationFixed = whitespaceNormalizationFixed;
        }
        public readonly ulong? MinLength;
        public readonly bool MinLengthFixed;
        public readonly ulong? MaxLength;
        public readonly bool MaxLengthFixed;
        public readonly byte? TotalDigits;
        public readonly bool TotalDigitsFixed;
        public readonly byte? FractionDigits;
        public readonly bool FractionDigitsFixed;
        public readonly object LowerValue;
        public readonly bool LowerValueInclusive;
        public readonly bool LowerValueFixed;
        public readonly string LowerValueText;
        public readonly object UpperValue;
        public readonly bool UpperValueInclusive;
        public readonly bool UpperValueFixed;
        public readonly string UpperValueText;
        public readonly IReadOnlyList<EnumerationItemInfo> Enumerations;
        public readonly string EnumerationsText;
        public readonly IReadOnlyList<PatternItemInfo> Patterns;
        public readonly WhitespaceNormalization? WhitespaceNormalization;
        public readonly bool WhitespaceNormalizationFixed;
        public bool EnumerationsContains(object value) {
            if (Enumerations != null)
                foreach (var i in Enumerations)
                    if (SimpleType.ValueEquals(i.Value, value)) return true;
            return false;
        }
    }
    [Serializable]
    public struct EnumerationItemInfo {
        public EnumerationItemInfo(string name, object value) {
            if (value == null) throw new ArgumentNullException("value");
            Name = name;
            Value = value;
        }
        public readonly string Name;//opt
        public readonly object Value;
    }
    [Serializable]
    public sealed class PatternItemInfo {
        public PatternItemInfo(string pattern) {
            if (pattern == null) throw new ArgumentNullException("pattern");
            Pattern = pattern;
        }
        public readonly string Pattern;
        private static readonly ConcurrentDictionary<string, Regex> _regexDict = new ConcurrentDictionary<string, Regex>();
        public Regex Regex { get { return _regexDict.GetOrAdd(Pattern, p => new Regex(p)); } }
    }
    public enum WhitespaceNormalization { Preserve = 1, Replace, Collapse, }
    public interface ISimpleTypeInfo {
        TypeKind Kind { get; }
        SType ValueClrType { get; }
        FacetSetInfo FacetSet { get; }
        ISimpleTypeInfo ItemType { get; }//for listed simple type
        IReadOnlyList<IUnitedSimpleTypeMemberInfo> Members { get; }//for united simple type
    }
    [Serializable]
    public class SimpleTypeInfo : TypeInfo, ISimpleTypeInfo {
        public SimpleTypeInfo(SType clrType, TypeKind kind, XName name, TypeInfo baseType, SType valueClrType, FacetSetInfo facetSet)
            : base(clrType, kind, name, baseType) {
            if (baseType == null) throw new ArgumentNullException("baseType");
            if (valueClrType == null) throw new ArgumentNullException("valueClrType");
            ValueClrType = valueClrType;
            FacetSet = facetSet;
        }
        public SType ValueClrType { get; private set; }
        public FacetSetInfo FacetSet { get; private set; }//opt
        public virtual SimpleTypeInfo ItemType { get { return null; } }
        public virtual IReadOnlyList<UnitedSimpleTypeMemberInfo> Members { get { return null; } }
        ISimpleTypeInfo ISimpleTypeInfo.ItemType { get { return ItemType; } }
        IReadOnlyList<IUnitedSimpleTypeMemberInfo> ISimpleTypeInfo.Members { get { return Members; } }
    }
    [Serializable]
    public sealed class AtomicSimpleTypeInfo : SimpleTypeInfo {
        public AtomicSimpleTypeInfo(SType clrType, TypeKind kind, XName name, SimpleTypeInfo baseType, SType valueClrType, FacetSetInfo facetSet)
            : base(clrType, kind, name, baseType, valueClrType, facetSet) { }
        new public SimpleTypeInfo BaseType { get { return (SimpleTypeInfo)base.BaseType; } }
    }
    [Serializable]
    public sealed class ListedSimpleTypeInfo : SimpleTypeInfo {
        public ListedSimpleTypeInfo(SType clrType, XName name, SimpleTypeInfo itemType)
            : base(clrType, TypeKind.ListedSimpleType, name, SimpleType.ThisInfo, typeof(ListedSimpleTypeValue), null) {
            if (itemType == null) throw new ArgumentNullException("itemType");
            _itemType = itemType;
        }
        public ListedSimpleTypeInfo(SType clrType, XName name, ListedSimpleTypeInfo baseType, FacetSetInfo facetSet)
            : base(clrType, TypeKind.ListedSimpleType, name, baseType, typeof(ListedSimpleTypeValue), facetSet) { }
        new public SimpleTypeInfo BaseType { get { return (SimpleTypeInfo)base.BaseType; } }
        private readonly SimpleTypeInfo _itemType;//opt
        public override SimpleTypeInfo ItemType { get { return _itemType ?? BaseType.ItemType; } }
    }
    public interface IUnitedSimpleTypeMemberInfo {
        string Name { get; }
        ISimpleTypeInfo Type { get; }
    }
    [Serializable]
    public sealed class UnitedSimpleTypeMemberInfo : IUnitedSimpleTypeMemberInfo {
        public UnitedSimpleTypeMemberInfo(string name, SimpleTypeInfo type) {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
            if (type == null) throw new ArgumentNullException("type");
            Name = name;
            Type = type;
        }
        public readonly string Name;
        public readonly SimpleTypeInfo Type;
        string IUnitedSimpleTypeMemberInfo.Name { get { return Name; } }
        ISimpleTypeInfo IUnitedSimpleTypeMemberInfo.Type { get { return Type; } }
    }
    [Serializable]
    public sealed class UnitedSimpleTypeInfo : SimpleTypeInfo {
        public UnitedSimpleTypeInfo(SType clrType, XName name, IReadOnlyList<UnitedSimpleTypeMemberInfo> members)
            : base(clrType, TypeKind.UnitedSimpleType, name, SimpleType.ThisInfo, typeof(UnitedSimpleTypeValue), null) {
            if (members == null || members.Count == 0) throw new ArgumentNullException("members");
            _members = members;
        }
        public UnitedSimpleTypeInfo(SType clrType, XName name, UnitedSimpleTypeInfo baseType, FacetSetInfo facetSet)
            : base(clrType, TypeKind.UnitedSimpleType, name, baseType, typeof(UnitedSimpleTypeValue), facetSet) { }
        new public SimpleTypeInfo BaseType { get { return (SimpleTypeInfo)base.BaseType; } }
        private readonly IReadOnlyList<UnitedSimpleTypeMemberInfo> _members;
        public override IReadOnlyList<UnitedSimpleTypeMemberInfo> Members { get { return _members ?? BaseType.Members; } }
    }
    [Flags]
    public enum InstanceProhibition {//xsd block
        None = 0,//allow any instance
        Substitution = XmlSchemaDerivationMethod.Substitution,//1, element substitution
        Extension = XmlSchemaDerivationMethod.Extension,//2
        Restriction = XmlSchemaDerivationMethod.Restriction,//4
        All = Substitution | Extension | Restriction,
    }
    [Serializable]
    public sealed class ComplexTypeInfo : TypeInfo {
        public ComplexTypeInfo(SType clrType, XName name, TypeInfo baseType, bool isExtension, bool isAbstract, InstanceProhibition instanceProhibition,
             AttributeSetInfo attributeSet, SimpleTypeInfo simpleChild, ChildStructInfo complexChild)
            : base(clrType, TypeKind.ComplexType, name, baseType) {
            if (baseType == null) throw new ArgumentNullException("baseType");
            IsExtension = isExtension;
            IsAbstract = isAbstract;
            InstanceProhibition = instanceProhibition;
            AttributeSet = attributeSet;
            SimpleChild = simpleChild;
            ComplexChild = complexChild;
        }
        public readonly bool IsExtension;
        public readonly bool IsAbstract;
        public readonly InstanceProhibition InstanceProhibition;
        public readonly AttributeSetInfo AttributeSet;//opt
        public readonly SimpleTypeInfo SimpleChild;//opt
        public readonly ChildStructInfo ComplexChild;//opt
    }
    public enum WildcardUriKind {
        Any = 0,//qualified or unqualified
        Other,//qulified except this ns
        Specific,//qualified
        Unqualified,
    }
    [Serializable]
    public sealed class WildcardUriInfo : IEquatable<WildcardUriInfo> {
        public WildcardUriInfo(WildcardUriKind kind, XNamespace value) {
            if (kind == WildcardUriKind.Other || kind == WildcardUriKind.Specific) {
                if (value == null) throw new ArgumentNullException("value");
            }
            else if (kind == WildcardUriKind.Any || kind == WildcardUriKind.Unqualified) {
                if (value != null) throw new ArgumentException("value is not null");
            }
            else throw new ArgumentException("kind");
            Kind = kind;
            Value = value;
        }
        public readonly WildcardUriKind Kind;
        public readonly XNamespace Value;//opt for Any or Unqualified
        public bool IsMatch(XNamespace value) {
            if (value == null) throw new ArgumentNullException("value");
            switch (Kind) {
                case WildcardUriKind.Any: return true;
                case WildcardUriKind.Other: return value != Value && !value.IsEmpty();
                case WildcardUriKind.Specific: return value == Value;
                case WildcardUriKind.Unqualified: return value.IsEmpty();
                default: throw new InvalidOperationException();
            }
        }
        public bool Equals(WildcardUriInfo other) {
            if (object.ReferenceEquals(this, other)) return true;
            if (object.ReferenceEquals(other, null)) return false;
            return Kind == other.Kind && Value == other.Value;
        }
        public override bool Equals(object obj) { return Equals(obj as WildcardUriInfo); }
        public override int GetHashCode() {
            return ExtensionMethods.CombineHash(Kind.GetHashCode(), Value == null ? 0 : Value.GetHashCode());
        }
        public static bool operator ==(WildcardUriInfo left, WildcardUriInfo right) {
            if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
            return left.Equals(right);
        }
        public static bool operator !=(WildcardUriInfo left, WildcardUriInfo right) { return !(left == right); }
        private volatile string _text;
        public override string ToString() {
            if (_text == null) {
                switch (Kind) {
                    case WildcardUriKind.Any: _text = "any"; break;
                    case WildcardUriKind.Other: _text = "!{" + Value.ToString() + "}"; break;
                    case WildcardUriKind.Specific: _text = "{" + Value.ToString() + "}"; break;
                    case WildcardUriKind.Unqualified: _text = "unqualified"; break;
                    default: throw new InvalidOperationException();
                }
            }
            return _text;
        }
    }
    public enum WildcardValidation {
        SkipValidate = XmlSchemaContentProcessing.Skip,//1
        TryValidate = XmlSchemaContentProcessing.Lax,//2
        MustValidate = XmlSchemaContentProcessing.Strict,//3
    }
    [Serializable]
    public sealed class WildcardInfo {
        public WildcardInfo(ProgramInfo program, IReadOnlyList<WildcardUriInfo> uris, WildcardValidation validation) {
            if (program == null) throw new ArgumentNullException("program");
            if (uris == null) throw new ArgumentNullException("uris");
            Program = program;
            Uris = uris;
            Validation = validation;
        }
        public readonly ProgramInfo Program;
        public readonly IReadOnlyList<WildcardUriInfo> Uris;
        public readonly WildcardValidation Validation;
        public bool IsMatch(XNamespace value) {
            foreach (var uri in Uris)
                if (uri.IsMatch(value)) return true;
            return false;
        }
        public bool IsMatch(XNamespace value, Context context, Object diagSource, DiagnosticCode diagKind) {
            if (IsMatch(value)) return true;
            new Diagnostic(context, diagSource, diagKind, value, UrisText);
            return false;
        }
        private volatile string _urisText;
        public string UrisText { get { return _urisText ?? (_urisText = GetUrisText(Uris)); } }
        public static string GetUrisText(IReadOnlyList<WildcardUriInfo> uris) {
            if (uris == null) throw new ArgumentNullException("uris");
            if (uris.Count == 1) return uris[0].ToString();
            else {
                var sb = new StringBuilder();
                for (var i = 0; i < uris.Count; i++) {
                    if (i > 0) sb.Append(", ");
                    sb.Append(uris[i].ToString());
                }
                return sb.ToString();
            }
        }
    }
    [Serializable]
    public sealed class DefaultOrFixedValueInfo {
        public DefaultOrFixedValueInfo(bool isDefault, object value, string valueText) {
            if (value == null) throw new ArgumentNullException("value");
            if (valueText == null) throw new ArgumentNullException("valueText");
            IsDefault = isDefault;
            Value = value;
            ValueText = valueText;
        }
        public readonly bool IsDefault;
        public bool IsFixed { get { return !IsDefault; } }
        public readonly object Value;
        public readonly string ValueText;
    }
    [Serializable]
    public sealed class AttributeSetInfo : ObjectInfo {
        public AttributeSetInfo(SType clrType, IEnumerable<AttributeInfo> attributes, WildcardInfo wildcard, bool isOptional)
            : base(clrType) {
            var attributeDict = new Dictionary<XName, AttributeInfo>();
            if (attributes != null)
                foreach (var attribute in attributes)
                    attributeDict.Add(attribute.Name, attribute);
            AttributeDict = attributeDict;
            Wildcard = wildcard;
            IsOptional = isOptional;
        }
        public readonly IReadOnlyDictionary<XName, AttributeInfo> AttributeDict;
        public IEnumerable<AttributeInfo> Attributes { get { return AttributeDict.Values; } }
        public readonly WildcardInfo Wildcard;//opt
        public readonly bool IsOptional;
    }
    public enum EntityDeclarationKind { Local, Global, Reference }
    [Serializable]
    public sealed class AttributeInfo : ObjectInfo {
        public AttributeInfo(SType clrType, EntityDeclarationKind kind, XName name, string memberName, bool isOptional, DefaultOrFixedValueInfo defaultOrFixedValue,
            SimpleTypeInfo type)
            : base(clrType) {
            if (name == null) throw new ArgumentNullException("name");
            if (type == null) throw new ArgumentNullException("type");
            Kind = kind;
            _name = name;
            MemberName = memberName;
            IsOptional = isOptional;
            DefaultOrFixedValue = defaultOrFixedValue;
            _type = type;
        }
        public AttributeInfo(SType clrType, AttributeInfo referentialAttribute, string memberName, bool isOptional, DefaultOrFixedValueInfo defaultOrFixedValue)
            : base(clrType) {
            if (referentialAttribute == null) throw new ArgumentNullException("referentialAttribute");
            if (memberName == null) throw new ArgumentNullException("memberName");
            Kind = EntityDeclarationKind.Reference;
            ReferentialAttribute = referentialAttribute;
            MemberName = memberName;
            IsOptional = isOptional;
            DefaultOrFixedValue = defaultOrFixedValue;
        }
        public readonly EntityDeclarationKind Kind;
        public bool IsLocal { get { return Kind == EntityDeclarationKind.Local; } }
        public bool IsGlobal { get { return Kind == EntityDeclarationKind.Global; } }
        public bool IsReference { get { return Kind == EntityDeclarationKind.Reference; } }
        public readonly AttributeInfo ReferentialAttribute;//for att ref
        private readonly XName _name;
        public XName Name { get { return IsReference ? ReferentialAttribute._name : _name; } }
        public string LocalName { get { return Name.LocalName; } }
        public readonly string MemberName;//for local att & att ref
        public readonly bool IsOptional;
        public readonly DefaultOrFixedValueInfo DefaultOrFixedValue;//opt
        private readonly SimpleTypeInfo _type;
        public SimpleTypeInfo Type { get { return IsReference ? ReferentialAttribute._type : _type; } }
    }
    [Serializable]
    public abstract class ChildInfo : ObjectInfo {
        protected ChildInfo(SType clrType, string memberName, bool isEffectiveOptional)
            : base(clrType) {
            MemberName = memberName;
            IsEffectiveOptional = isEffectiveOptional;
        }
        public readonly string MemberName;//null for list item, global element, root child struct
        public readonly bool IsEffectiveOptional;
    }
    [Serializable]
    public abstract class ElementBaseInfo : ChildInfo {
        protected ElementBaseInfo(SType clrType, string memberName, bool isEffectiveOptional) : base(clrType, memberName, isEffectiveOptional) { }
    }
    [Serializable]
    public sealed class ElementInfo : ElementBaseInfo {
        public ElementInfo(SType clrType, string memberName, bool isEffectiveOptional, EntityDeclarationKind kind, ProgramInfo program, XName name,
            bool isAbstract, bool isNullable, InstanceProhibition instanceProhibition, DefaultOrFixedValueInfo defaultOrFixedValue, TypeInfo type,
            ElementInfo substitutedElement, IReadOnlyList<GlobalObjectRefInfo> directSubstitutingElementRefs, IReadOnlyList<IdentityConstraintInfo> identityConstraints)
            : base(clrType, memberName, isEffectiveOptional) {
            if (program == null) throw new ArgumentNullException("program");
            if (name == null) throw new ArgumentNullException("name");
            if (type == null) throw new ArgumentNullException("type");
            Kind = kind;
            _program = program;
            _name = name;
            _isAbstract = isAbstract;
            _isNullable = isNullable;
            _instanceProhibition = instanceProhibition;
            _defaultOrFixedValue = defaultOrFixedValue;
            _type = type;
            _substitutedElement = substitutedElement;
            _directSubstitutingElementRefs = directSubstitutingElementRefs;
            _identityConstraints = identityConstraints;
        }
        public ElementInfo(SType clrType, string memberName, bool isEffectiveOptional, ElementInfo referentialElement)
            : base(clrType, memberName, isEffectiveOptional) {
            if (referentialElement == null) throw new ArgumentNullException("referentialElement");
            Kind = EntityDeclarationKind.Reference;
            ReferentialElement = referentialElement;
        }
        public readonly EntityDeclarationKind Kind;
        public bool IsLocal { get { return Kind == EntityDeclarationKind.Local; } }
        public bool IsGlobal { get { return Kind == EntityDeclarationKind.Global; } }
        public bool IsReference { get { return Kind == EntityDeclarationKind.Reference; } }
        public readonly ElementInfo ReferentialElement;//for element ref
        private readonly ProgramInfo _program;
        public ProgramInfo Program { get { return IsReference ? ReferentialElement._program : _program; } }
        private readonly XName _name;
        public XName Name { get { return IsReference ? ReferentialElement._name : _name; } }
        public string LocalName { get { return Name.LocalName; } }
        private readonly bool _isAbstract;
        public bool IsAbstract { get { return IsReference ? ReferentialElement._isAbstract : _isAbstract; } }
        private readonly bool _isNullable;
        public bool IsNullable { get { return IsReference ? ReferentialElement._isNullable : _isNullable; } }
        private readonly InstanceProhibition _instanceProhibition;
        public InstanceProhibition InstanceProhibition { get { return IsReference ? ReferentialElement._instanceProhibition : _instanceProhibition; } }
        private readonly DefaultOrFixedValueInfo _defaultOrFixedValue;//opt
        public DefaultOrFixedValueInfo DefaultOrFixedValue { get { return IsReference ? ReferentialElement._defaultOrFixedValue : _defaultOrFixedValue; } }
        private readonly TypeInfo _type;
        public TypeInfo Type { get { return IsReference ? ReferentialElement._type : _type; } }
        private readonly ElementInfo _substitutedElement;//opt, for global element
        public ElementInfo SubstitutedElement { get { return IsReference ? ReferentialElement._substitutedElement : _substitutedElement; } }
        private readonly IReadOnlyList<GlobalObjectRefInfo> _directSubstitutingElementRefs;//opt, for global element
        private volatile IReadOnlyList<ElementInfo> _directSubstitutingElements;
        public IReadOnlyList<ElementInfo> DirectSubstitutingElements {
            get {
                if (IsReference) return ReferentialElement.DirectSubstitutingElements;
                if (_directSubstitutingElements != null) return _directSubstitutingElements;
                if (_directSubstitutingElementRefs == null) return null;
                return _directSubstitutingElements = _directSubstitutingElementRefs.Select(i => (ElementInfo)i.GetValue(false)).ToList();
            }
        }
        private readonly IReadOnlyList<IdentityConstraintInfo> _identityConstraints;//opt
        public IReadOnlyList<IdentityConstraintInfo> IdentityConstraints { get { return IsReference ? ReferentialElement._identityConstraints : _identityConstraints; } }
        public ElementInfo TryGet(XName name) {
            if (IsReference) return ReferentialElement.TryGet(name);
            if (name == null) throw new ArgumentNullException("name");
            if (name == _name) return this;
            if (IsGlobal) {
                var directSubstitutingElements = DirectSubstitutingElements;
                if (directSubstitutingElements != null) {
                    foreach (var i in directSubstitutingElements) {
                        var info = i.TryGet(name);
                        if (info != null) return info;
                    }
                }
            }
            return null;
        }
    }
    [Serializable]
    public sealed class IdentityConstraintInfo {
        public IdentityConstraintInfo(IdentityConstraintKind kind, XName name, XName referentialName, bool isSplitListValue,
            PathExpressionInfo identityPathExpression, IReadOnlyList<PathExpressionInfo> valuePathExpressions) {
            if (name == null) throw new ArgumentNullException("name");
            if (kind == IdentityConstraintKind.KeyRef) {
                if (referentialName == null) throw new ArgumentNullException("referentialName");
            }
            else if (referentialName != null) throw new ArgumentException("referentialName != null");
            if (identityPathExpression == null) throw new ArgumentNullException("identityPathExpression");
            if (valuePathExpressions == null || valuePathExpressions.Count == 0) throw new ArgumentNullException("valuePathExpressions");
            Kind = kind;
            Name = name;
            ReferentialName = referentialName;
            IsSplitListValue = isSplitListValue;
            IdentityPathExpression = identityPathExpression;
            ValuePathExpressions = valuePathExpressions;
        }
        public readonly IdentityConstraintKind Kind;
        public readonly XName Name;
        public string LocalName { get { return Name.LocalName; } }
        public bool IsKey { get { return Kind == IdentityConstraintKind.Key; } }
        public bool IsUnique { get { return Kind == IdentityConstraintKind.Unique; } }
        public bool IsKeyRef { get { return Kind == IdentityConstraintKind.KeyRef; } }
        public readonly XName ReferentialName;//for KeyRef
        public readonly bool IsSplitListValue;//for KeyRef
        public readonly PathExpressionInfo IdentityPathExpression;
        public readonly IReadOnlyList<PathExpressionInfo> ValuePathExpressions;
    }
    [Serializable]
    public sealed class PathExpressionInfo {
        public PathExpressionInfo(IReadOnlyList<PathInfo> paths) {
            if (paths == null || paths.Count == 0) throw new ArgumentNullException("paths");
            Paths = paths;
        }
        public readonly IReadOnlyList<PathInfo> Paths;
    }
    [Serializable]
    public sealed class PathInfo {
        public PathInfo(IReadOnlyList<StepInfo> steps) {
            if (steps == null || steps.Count == 0) throw new ArgumentNullException("steps");
            Steps = steps;
        }
        public readonly IReadOnlyList<StepInfo> Steps;
    }
    public enum StepKind { Self, SelfAndDescendants, Descendants, ChildrenOrAttributes, Uri, Name }
    [Serializable]
    public sealed class StepInfo {
        public StepInfo(StepKind kind, bool isAttribute, XNamespace uri, XName name) {
            if (kind == StepKind.Uri) {
                if (uri == null || name != null) throw new ArgumentException("uri == null || name != null");
            }
            else if (kind == StepKind.Name) {
                if (uri != null || name == null) throw new ArgumentException("uri != null || name == null");
            }
            else {
                if (uri != null || name != null) throw new ArgumentException("uri != null || name != null");
            }
            Kind = kind;
            IsAttribute = isAttribute;
            Uri = uri;
            Name = name;
        }
        public readonly StepKind Kind;
        public readonly bool IsAttribute;
        public readonly XNamespace Uri;//opt
        public readonly XName Name;//opt
    }
    //
    [Serializable]
    public sealed class ElementWildcardInfo : ElementBaseInfo {
        public ElementWildcardInfo(SType clrType, string memberName, bool isEffectiveOptional, ProgramInfo program, WildcardInfo wildcard)
            : base(clrType, memberName, isEffectiveOptional) {
            if (program == null) throw new ArgumentNullException("program");
            if (wildcard == null) throw new ArgumentNullException("wildcard");
            Program = program;
            Wildcard = wildcard;
        }
        public readonly ProgramInfo Program;
        public readonly WildcardInfo Wildcard;
    }
    public enum ChildContainerKind { Seq, Choice, Unordered, List }
    [Serializable]
    public abstract class ChildContainerInfo : ChildInfo {
        protected ChildContainerInfo(SType clrType, string memberName, bool isEffectiveOptional, ChildContainerKind kind, bool isMixed)
            : base(clrType, memberName, isEffectiveOptional) {
            Kind = kind;
            IsMixed = isMixed;
        }
        public readonly ChildContainerKind Kind;
        public readonly bool IsMixed;
    }
    [Serializable]
    public sealed class ChildListInfo : ChildContainerInfo {
        public ChildListInfo(SType clrType, string memberName, bool isEffectiveOptional, bool isMixed, ulong minOccurs, ulong maxOccurs, ChildInfo item)
            : base(clrType, memberName, isEffectiveOptional, ChildContainerKind.List, isMixed) {
            if (item == null) throw new ArgumentNullException("item");
            MinOccurs = minOccurs;
            MaxOccurs = maxOccurs;
            Item = item;
        }
        public readonly ulong MinOccurs;
        public readonly ulong MaxOccurs;
        public readonly ChildInfo Item;
    }
    [Serializable]
    public sealed class ChildStructInfo : ChildContainerInfo {
        public ChildStructInfo(SType clrType, string memberName, bool isEffectiveOptional, ChildContainerKind kind, bool isMixed, bool isRoot, bool hasElementBases,
            IReadOnlyList<ChildInfo> members)
            : base(clrType, memberName, isEffectiveOptional, kind, isMixed) {
            if (members == null) throw new ArgumentNullException("members");
            IsRoot = isRoot;
            HasElementBases = hasElementBases;
            Members = members;
        }
        public readonly bool IsRoot;
        public readonly bool HasElementBases;
        public readonly IReadOnlyList<ChildInfo> Members;
        public IEnumerable<ChildInfo> NonNullMembers { get { return Members.Where(i => i != null); } }
    }

    #endregion
}

namespace Metah.X.Extensions {
    public static class ExtensionMethods {
        public static bool IsAssignableTo(SType to, SType from, bool @try) {
            if (to == null) throw new ArgumentNullException("to");
            if (to.IsAssignableFrom(from)) return true;
            if (@try) return false;
            throw new InvalidOperationException("Invalid object clr type '{0}', expecting '{1}' or it's base type".InvariantFormat(to.FullName, from.FullName));
        }
        public static object CreateInstance(SType type) { return Activator.CreateInstance(type, true); }
        public static string InvariantFormat(this string format, params object[] args) { return string.Format(CultureInfo.InvariantCulture, format, args); }
        public static bool IsEmpty(this XNamespace xnamespace) {
            if (xnamespace == null) throw new ArgumentNullException("xnamespace");
            return xnamespace == XNamespace.None;
        }
        public static bool IsUnqualified(this XName name) {
            if (name == null) throw new ArgumentNullException("name");
            return name.Namespace.IsEmpty();
        }
        public static bool IsQualified(this XName name) { return !name.IsUnqualified(); }
        public static int AggregateHash(int hash, int newValue) { unchecked { return hash * 31 + newValue; } }
        public static int CombineHash(int a, int b) {
            unchecked {
                int hash = 17;
                hash = hash * 31 + a;
                hash = hash * 31 + b;
                return hash;
            }
        }
        public static int CombineHash(int a, int b, int c) {
            unchecked {
                int hash = 17;
                hash = hash * 31 + a;
                hash = hash * 31 + b;
                hash = hash * 31 + c;
                return hash;
            }
        }
        public static TValue TryGetValue<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dict, TKey key) where TValue : class {
            if (dict == null) throw new ArgumentNullException("dict");
            TValue value;
            if (dict.TryGetValue(key, out value)) return value;
            return null;
        }
        public static void CopyTo<T>(IReadOnlyList<T> list, T[] array, int arrayIndex) {
            if (list == null) throw new ArgumentNullException("list");
            if (array == null) throw new ArgumentNullException("array");
            if (arrayIndex < 0 || arrayIndex > array.Length) throw new ArgumentOutOfRangeException("arrayIndex");
            if (array.Length - arrayIndex < list.Count) throw new ArgumentException("Insufficient array space");
            for (var i = 0; i < list.Count; i++)
                array[arrayIndex++] = list[i];
        }
        public static string SourceUri(this Location? location) { return location.GetValueOrDefault().SourceUri; }
        public static int Line(this Location? location) { return location.GetValueOrDefault().Line; }
        public static int Column(this Location? location) { return location.GetValueOrDefault().Column; }
        public static bool IsSet(this InstanceProhibition value, InstanceProhibition flag) { return (value & flag) != 0; }
        public static XName ToSystemName(this TypeKind typeKind) { return NamespaceInfo.SystemUri.GetName(typeKind.ToString()); }
        public static string ToXsName(this TypeKind typeKind) { return NamespaceInfo.TypeKindToXsNameDict.TryGetValue(typeKind); }
        //
        public static IEnumerable<T> ElementAncestors<T>(this IEnumerable<Child> source, Func<Element, bool> filter = null) where T : Element {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var child in source)
                foreach (var i in child.ElementAncestors<T>(filter))
                    yield return i;
        }
        public static IEnumerable<T> ElementAncestors<T>(this IEnumerable<Child> source, XName name) where T : Element {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var child in source)
                foreach (var i in child.ElementAncestors<T>(name))
                    yield return i;
        }
        public static IEnumerable<T> SelfAndElementAncestors<T>(this IEnumerable<Element> source, Func<Element, bool> filter = null) where T : Element {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var element in source)
                foreach (var i in element.SelfAndElementAncestors<T>(filter))
                    yield return i;
        }
        public static IEnumerable<T> SelfAndElementAncestors<T>(this IEnumerable<Element> source, XName name) where T : Element {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var element in source)
                foreach (var i in element.SelfAndElementAncestors<T>(name))
                    yield return i;
        }
        public static IEnumerable<T> Attributes<T>(this IEnumerable<Element> source, Func<T, bool> filter = null) where T : Attribute {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var element in source)
                foreach (var i in element.Attributes<T>(filter))
                    yield return i;
        }
        public static IEnumerable<T> Attributes<T>(this IEnumerable<Element> source, XName name) where T : Attribute {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var element in source)
                foreach (var i in element.Attributes<T>(name))
                    yield return i;
        }
        public static IEnumerable<T> ContentChildren<T>(this IEnumerable<Element> source, Func<T, bool> filter = null) where T : ContentChild {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var element in source)
                foreach (var i in element.ContentChildren<T>(filter))
                    yield return i;
        }
        public static IEnumerable<T> ContentDescendants<T>(this IEnumerable<Element> source, Func<T, bool> filter = null) where T : ContentChild {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var element in source)
                foreach (var i in element.ContentDescendants<T>(filter))
                    yield return i;
        }
        public static IEnumerable<T> SelfAndContentDescendants<T>(this IEnumerable<Element> source, Func<T, bool> filter = null) where T : ContentChild {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var element in source)
                foreach (var i in element.SelfAndContentDescendants<T>(filter))
                    yield return i;
        }
        public static IEnumerable<T> ElementChildren<T>(this IEnumerable<Element> source, Func<T, bool> filter = null) where T : Element {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var element in source)
                foreach (var i in element.ElementChildren<T>(filter))
                    yield return i;
        }
        public static IEnumerable<T> ElementDescendants<T>(this IEnumerable<Element> source, Func<T, bool> filter = null) where T : Element {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var element in source)
                foreach (var i in element.ElementDescendants<T>(filter))
                    yield return i;
        }
        public static IEnumerable<T> SelfAndElementDescendants<T>(this IEnumerable<Element> source, Func<T, bool> filter = null) where T : Element {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var element in source)
                foreach (var i in element.SelfAndElementDescendants<T>(filter))
                    yield return i;
        }
        public static IEnumerable<T> ElementChildren<T>(this IEnumerable<Element> source, XName name) where T : Element {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var element in source)
                foreach (var i in element.ElementChildren<T>(name))
                    yield return i;
        }
        public static IEnumerable<T> ElementDescendants<T>(this IEnumerable<Element> source, XName name) where T : Element {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var element in source)
                foreach (var i in element.ElementDescendants<T>(name))
                    yield return i;
        }
        public static IEnumerable<T> SelfAndElementDescendants<T>(this IEnumerable<Element> source, XName name) where T : Element {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var element in source)
                foreach (var i in element.SelfAndElementDescendants<T>(name))
                    yield return i;
        }
    }
}