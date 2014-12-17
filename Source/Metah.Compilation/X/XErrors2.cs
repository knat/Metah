using System;

namespace Metah.Compilation.X {
    internal sealed class CompilationContext : CompilationContextBase {
        private CompilationContext() { }
        public static string GetErrorMessageFormat(int code) {
            var kind = (ErrorKind)code;
            switch (kind) {
                case ErrorKind.CodeNotAllowedInSchemaOnlyFile: return "C# code not allowed in schema-only file";
                case ErrorKind.CannotAddClassToParent: return "Cannot add C# class to parent";
                case ErrorKind.NestedClassNameEqualToParent: return "Nested C# class name '{0}' equal to parent. Try specify another member name";
                case ErrorKind.ParameterlessConstructorRequired: return "Parameterless constructor required";
                case ErrorKind.DuplicateClassAlias: return "Duplicate class alias '{0}'";
                case ErrorKind.CSNamespaceRequired: return "C# namespace required";
                case ErrorKind.CSNamespaceNotEqualTo: return "Object merge error: C# namespace '{0}' not equal to '{1}'";
                case ErrorKind.DuplicateAnnotation: return "Duplicate annotation '{0}'";
                case ErrorKind.AnnotationNotAllowed: return "Annotation '{0}' not allowed";
                case ErrorKind.InvalidNamespaceUri: return "Invalid namespace uri";
                case ErrorKind.NamespaceUriReserved: return "Namespace uri '{0}' reserved";
                case ErrorKind.DuplicateUriAlias: return "Duplicate uri alias '{0}'";
                case ErrorKind.InvalidUriAlias: return "Invalid uri alias '{0}'";
                case ErrorKind.NamespaceImportAliasReserved: return "Namespace import alias '{0}' reserved";
                case ErrorKind.DuplicateNamespaceImportAlias: return "Duplicate namespace import alias '{0}'";
                case ErrorKind.DuplicateNamespaceImportUri: return "Duplicate namespace import uri '{0}'";
                case ErrorKind.InvalidNamespaceImportUri: return "Invalid namespace import uri '{0}'";
                case ErrorKind.InvalidQualifiableNameAlias: return "Invalid qualifiable name alias '{0}'";
                case ErrorKind.AmbiguousQualifiableName: return "Ambiguous qualifiable name '{0}'";
                case ErrorKind.InvalidQualifiableName: return "Invalid qualifiable name '{0}'";
                case ErrorKind.QualifiableNameNotEqualTo: return "Object merge error: qualifiable name '{0}' not equal to '{1}'";
                case ErrorKind.DerivationProhibitionNotEqualTo: return "Object merge error: derivation prohibition '{0}' not equal to '{1}'";
                case ErrorKind.InstanceProhibitionNotEqualTo: return "Object merge error: instance prohibition '{0}' not equal to '{1}'";
                case ErrorKind.ElementQualificationNotEqualTo: return "Object merge error: element qualification '{0}' not equal to '{1}'";
                case ErrorKind.AttributeQualificationNotEqualTo: return "Object merge error: attribute qualification '{0}' not equal to '{1}'";
                //
                case ErrorKind.CircularReferenceDetected: return "Circular reference detected";
                case ErrorKind.NameRequiredForGlobalType: return "Name required for global type";
                case ErrorKind.NameNotAllowedForLocalType: return "Name not allowed for local type";
                case ErrorKind.DerivationProhibitionNotAllowedForLocalType: return "Derivation prohibition not allowed for local type";
                case ErrorKind.AbstractNotAllowedForLocalType: return "Abstract not allowed for local type";
                case ErrorKind.LocalTypeAndTypeReferenceNotCompatible: return "Object merge error: local type and type reference not compatible";
                case ErrorKind.TypeNotCompatibleWith: return "Object merge error: type '{0}' not compatible with '{1}'";
                case ErrorKind.SpecificTypeRequired: return "Specific type required";
                case ErrorKind.TypeRequired: return "Type required";
                case ErrorKind.SimpleTypeRequired: return "Simple type required";
                case ErrorKind.CannotExtendOrRestrictSystemRootType: return "Cannot extend or restrict system root type";
                case ErrorKind.CannotRestrictSystemRootSimpleType: return "Cannot restrict system root simple type";
                case ErrorKind.AbstractNotAllowedForSimpleType: return "Abstract not allowed for simple type";
                case ErrorKind.MixedNotAllowedForSimpleType: return "Mixed not allowed for simple type";
                case ErrorKind.InstanceProhibitionNotAllowedForSimpleType: return "Instance prohibition not allowed for simple type";
                case ErrorKind.AttributeSetNotAllowedForSimpleType: return "Attribute set not allowed for simple type";
                case ErrorKind.ChildrenNotAllowedForSimpleType: return "Children not allowed for simple type";
                case ErrorKind.CodeNotAllowedInFacetSetForSimpleType: return "Code not allowed in facet set for simple type. Move them to type declaration block";
                case ErrorKind.BaseTypeOfComplexTypeCannotBeLocal: return "Base type of complex type cannot be local";
                case ErrorKind.MixedNotAllowedForSimpleChildComplexType: return "Mixed not allowed for simple child complex type";
                case ErrorKind.ChildStructNotAllowedForSimpleChildComplexType: return "Child struct not allowed for simple child complex type";
                case ErrorKind.FacetSetNotAllowedForComplexChildComplexType: return "Facet set not allowed for complex child complex type";
                case ErrorKind.ExtensionDerivationProhibited: return "Extension derivation prohibited";
                case ErrorKind.RestrictionDerivationProhibited: return "Restriction derivation prohibited";
                case ErrorKind.ListDerivationProhibited: return "List derivation prohibited";
                case ErrorKind.UnionDerivationProhibited: return "Union derivation prohibited";
                case ErrorKind.ListedSimpleTypeItemCannotBeList: return "Listed simple type item cannot be list";
                case ErrorKind.UnitedSimpleTypeMemberTypeReferenceMustPrecedeLocalType: return "United simple type member type reference must precede local type";
                case ErrorKind.CannotRestrictNullBaseAttributeSet: return "Cannot restrict null base attribute set";
                case ErrorKind.CannotRestrictNullBaseChildStruct: return "Cannot restrict null base child struct";
                case ErrorKind.MixedNotEqualToBase: return "Mixed '{0}' not equal to base '{1}'";
                //
                case ErrorKind.InvalidSimpleTypeLiteral: return "Invalid simple type literal:{0}";
                //case ErrorKind.QualifiableNameLiteralCannotBeString: return "Qualifiable name literal cannot be string";
                case ErrorKind.ComplexChildComplexTypeLiteralMustBeString: return "Complex child complex type literal must be string";
                case ErrorKind.InvalidInteger: return "Invalid integer";
                case ErrorKind.FacetNotEqualTo: return "Object merge error: facet {0} '{1}' not equal to '{2}'";
                case ErrorKind.DuplicateFacet: return "Duplicate facet";
                case ErrorKind.FacetNotApplicable: return "Facet '{0}' not applicable to type '{1}'";
                case ErrorKind.MaxLengthMustGreaterThanOrEqualToMinLength: return "Max length '{0}' must greater than or equal to min length '{1}'";
                case ErrorKind.MinLengthMustGreaterThanOrEqualToBaseMinLength: return "Min length '{0}' must greater than or equal to base min length '{1}'";
                case ErrorKind.MinLengthMustEqualToBaseMinLengthIfBaseIsFixed: return "Min length '{0}' must equal to base min length '{1}' if base min length is fixed";
                case ErrorKind.MaxLengthMustLessThanOrEqualToBaseMaxLength: return "Max length '{0}' must less than or equal to base max length '{1}'";
                case ErrorKind.MaxLengthMustEqualToBaseMaxLengthIfBaseIsFixed: return "Max length '{0}' must equal to base max length '{1}' if base max length is fixed";
                case ErrorKind.MaxLengthMustGreaterThanOrEqualToBaseMinLength: return "Max length '{0}' must greater than or equal to base min length '{1}'";
                case ErrorKind.MinLengthMustLessThanOrEqualToBaseMaxLength: return "Min length '{0}' must less than or equal to base max length '{1}'";
                case ErrorKind.TotalDigitsMustGreaterThanZero: return "Total digits must greater than zero";
                case ErrorKind.FractionDigitsMustLessThanOrEqualToTotalDigits: return "Fraction digits '{0}' must less than or equal to total digits '{1}'";
                case ErrorKind.TotalDigitsMustLessThanOrEqualToBaseTotalDigits: return "Total digits '{0}' must less than or equal to base total digits '{1}'";
                case ErrorKind.TotalDigitsMustEqualToBaseTotalDigitsIfBaseIsFixed: return "Total digits '{0}' must equal to base total digits '{1}' if base total digits is fixed";
                case ErrorKind.FractionDigitsMustLessThanOrEqualToBaseFractionDigits: return "Fraction digits '{0}' must less than or equal to base fraction digits '{1}'";
                case ErrorKind.FractionDigitsMustEqualToBaseFractionDigitsIfBaseIsFixed: return "Fraction digits '{0}' must equal to base fraction digits '{1}' if base fraction digits is fixed";
                case ErrorKind.FractionDigitsMustLessThanOrEqualToBaseTotalDigits: return "Fraction digits '{0}' must less than or equal to base total digits '{1}'";
                case ErrorKind.TotalDigitsMustGreaterThanOrEqualToBaseFractionDigits: return "Total digits '{0}' must greater than or equal to base fraction digits '{1}'";
                case ErrorKind.LowerValueMustEqualToBaseLowerValueIfBaseIsFixed: return "Lower value '{0}' must equal to base lower value '{1}' if base lower is fixed";
                case ErrorKind.UpperValueMustEqualToBaseUpperValueIfBaseIsFixed: return "Upper value '{0}' must equal to base upper value '{1}' if base upper is fixed";
                case ErrorKind.UpperValueMustGreaterThanOrEqualToLowerValue: return "Upper value '{0}' must greater than or equal to lower value '{1}'";
                case ErrorKind.LowerMustBeInclusiveIfLowerValueEqualToUpperValue: return "Lower must be inclusive if lower value equal to upper value";
                case ErrorKind.UpperMustBeInclusiveIfLowerValueEqualToUpperValue: return "Upper must be inclusive if lower value equal to upper value";
                case ErrorKind.DuplicateEnumerationsItemName: return "Duplicate enumerations item name '{0}'";
                case ErrorKind.InvalidFacetPattern: return "Invalid facet pattern '{0}'";
                case ErrorKind.WhitespaceNormalizationMustStrongerThanOrEqualToBaseWhitespaceNormalization: return "Whitespace normalization '{0}' must stronger than or equal to base whitespace normalization '{1}'";
                case ErrorKind.WhitespaceNormalizationMustEqualToBaseWhitespaceNormalizationIfBaseIsFixed: return "Whitespace normalization '{0}' must equal to base whitespace normalization '{1}' if base whitespace normalization is fixed";
                //
                case ErrorKind.NameRequired: return "Name required";
                case ErrorKind.QualifiableNameRequired: return "Qualifiable name required";
                case ErrorKind.NameNotEqualTo: return "Object merge error: name '{0}' not equal to '{1}'";
                case ErrorKind.NameOrMemberNameRequired: return "Name or member name required";
                case ErrorKind.DuplicateMemberName: return "Duplicate member name '{0}'";
                case ErrorKind.UnexpectedMemberName: return "Object merge error: unexpected member name '{0}'";
                case ErrorKind.ObjectNotCompatibleWith: return "Object merge error: '{0}' not compatible with '{1}'";
                case ErrorKind.DefaultOrFixedValueNotEqaulTo: return "Object merge error: default or fixed value '{0}' not eqaul to '{1}'";
                case ErrorKind.DuplicateWildcardUri: return "Duplicate wildcard uri '{0}'";
                case ErrorKind.InvalidWildcardUris: return "Invalid wildcard uris";
                case ErrorKind.WildcardRequired: return "Wildcard required";
                case ErrorKind.WildcardNotEqualTo: return "Object merge error: wildcard '{0}' not equal to '{1}'";
                case ErrorKind.RestrictingWildcardNotEqualToOrRestrictedThanRestricted: return "Restricting wildcard '{0}' not equal to or restricted than restricted wildcard '{1}'";
                case ErrorKind.CannotUniteWildcardWith: return "Cannot unite wildcard '{0}' with '{1}'";
                //
                case ErrorKind.MemberNameNotAllowedForGlobalAttribute: return "Member name not allowed for global attribute";
                case ErrorKind.DuplicateAttributeFullName: return "Duplicate attribute full name '{0}'";
                case ErrorKind.RestrictingAttributeMemberNameNotEqualToRestrictedAttribute: return "Restricting attribute member name '{0}' not equal to restricted attribute member name '{1}'";
                case ErrorKind.RestrictingAttributeIsOptionalButRestrictedAttributeIsRequired: return "Restricting attribute is optional but restricted attribute is required";
                case ErrorKind.RestrictedAttributeNotFoundAndNoBaseWildcard: return "Restricted attribute with full name '{0}' not found and no base wildcard";
                case ErrorKind.RestrictingAttributeNamespaceNotMatchWithBaseWildcard: return "Restricting attribute namespace '{0}' not match with base wildcard '{1}'";
                case ErrorKind.RestrictingAttributeTypeNotEqualToOrRestrictedDeriveFromRestrictedAttributeType: return "Restricting attribute type not equal to or restricted derive from restricted attribute type";
                case ErrorKind.AttributeHasDefaultValueMustBeOptional: return "Attribute has default value must be optional";
                case ErrorKind.RequiredAttributeNotRestricting: return "Required attribute with full name '{0}' not restricting";
                case ErrorKind.RestrictedAttributesWildcardNotFound: return "Restricted attributes wildcard not found";
                case ErrorKind.RestrictedAttributeAndRestrictingAttributeMustBothHasFixedValueOrNeither: return "Restricted attribute and restricting attribute must both has fixed value or neither";
                case ErrorKind.RestrictedAttributeFixedValueNotEqualToRestrictingAttributeFixedValue: return "Restricted attribute fixed value '{0}' not equal to restricting attribute fixed value '{1}'";
                case ErrorKind.IfGlobalAttributeHasFixedValueAttributeReferenceMustHasFixedValueOrAbsent: return "If global attribute has fixed value, attribute reference must has fixed value or absent";
                case ErrorKind.AttributeReferenceFixedValueNotEqualToGlobalAttributeFixedValue: return "Attribute reference fixed value '{0}' not equal to global attribute fixed value '{1}'";
                //
                case ErrorKind.OccursNotEqualTo: return "Object merge error: occurs '{0}' not equal to '{1}'";
                case ErrorKind.MaxOccursMustGreaterThanZero: return "Max occurs must greater than zero";
                case ErrorKind.MaxOccursMustGreaterThanOrEqualToMinOccurs: return "Max occurs '{0}' must greater than or equal to min occurs '{1}'";
                case ErrorKind.ListCodeNotAllowedForNonListChild: return "List code not allowed for non list child";
                case ErrorKind.UnorderedChildStructMustBeDirectMemberOfChildren: return "Unordered child struct must be direct member of children";
                case ErrorKind.UnorderedChildStructMustBeTheOnlyMemberOfChildren: return "Unordered child struct must be the only member of children";
                case ErrorKind.UnorderedChildStructOrMemberMaxOccursMustBeOne: return "Unordered child struct or member max occurs must be one";
                case ErrorKind.UnorderedChildStructMemberMustBeElementOrElementReference: return "Unordered child struct member must be element or element reference";
                case ErrorKind.DuplicateElementFullNameInUnorderedChildStruct: return "Duplicate element full name '{0}' in unordered child struct";
                case ErrorKind.NameRequiredForGlobalChildStruct: return "Name required for global child struct";
                case ErrorKind.MemberNameNotAllowedForGlobalChildStruct: return "Member name not allowed for global child struct";
                case ErrorKind.OccursNotAllowedForGlobalChildStruct: return "Occurs not allowed for global child struct";
                case ErrorKind.CodeNotAllowedForGlobalChildStruct: return "Code not allowed for global child struct";
                case ErrorKind.NameNotAllowedForLocalChildStruct: return "Name not allowed for local child struct";
                case ErrorKind.OccursNotAllowedForGlobalElement: return "Occurs not allowed for global element";
                case ErrorKind.MemberNameNotAllowedForGlobalElement: return "Member name not allowed for global element";
                case ErrorKind.RestrictedChildNotFound: return "Restricted child with member name '{0}' not found";
                case ErrorKind.RestrictingChildMinOccursNotEqualToOrGreaterThanRestrictedChild: return "Restricting child min occurs '{0}' not equal to or greater than restricted child '{1}'";
                case ErrorKind.RestrictingChildMaxOccursNotEqualToOrLessThanRestrictedChild: return "Restricting child max occurs '{0}' not equal to or less than restricted child '{1}'";
                case ErrorKind.RestrictedChildIsNotStruct: return "Restricted child is not struct";
                case ErrorKind.RestrictingChildStructKindNotEqualToRestricted: return "Restricting child struct kind '{0}' not equal to restricted child struct '{1}'";
                case ErrorKind.RequiredChildNotRestricting: return "Required child with member name '{0}' not restricting";
                case ErrorKind.RestrictingChoiceMustHasMembersIfRestrictedChoiceNotEffectiveOptional: return "Restricting choice must has members if restricted choice is not effective optional";
                case ErrorKind.RestrictedChildIsNotElementOrElementWildcard: return "Restricted child is not element or element wildcard";
                case ErrorKind.RestrictingElementFullNameNotMatchWithRestrictedElement: return "Restricting element full name '{0}' not match with restricted element full name";
                case ErrorKind.RestrictingElementNamespaceNotMatchWithRestrictedElementWildcard: return "Restricting element namespace '{0}' not match with restricted element wildcard '{1}'";
                case ErrorKind.RestrictingElementTypeNotEqualToOrRestrictedDeriveFromRestrictedElementType: return "Restricting element type not equal to or restricted derive from restricted element type";
                case ErrorKind.CannotSetDefaultOrFixedValueIfElementTypeIsSystemRootType: return "Cannot set default or fixed value if element type is system root type";
                case ErrorKind.CannotSetDefaultOrFixedValueIfElementComplexTypeIsNotMixed: return "Cannot set default or fixed value if element complex type is not mixed";
                case ErrorKind.CannotSetDefaultOrFixedValueIfElementTypeHasNoChildStruct: return "Cannot set default or fixed value if element type has no child struct";
                case ErrorKind.CannotSetDefaultOrFixedValueIfElementTypeChildStructIsNotEffectiveOptional: return "Cannot set default or fixed value if element type child struct is not effective optional";
                case ErrorKind.RestrictedElementAndRestrictingElementMustBothHasFixedValueOrNeither: return "Restricted element and restricting element must both has fixed value or neither";
                case ErrorKind.RestrictedElementFixedValueNotEqualToRestrictingElementFixedValue: return "Restricted element fixed value '{0}' not equal to restricting element fixed value '{1}'";
                case ErrorKind.SubstitutingElementTypeNotEqualToOrDeriveFromSubstitutedElementType: return "Substituting element type not equal to or derive from substituted element type";
                case ErrorKind.RestrictedChildIsNotElementWildcard: return "Restricted child is not element wildcard";
                case ErrorKind.UpaViolated: return "Unique particle attribution violated";
                //
                //case ErrorKind.WildcardLocalNameMustHasNonEmptyUri: return "Wildcard local name must has non empty uri";
                case ErrorKind.AttributeStepNotAllowedInIdentityPath: return "Attribute step not allowed in identity path";
                case ErrorKind.InvalidAttributeStep: return "Invalid attribute step";
                case ErrorKind.SplitListValueAnnotationRequiresSingleValuePathExpression: return "Split list value annotation requires single value path expression";
                case ErrorKind.DuplicateIdentityConstraintName: return "Duplicate identity constraint name '{0}'";
                case ErrorKind.KeyRefValueCountNotEqualToReferentialValueCount: return "Key ref value count '{0}' not equal to referential value count '{1}'";

                //
                default: throw new InvalidOperationException("Invalid X error kind: " + kind);
            }
        }
    }
}
