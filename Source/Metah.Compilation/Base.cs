using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Metah.Compilation {
    internal static class CS {
        internal const string BinExprNodeLabel = "_BinaryExpression";
        internal const string BinExprNodeLeftLabel = "Left";
        internal const string BinExprNodeTokenLabel = "OperatorToken";
        internal const string BinExprNodeRightLabel = "Right";
        internal static CSharpSyntaxNode ToSyntaxNode(this Node node) {
            if (node.IsNull) return null;
            switch (node.Label) {
                case "_CompilationUnit":
                    return SyntaxFactory.CompilationUnit(
                        externs: node.Member("Externs").ToSyntaxList<ExternAliasDirectiveSyntax>(),
                        usings: node.Member("Usings").ToSyntaxList<UsingDirectiveSyntax>(),
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        members: node.Member("Members").ToSyntaxList<MemberDeclarationSyntax>());
                case "_NamespaceDeclaration":
                    return SyntaxFactory.NamespaceDeclaration(
                        namespaceKeyword: node.Member("NamespaceKeyword").ToSyntaxToken(),
                        name: (NameSyntax)node.Member("Name").ToSyntaxNode(),
                        openBraceToken: node.Member("OpenBraceToken").ToSyntaxToken(),
                        externs: node.Member("Externs").ToSyntaxList<ExternAliasDirectiveSyntax>(),
                        usings: node.Member("Usings").ToSyntaxList<UsingDirectiveSyntax>(),
                        members: node.Member("Members").ToSyntaxList<MemberDeclarationSyntax>(),
                        closeBraceToken: node.Member("CloseBraceToken").ToSyntaxToken(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_ExternAliasDirective":
                    return SyntaxFactory.ExternAliasDirective(
                        externKeyword: node.Member("ExternKeyword").ToSyntaxToken(),
                        aliasKeyword: node.Member("AliasKeyword").ToSyntaxToken(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_UsingDirective":
                    return SyntaxFactory.UsingDirective(
                        usingKeyword: node.Member("UsingKeyword").ToSyntaxToken(),
                        alias: (NameEqualsSyntax)node.Member("Alias").ToSyntaxNode(),
                        name: (NameSyntax)node.Member("Name").ToSyntaxNode(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_AttributeList":
                    return SyntaxFactory.AttributeList(
                        openBracketToken: node.Member("OpenBracketToken").ToSyntaxToken(),
                        target: (AttributeTargetSpecifierSyntax)node.Member("Target").ToSyntaxNode(),
                        attributes: node.Member("Attributes").ToSeparatedSyntaxList<AttributeSyntax>(),
                        closeBracketToken: node.Member("CloseBracketToken").ToSyntaxToken());
                case "_AttributeTargetSpecifier":
                    return SyntaxFactory.AttributeTargetSpecifier(
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        colonToken: node.Member("ColonToken").ToSyntaxToken());
                case "_Attribute":
                    return SyntaxFactory.Attribute(
                        name: (NameSyntax)node.Member("Name").ToSyntaxNode(),
                        argumentList: (AttributeArgumentListSyntax)node.Member("ArgumentList").ToSyntaxNode());
                case "_AttributeArgumentList":
                    return SyntaxFactory.AttributeArgumentList(
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        arguments: node.Member("Arguments").ToSeparatedSyntaxList<AttributeArgumentSyntax>(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken());
                case "_AttributeArgument":
                    return SyntaxFactory.AttributeArgument(
                        nameEquals: (NameEqualsSyntax)node.Member("NameEquals").ToSyntaxNode(),
                        nameColon: (NameColonSyntax)node.Member("NameColon").ToSyntaxNode(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode());
                case "_DelegateDeclaration":
                    return SyntaxFactory.DelegateDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        delegateKeyword: node.Member("DelegateKeyword").ToSyntaxToken(),
                        returnType: (TypeSyntax)node.Member("ReturnType").ToSyntaxNode(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        typeParameterList: (TypeParameterListSyntax)node.Member("TypeParameterList").ToSyntaxNode(),
                        parameterList: (ParameterListSyntax)node.Member("ParameterList").ToSyntaxNode(),
                        constraintClauses: node.Member("ConstraintClauses").ToSyntaxList<TypeParameterConstraintClauseSyntax>(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_EnumDeclaration":
                    return SyntaxFactory.EnumDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        enumKeyword: node.Member("EnumKeyword").ToSyntaxToken(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        baseList: (BaseListSyntax)node.Member("BaseList").ToSyntaxNode(),
                        openBraceToken: node.Member("OpenBraceToken").ToSyntaxToken(),
                        members: node.Member("Members").ToSeparatedSyntaxList<EnumMemberDeclarationSyntax>(),
                        closeBraceToken: node.Member("CloseBraceToken").ToSyntaxToken(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_EnumMemberDeclaration":
                    return SyntaxFactory.EnumMemberDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        equalsValue: (EqualsValueClauseSyntax)node.Member("EqualsValue").ToSyntaxNode());
                case "_ClassDeclaration":
                    return SyntaxFactory.ClassDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        keyword: node.Member("ClassKeyword").ToSyntaxToken(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        typeParameterList: (TypeParameterListSyntax)node.Member("TypeParameterList").ToSyntaxNode(),
                        baseList: (BaseListSyntax)node.Member("BaseList").ToSyntaxNode(),
                        constraintClauses: node.Member("ConstraintClauses").ToSyntaxList<TypeParameterConstraintClauseSyntax>(),
                        openBraceToken: node.Member("OpenBraceToken").ToSyntaxToken(),
                        members: node.Member("Members").ToSyntaxList<MemberDeclarationSyntax>(),
                        closeBraceToken: node.Member("CloseBraceToken").ToSyntaxToken(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_StructDeclaration":
                    return SyntaxFactory.StructDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        keyword: node.Member("StructKeyword").ToSyntaxToken(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        typeParameterList: (TypeParameterListSyntax)node.Member("TypeParameterList").ToSyntaxNode(),
                        baseList: (BaseListSyntax)node.Member("BaseList").ToSyntaxNode(),
                        constraintClauses: node.Member("ConstraintClauses").ToSyntaxList<TypeParameterConstraintClauseSyntax>(),
                        openBraceToken: node.Member("OpenBraceToken").ToSyntaxToken(),
                        members: node.Member("Members").ToSyntaxList<MemberDeclarationSyntax>(),
                        closeBraceToken: node.Member("CloseBraceToken").ToSyntaxToken(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_InterfaceDeclaration":
                    return SyntaxFactory.InterfaceDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        keyword: node.Member("InterfaceKeyword").ToSyntaxToken(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        typeParameterList: (TypeParameterListSyntax)node.Member("TypeParameterList").ToSyntaxNode(),
                        baseList: (BaseListSyntax)node.Member("BaseList").ToSyntaxNode(),
                        constraintClauses: node.Member("ConstraintClauses").ToSyntaxList<TypeParameterConstraintClauseSyntax>(),
                        openBraceToken: node.Member("OpenBraceToken").ToSyntaxToken(),
                        members: node.Member("Members").ToSyntaxList<MemberDeclarationSyntax>(),
                        closeBraceToken: node.Member("CloseBraceToken").ToSyntaxToken(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_BaseList":
                    return SyntaxFactory.BaseList(
                        colonToken: node.Member("ColonToken").ToSyntaxToken(),
                        types: node.Member("Types").ToSeparatedSyntaxList<BaseTypeSyntax>());
                case "_SimpleBaseType":
                    return SyntaxFactory.SimpleBaseType(
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode());
                case "_FieldDeclaration":
                    return SyntaxFactory.FieldDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        declaration: (VariableDeclarationSyntax)node.Member("Declaration").ToSyntaxNode(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_EventFieldDeclaration":
                    return SyntaxFactory.EventFieldDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        eventKeyword: node.Member("EventKeyword").ToSyntaxToken(),
                        declaration: (VariableDeclarationSyntax)node.Member("Declaration").ToSyntaxNode(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_MethodDeclaration":
                    return SyntaxFactory.MethodDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        returnType: (TypeSyntax)node.Member("ReturnType").ToSyntaxNode(),
                        explicitInterfaceSpecifier: (ExplicitInterfaceSpecifierSyntax)node.Member("ExplicitInterfaceSpecifier").ToSyntaxNode(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        typeParameterList: (TypeParameterListSyntax)node.Member("TypeParameterList").ToSyntaxNode(),
                        parameterList: (ParameterListSyntax)node.Member("ParameterList").ToSyntaxNode(),
                        constraintClauses: node.Member("ConstraintClauses").ToSyntaxList<TypeParameterConstraintClauseSyntax>(),
                        body: (BlockSyntax)node.Member("Body").ToSyntaxNode(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_ExplicitInterfaceSpecifier":
                    return SyntaxFactory.ExplicitInterfaceSpecifier(
                        name: (NameSyntax)node.Member("Name").ToSyntaxNode(),
                        dotToken: node.Member("DotToken").ToSyntaxToken());
                case "_OperatorDeclaration":
                    return SyntaxFactory.OperatorDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        returnType: (TypeSyntax)node.Member("ReturnType").ToSyntaxNode(),
                        operatorKeyword: node.Member("OperatorKeyword").ToSyntaxToken(),
                        operatorToken: node.Member("OperatorToken").ToSyntaxToken(),
                        parameterList: (ParameterListSyntax)node.Member("ParameterList").ToSyntaxNode(),
                        body: (BlockSyntax)node.Member("Body").ToSyntaxNode(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_ConversionOperatorDeclaration":
                    return SyntaxFactory.ConversionOperatorDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        implicitOrExplicitKeyword: node.Member("ImplicitOrExplicitKeyword").ToSyntaxToken(),
                        operatorKeyword: node.Member("OperatorKeyword").ToSyntaxToken(),
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode(),
                        parameterList: (ParameterListSyntax)node.Member("ParameterList").ToSyntaxNode(),
                        body: (BlockSyntax)node.Member("Body").ToSyntaxNode(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_ConstructorDeclaration":
                    return SyntaxFactory.ConstructorDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        parameterList: (ParameterListSyntax)node.Member("ParameterList").ToSyntaxNode(),
                        initializer: (ConstructorInitializerSyntax)node.Member("Initializer").ToSyntaxNode(),
                        body: (BlockSyntax)node.Member("Body").ToSyntaxNode(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_ConstructorInitializer":
                    return SyntaxFactory.ConstructorInitializer(
                        kind: node.Member("Kind").CSTokenKind(),
                        colonToken: node.Member("ColonToken").ToSyntaxToken(),
                        thisOrBaseKeyword: node.Member("ThisOrBaseKeyword").ToSyntaxToken(),
                        argumentList: (ArgumentListSyntax)node.Member("ArgumentList").ToSyntaxNode());
                case "_DestructorDeclaration":
                    return SyntaxFactory.DestructorDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        tildeToken: node.Member("TildeToken").ToSyntaxToken(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        parameterList: (ParameterListSyntax)node.Member("ParameterList").ToSyntaxNode(),
                        body: (BlockSyntax)node.Member("Body").ToSyntaxNode(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_PropertyDeclaration":
                    return SyntaxFactory.PropertyDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode(),
                        explicitInterfaceSpecifier: (ExplicitInterfaceSpecifierSyntax)node.Member("ExplicitInterfaceSpecifier").ToSyntaxNode(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        accessorList: (AccessorListSyntax)node.Member("AccessorList").ToSyntaxNode(),
                        expressionBody: null,
                        initializer: null,
                        semicolon: default(SyntaxToken));
                case "_IndexerDeclaration":
                    return SyntaxFactory.IndexerDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode(),
                        explicitInterfaceSpecifier: (ExplicitInterfaceSpecifierSyntax)node.Member("ExplicitInterfaceSpecifier").ToSyntaxNode(),
                        thisKeyword: node.Member("ThisKeyword").ToSyntaxToken(),
                        parameterList: (BracketedParameterListSyntax)node.Member("ParameterList").ToSyntaxNode(),
                        accessorList: (AccessorListSyntax)node.Member("AccessorList").ToSyntaxNode(),
                        expressionBody: null,
                        semicolon: default(SyntaxToken));
                case "_AccessorList":
                    return SyntaxFactory.AccessorList(
                        openBraceToken: node.Member("OpenBraceToken").ToSyntaxToken(),
                        accessors: node.Member("Accessors").ToSyntaxList<AccessorDeclarationSyntax>(),
                        closeBraceToken: node.Member("CloseBraceToken").ToSyntaxToken());
                case "_AccessorDeclaration":
                    return SyntaxFactory.AccessorDeclaration(
                        kind: node.Member("Kind").CSTokenKind(),
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        keyword: node.Member("Keyword").ToSyntaxToken(),
                        body: (BlockSyntax)node.Member("Body").ToSyntaxNode(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_EventDeclaration":
                    return SyntaxFactory.EventDeclaration(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        eventKeyword: node.Member("EventKeyword").ToSyntaxToken(),
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode(),
                        explicitInterfaceSpecifier: (ExplicitInterfaceSpecifierSyntax)node.Member("ExplicitInterfaceSpecifier").ToSyntaxNode(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        accessorList: (AccessorListSyntax)node.Member("AccessorList").ToSyntaxNode());
                case "_TypeParameterList":
                    return SyntaxFactory.TypeParameterList(
                        lessThanToken: node.Member("LessThanToken").ToSyntaxToken(),
                        parameters: node.Member("Parameters").ToSeparatedSyntaxList<TypeParameterSyntax>(),
                        greaterThanToken: node.Member("GreaterThanToken").ToSyntaxToken());
                case "_TypeParameter":
                    return SyntaxFactory.TypeParameter(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        varianceKeyword: node.Member("VarianceKeyword").ToSyntaxToken(),
                        identifier: node.Member("Identifier").ToSyntaxToken());
                case "_TypeParameterConstraintClause":
                    return SyntaxFactory.TypeParameterConstraintClause(
                        whereKeyword: node.Member("WhereKeyword").ToSyntaxToken(),
                        name: (IdentifierNameSyntax)node.Member("Identifier").ToSyntaxNode(),
                        colonToken: node.Member("ColonToken").ToSyntaxToken(),
                        constraints: node.Member("Constraints").ToSeparatedSyntaxList<TypeParameterConstraintSyntax>());
                case "_ConstructorConstraint":
                    return SyntaxFactory.ConstructorConstraint(
                        newKeyword: node.Member("NewKeyword").ToSyntaxToken(),
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken());
                case "_TypeConstraint":
                    return SyntaxFactory.TypeConstraint(
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode());
                case "_ClassOrStructConstraint":
                    return SyntaxFactory.ClassOrStructConstraint(
                        kind: node.Member("Kind").CSTokenKind(),
                        classOrStructKeyword: node.Member("ClassOrStructKeyword").ToSyntaxToken());
                case "_ArgumentList":
                    return SyntaxFactory.ArgumentList(
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        arguments: node.Member("Arguments").ToSeparatedSyntaxList<ArgumentSyntax>(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken());
                case "_BracketedArgumentList":
                    return SyntaxFactory.BracketedArgumentList(
                        openBracketToken: node.Member("OpenBracketToken").ToSyntaxToken(),
                        arguments: node.Member("Arguments").ToSeparatedSyntaxList<ArgumentSyntax>(),
                        closeBracketToken: node.Member("CloseBracketToken").ToSyntaxToken());
                case "_Argument":
                    return SyntaxFactory.Argument(
                        nameColon: (NameColonSyntax)node.Member("NameColon").ToSyntaxNode(),
                        refOrOutKeyword: node.Member("RefOrOutKeyword").ToSyntaxToken(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode());
                case "_ParameterList":
                    return SyntaxFactory.ParameterList(
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        parameters: node.Member("Parameters").ToSeparatedSyntaxList<ParameterSyntax>(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken());
                case "_BracketedParameterList":
                    return SyntaxFactory.BracketedParameterList(
                        openBracketToken: node.Member("OpenBracketToken").ToSyntaxToken(),
                        parameters: node.Member("Parameters").ToSeparatedSyntaxList<ParameterSyntax>(),
                    closeBracketToken: node.Member("CloseBracketToken").ToSyntaxToken());
                case "_Parameter":
                    return SyntaxFactory.Parameter(
                        attributeLists: node.Member("AttributeLists").ToSyntaxList<AttributeListSyntax>(),
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        @default: (EqualsValueClauseSyntax)node.Member("Default").ToSyntaxNode());
                case "_EqualsValueClause":
                    return SyntaxFactory.EqualsValueClause(
                        equalsToken: node.Member("EqualsToken").ToSyntaxToken(),
                        value: (ExpressionSyntax)node.Member("Value").ToSyntaxNode());
                case "_NameColon":
                    return SyntaxFactory.NameColon(
                        name: (IdentifierNameSyntax)node.Member("Identifier").ToSyntaxNode(),
                        colonToken: node.Member("ColonToken").ToSyntaxToken());
                case "_NameEquals":
                    return SyntaxFactory.NameEquals(
                        name: (IdentifierNameSyntax)node.Member("Identifier").ToSyntaxNode(),
                        equalsToken: node.Member("EqualsToken").ToSyntaxToken());
                //
                case "_Block":
                    return SyntaxFactory.Block(
                        openBraceToken: node.Member("OpenBraceToken").ToSyntaxToken(),
                        statements: node.Member("Statements").ToSyntaxList<StatementSyntax>(),
                        closeBraceToken: node.Member("CloseBraceToken").ToSyntaxToken());
                case "_EmptyStatement":
                    return SyntaxFactory.EmptyStatement(
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_ExpressionStatement":
                    return SyntaxFactory.ExpressionStatement(
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_LocalDeclarationStatement":
                    return SyntaxFactory.LocalDeclarationStatement(
                        modifiers: node.Member("Modifiers").ToSyntaxTokenList(),
                        declaration: (VariableDeclarationSyntax)node.Member("Declaration").ToSyntaxNode(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_VariableDeclaration":
                    return SyntaxFactory.VariableDeclaration(
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode(),
                        variables: node.Member("Variables").ToSeparatedSyntaxList<VariableDeclaratorSyntax>());
                case "_VariableDeclarator":
                    return SyntaxFactory.VariableDeclarator(
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        argumentList: (BracketedArgumentListSyntax)node.Member("ArgumentList").ToSyntaxNode(),
                        initializer: (EqualsValueClauseSyntax)node.Member("Initializer").ToSyntaxNode());
                case "_LabeledStatement":
                    return SyntaxFactory.LabeledStatement(
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        colonToken: node.Member("ColonToken").ToSyntaxToken(),
                        statement: (StatementSyntax)node.Member("Statement").ToSyntaxNode());
                case "_GotoStatement":
                    return SyntaxFactory.GotoStatement(
                        kind: node.Member("Kind").CSTokenKind(),
                        gotoKeyword: node.Member("GotoKeyword").ToSyntaxToken(),
                        caseOrDefaultKeyword: node.Member("CaseOrDefaultKeyword").ToSyntaxToken(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_BreakStatement":
                    return SyntaxFactory.BreakStatement(
                        breakKeyword: node.Member("BreakKeyword").ToSyntaxToken(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_ContinueStatement":
                    return SyntaxFactory.ContinueStatement(
                        continueKeyword: node.Member("ContinueKeyword").ToSyntaxToken(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_ReturnStatement":
                    return SyntaxFactory.ReturnStatement(
                        returnKeyword: node.Member("ReturnKeyword").ToSyntaxToken(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_ThrowStatement":
                    return SyntaxFactory.ThrowStatement(
                        throwKeyword: node.Member("ThrowKeyword").ToSyntaxToken(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_YieldStatement":
                    return SyntaxFactory.YieldStatement(
                        kind: node.Member("Kind").CSTokenKind(),
                        yieldKeyword: node.Member("YieldKeyword").ToSyntaxToken(),
                        returnOrBreakKeyword: node.Member("ReturnOrBreakKeyword").ToSyntaxToken(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_WhileStatement":
                    return SyntaxFactory.WhileStatement(
                        whileKeyword: node.Member("WhileKeyword").ToSyntaxToken(),
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        condition: (ExpressionSyntax)node.Member("Condition").ToSyntaxNode(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken(),
                        statement: (StatementSyntax)node.Member("Statement").ToSyntaxNode());
                case "_DoStatement":
                    return SyntaxFactory.DoStatement(
                        doKeyword: node.Member("DoKeyword").ToSyntaxToken(),
                        statement: (StatementSyntax)node.Member("Statement").ToSyntaxNode(),
                        whileKeyword: node.Member("WhileKeyword").ToSyntaxToken(),
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        condition: (ExpressionSyntax)node.Member("Condition").ToSyntaxNode(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken(),
                        semicolonToken: node.Member("SemicolonToken").ToSyntaxToken());
                case "_ForStatement":
                    return SyntaxFactory.ForStatement(
                        forKeyword: node.Member("ForKeyword").ToSyntaxToken(),
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        declaration: (VariableDeclarationSyntax)node.Member("Declaration").ToSyntaxNode(),
                        initializers: node.Member("Initializers").ToSeparatedSyntaxList<ExpressionSyntax>(),
                        firstSemicolonToken: node.Member("FirstSemicolonToken").ToSyntaxToken(),
                        condition: (ExpressionSyntax)node.Member("Condition").ToSyntaxNode(),
                        secondSemicolonToken: node.Member("SecondSemicolonToken").ToSyntaxToken(),
                        incrementors: node.Member("Incrementors").ToSeparatedSyntaxList<ExpressionSyntax>(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken(),
                        statement: (StatementSyntax)node.Member("Statement").ToSyntaxNode());
                case "_ForEachStatement":
                    return SyntaxFactory.ForEachStatement(
                        forEachKeyword: node.Member("ForEachKeyword").ToSyntaxToken(),
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        inKeyword: node.Member("InKeyword").ToSyntaxToken(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken(),
                        statement: (StatementSyntax)node.Member("Statement").ToSyntaxNode());
                case "_UsingStatement":
                    return SyntaxFactory.UsingStatement(
                        usingKeyword: node.Member("UsingKeyword").ToSyntaxToken(),
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        declaration: (VariableDeclarationSyntax)node.Member("Declaration").ToSyntaxNode(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken(),
                        statement: (StatementSyntax)node.Member("Statement").ToSyntaxNode());
                case "_FixedStatement":
                    return SyntaxFactory.FixedStatement(
                        fixedKeyword: node.Member("FixedKeyword").ToSyntaxToken(),
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        declaration: (VariableDeclarationSyntax)node.Member("Declaration").ToSyntaxNode(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken(),
                        statement: (StatementSyntax)node.Member("Statement").ToSyntaxNode());
                case "_CheckedStatement":
                    return SyntaxFactory.CheckedStatement(
                        kind: node.Member("Kind").CSTokenKind(),
                        keyword: node.Member("CheckedOrUncheckedKeyword").ToSyntaxToken(),
                        block: (BlockSyntax)node.Member("Block").ToSyntaxNode());
                case "_UnsafeStatement":
                    return SyntaxFactory.UnsafeStatement(
                        unsafeKeyword: node.Member("UnsafeKeyword").ToSyntaxToken(),
                        block: (BlockSyntax)node.Member("Block").ToSyntaxNode());
                case "_LockStatement":
                    return SyntaxFactory.LockStatement(
                        lockKeyword: node.Member("LockKeyword").ToSyntaxToken(),
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken(),
                        statement: (StatementSyntax)node.Member("Statement").ToSyntaxNode());
                case "_IfStatement":
                    return SyntaxFactory.IfStatement(
                        ifKeyword: node.Member("IfKeyword").ToSyntaxToken(),
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        condition: (ExpressionSyntax)node.Member("Condition").ToSyntaxNode(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken(),
                        statement: (StatementSyntax)node.Member("Statement").ToSyntaxNode(),
                        @else: (ElseClauseSyntax)node.Member("Else").ToSyntaxNode());
                case "_ElseClause":
                    return SyntaxFactory.ElseClause(
                        elseKeyword: node.Member("ElseKeyword").ToSyntaxToken(),
                        statement: (StatementSyntax)node.Member("Statement").ToSyntaxNode());
                case "_SwitchStatement":
                    return SyntaxFactory.SwitchStatement(
                        switchKeyword: node.Member("SwitchKeyword").ToSyntaxToken(),
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken(),
                        openBraceToken: node.Member("OpenBraceToken").ToSyntaxToken(),
                        sections: node.Member("Sections").ToSyntaxList<SwitchSectionSyntax>(),
                        closeBraceToken: node.Member("CloseBraceToken").ToSyntaxToken());
                case "_SwitchSection":
                    return SyntaxFactory.SwitchSection(
                        labels: node.Member("Labels").ToSyntaxList<SwitchLabelSyntax>(),
                        statements: node.Member("Statements").ToSyntaxList<StatementSyntax>());
                case "_CaseSwitchLabel":
                    return SyntaxFactory.CaseSwitchLabel(
                        keyword: node.Member("Keyword").ToSyntaxToken(),
                        value: (ExpressionSyntax)node.Member("Value").ToSyntaxNode(),
                        colonToken: node.Member("ColonToken").ToSyntaxToken());
                case "_DefaultSwitchLabel":
                    return SyntaxFactory.DefaultSwitchLabel(
                        keyword: node.Member("Keyword").ToSyntaxToken(),
                        colonToken: node.Member("ColonToken").ToSyntaxToken());
                case "_TryStatement":
                    return SyntaxFactory.TryStatement(
                        tryKeyword: node.Member("TryKeyword").ToSyntaxToken(),
                        block: (BlockSyntax)node.Member("Block").ToSyntaxNode(),
                        catches: node.Member("Catches").ToSyntaxList<CatchClauseSyntax>(),
                        @finally: (FinallyClauseSyntax)node.Member("Finally").ToSyntaxNode());
                case "_CatchClause":
                    return SyntaxFactory.CatchClause(
                        catchKeyword: node.Member("CatchKeyword").ToSyntaxToken(),
                        declaration: (CatchDeclarationSyntax)node.Member("Declaration").ToSyntaxNode(),
                        filter: null,
                        block: (BlockSyntax)node.Member("Block").ToSyntaxNode());
                case "_CatchDeclaration":
                    return SyntaxFactory.CatchDeclaration(
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken());
                case "_FinallyClause":
                    return SyntaxFactory.FinallyClause(
                        finallyKeyword: node.Member("FinallyKeyword").ToSyntaxToken(),
                        block: (BlockSyntax)node.Member("Block").ToSyntaxNode());
                //
                case "_SimpleLambdaExpression":
                    return SyntaxFactory.SimpleLambdaExpression(
                        asyncKeyword: node.Member("AsyncKeyword").ToSyntaxToken(),
                        parameter: (ParameterSyntax)node.Member("Parameter").ToSyntaxNode(),
                        arrowToken: node.Member("EqualsGreaterThanToken").ToSyntaxToken(),
                        body: node.Member("Body").ToSyntaxNode());
                case "_ParenthesizedLambdaExpression":
                    return SyntaxFactory.ParenthesizedLambdaExpression(
                        asyncKeyword: node.Member("AsyncKeyword").ToSyntaxToken(),
                        parameterList: (ParameterListSyntax)node.Member("ParameterList").ToSyntaxNode(),
                        arrowToken: node.Member("EqualsGreaterThanToken").ToSyntaxToken(),
                        body: node.Member("Body").ToSyntaxNode());
                case "_AssignmentExpression":
                    return SyntaxFactory.AssignmentExpression(
                        kind: node.Member("Kind").CSTokenKind(),
                        left: (ExpressionSyntax)node.Member(BinExprNodeLeftLabel).ToSyntaxNode(),
                        operatorToken: node.Member(BinExprNodeTokenLabel).ToSyntaxToken(),
                        right: (ExpressionSyntax)node.Member(BinExprNodeRightLabel).ToSyntaxNode());
                case BinExprNodeLabel:
                    return SyntaxFactory.BinaryExpression(
                        kind: node.Member("Kind").CSTokenKind(),
                        left: (ExpressionSyntax)node.Member(BinExprNodeLeftLabel).ToSyntaxNode(),
                        operatorToken: node.Member(BinExprNodeTokenLabel).ToSyntaxToken(),
                        right: (ExpressionSyntax)node.Member(BinExprNodeRightLabel).ToSyntaxNode());
                case "_ConditionalExpression":
                    return SyntaxFactory.ConditionalExpression(
                        condition: (ExpressionSyntax)node.Member("Condition").ToSyntaxNode(),
                        questionToken: node.Member("QuestionToken").ToSyntaxToken(),
                        whenTrue: (ExpressionSyntax)node.Member("WhenTrue").ToSyntaxNode(),
                        colonToken: node.Member("ColonToken").ToSyntaxToken(),
                        whenFalse: (ExpressionSyntax)node.Member("WhenFalse").ToSyntaxNode());
                case "_PrefixUnaryExpression":
                    return SyntaxFactory.PrefixUnaryExpression(
                        kind: node.Member("Kind").CSTokenKind(),
                        operatorToken: node.Member("OperatorToken").ToSyntaxToken(),
                        operand: (ExpressionSyntax)node.Member("Operand").ToSyntaxNode());
                case "_AwaitExpression":
                    return SyntaxFactory.AwaitExpression(
                        awaitKeyword: node.Member("AwaitKeyword").ToSyntaxToken(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode());
                case "_PostfixUnaryExpression":
                    return SyntaxFactory.PostfixUnaryExpression(
                        kind: node.Member("Kind").CSTokenKind(),
                        operand: (ExpressionSyntax)node.Member("Operand").ToSyntaxNode(),
                        operatorToken: node.Member("OperatorToken").ToSyntaxToken());
                case "_CastExpression":
                    return SyntaxFactory.CastExpression(
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode());
                case "_ParenthesizedExpression":
                    return SyntaxFactory.ParenthesizedExpression(
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken());
                case "_LiteralExpression":
                    return SyntaxFactory.LiteralExpression(
                        kind: node.Member("Kind").CSTokenKind(),
                        token: node.Member("Token").ToSyntaxToken());
                case "_ThisExpression":
                    return SyntaxFactory.ThisExpression(
                        token: node.Member("Token").ToSyntaxToken());
                case "_BaseExpression":
                    return SyntaxFactory.BaseExpression(
                        token: node.Member("Token").ToSyntaxToken());
                case "_TypeOfExpression":
                    return SyntaxFactory.TypeOfExpression(
                        keyword: node.Member("Keyword").ToSyntaxToken(),
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken());
                case "_SizeOfExpression":
                    return SyntaxFactory.SizeOfExpression(
                        keyword: node.Member("Keyword").ToSyntaxToken(),
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken());
                case "_CheckedExpression":
                    return SyntaxFactory.CheckedExpression(
                        kind: node.Member("Kind").CSTokenKind(), keyword: node.Member("Keyword").ToSyntaxToken(),
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken());
                case "_DefaultExpression":
                    return SyntaxFactory.DefaultExpression(
                        keyword: node.Member("Keyword").ToSyntaxToken(),
                        openParenToken: node.Member("OpenParenToken").ToSyntaxToken(),
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode(),
                        closeParenToken: node.Member("CloseParenToken").ToSyntaxToken());
                case "_MemberAccessExpression":
                    return SyntaxFactory.MemberAccessExpression(
                        kind: node.Member("Kind").CSTokenKind(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode(),
                        operatorToken: node.Member("OperatorToken").ToSyntaxToken(),
                        name: (SimpleNameSyntax)node.Member("Name").ToSyntaxNode());
                case "_ElementAccessExpression":
                    return SyntaxFactory.ElementAccessExpression(
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode(),
                        argumentList: (BracketedArgumentListSyntax)node.Member("ArgumentList").ToSyntaxNode());
                case "_ArrayCreationExpression":
                    return SyntaxFactory.ArrayCreationExpression(
                        newKeyword: node.Member("NewKeyword").ToSyntaxToken(),
                        type: (ArrayTypeSyntax)node.Member("Type").ToSyntaxNode(),
                        initializer: (InitializerExpressionSyntax)node.Member("Initializer").ToSyntaxNode());
                case "_ImplicitArrayCreationExpression":
                    return SyntaxFactory.ImplicitArrayCreationExpression(
                        newKeyword: node.Member("NewKeyword").ToSyntaxToken(),
                        openBracketToken: node.Member("OpenBracketToken").ToSyntaxToken(),
                        commas: node.Member("Commas").ToSyntaxTokenList(),
                        closeBracketToken: node.Member("CloseBracketToken").ToSyntaxToken(),
                        initializer: (InitializerExpressionSyntax)node.Member("Initializer").ToSyntaxNode());
                case "_ObjectCreationExpression":
                    return SyntaxFactory.ObjectCreationExpression(
                        newKeyword: node.Member("NewKeyword").ToSyntaxToken(),
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode(),
                        argumentList: (ArgumentListSyntax)node.Member("ArgumentList").ToSyntaxNode(),
                        initializer: (InitializerExpressionSyntax)node.Member("Initializer").ToSyntaxNode());
                case "_AnonymousObjectCreationExpression":
                    return SyntaxFactory.AnonymousObjectCreationExpression(
                        newKeyword: node.Member("NewKeyword").ToSyntaxToken(),
                        openBraceToken: node.Member("OpenBraceToken").ToSyntaxToken(),
                        initializers: node.Member("Initializers").ToSeparatedSyntaxList<AnonymousObjectMemberDeclaratorSyntax>(),
                        closeBraceToken: node.Member("CloseBraceToken").ToSyntaxToken());
                case "_AnonymousObjectMemberDeclarator":
                    return SyntaxFactory.AnonymousObjectMemberDeclarator(
                        nameEquals: (NameEqualsSyntax)node.Member("NameEquals").ToSyntaxNode(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode());
                case "_InvocationExpression":
                    return SyntaxFactory.InvocationExpression(
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode(),
                        argumentList: (ArgumentListSyntax)node.Member("ArgumentList").ToSyntaxNode());
                case "_AnonymousMethodExpression":
                    return SyntaxFactory.AnonymousMethodExpression(
                        asyncKeyword: node.Member("AsyncKeyword").ToSyntaxToken(),
                        delegateKeyword: node.Member("DelegateKeyword").ToSyntaxToken(),
                        parameterList: (ParameterListSyntax)node.Member("ParameterList").ToSyntaxNode(),
                        block: (BlockSyntax)node.Member("Block").ToSyntaxNode());
                case "_StackAllocArrayCreationExpression":
                    return SyntaxFactory.StackAllocArrayCreationExpression(
                        stackAllocKeyword: node.Member("StackAllocKeyword").ToSyntaxToken(),
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode());
                case "_InitializerExpression":
                    return SyntaxFactory.InitializerExpression(
                        kind: node.Member("Kind").CSTokenKind(),
                        openBraceToken: node.Member("OpenBraceToken").ToSyntaxToken(),
                        expressions: node.Member("Expressions").ToSeparatedSyntaxList<ExpressionSyntax>(),
                        closeBraceToken: node.Member("CloseBraceToken").ToSyntaxToken());
                //
                case "_QueryExpression":
                    return SyntaxFactory.QueryExpression(
                        fromClause: (FromClauseSyntax)node.Member("FromClause").ToSyntaxNode(),
                        body: (QueryBodySyntax)node.Member("Body").ToSyntaxNode());
                case "_QueryBody":
                    return SyntaxFactory.QueryBody(
                        clauses: node.Member("Clauses").ToSyntaxList<QueryClauseSyntax>(),
                        selectOrGroup: (SelectOrGroupClauseSyntax)node.Member("SelectOrGroup").ToSyntaxNode(),
                        continuation: (QueryContinuationSyntax)node.Member("Continuation").ToSyntaxNode());
                case "_FromClause":
                    return SyntaxFactory.FromClause(
                        fromKeyword: node.Member("FromKeyword").ToSyntaxToken(),
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        inKeyword: node.Member("InKeyword").ToSyntaxToken(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode());
                case "_LetClause":
                    return SyntaxFactory.LetClause(
                        letKeyword: node.Member("LetKeyword").ToSyntaxToken(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        equalsToken: node.Member("EqualsToken").ToSyntaxToken(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode());
                case "_JoinClause":
                    return SyntaxFactory.JoinClause(
                        joinKeyword: node.Member("JoinKeyword").ToSyntaxToken(),
                        type: (TypeSyntax)node.Member("Type").ToSyntaxNode(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        inKeyword: node.Member("InKeyword").ToSyntaxToken(),
                        inExpression: (ExpressionSyntax)node.Member("InExpression").ToSyntaxNode(),
                        onKeyword: node.Member("OnKeyword").ToSyntaxToken(),
                        leftExpression: (ExpressionSyntax)node.Member("LeftExpression").ToSyntaxNode(),
                        equalsKeyword: node.Member("EqualsKeyword").ToSyntaxToken(),
                        rightExpression: (ExpressionSyntax)node.Member("RightExpression").ToSyntaxNode(),
                        into: (JoinIntoClauseSyntax)node.Member("Into").ToSyntaxNode());
                case "_JoinIntoClause":
                    return SyntaxFactory.JoinIntoClause(
                        intoKeyword: node.Member("IntoKeyword").ToSyntaxToken(),
                        identifier: node.Member("Identifier").ToSyntaxToken());
                case "_WhereClause":
                    return SyntaxFactory.WhereClause(
                        whereKeyword: node.Member("WhereKeyword").ToSyntaxToken(),
                        condition: (ExpressionSyntax)node.Member("Condition").ToSyntaxNode());
                case "_OrderByClause":
                    return SyntaxFactory.OrderByClause(
                        orderByKeyword: node.Member("OrderByKeyword").ToSyntaxToken(),
                        orderings: node.Member("Orderings").ToSeparatedSyntaxList<OrderingSyntax>());
                case "_Ordering":
                    return SyntaxFactory.Ordering(
                        kind: node.Member("Kind").CSTokenKind(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode(),
                        ascendingOrDescendingKeyword: node.Member("AscendingOrDescendingKeyword").ToSyntaxToken());
                case "_SelectClause":
                    return SyntaxFactory.SelectClause(
                        selectKeyword: node.Member("SelectKeyword").ToSyntaxToken(),
                        expression: (ExpressionSyntax)node.Member("Expression").ToSyntaxNode());
                case "_GroupClause":
                    return SyntaxFactory.GroupClause(
                        groupKeyword: node.Member("GroupKeyword").ToSyntaxToken(),
                        groupExpression: (ExpressionSyntax)node.Member("GroupExpression").ToSyntaxNode(),
                        byKeyword: node.Member("ByKeyword").ToSyntaxToken(),
                        byExpression: (ExpressionSyntax)node.Member("ByExpression").ToSyntaxNode());
                case "_QueryContinuation":
                    return SyntaxFactory.QueryContinuation(
                        intoKeyword: node.Member("IntoKeyword").ToSyntaxToken(),
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        body: (QueryBodySyntax)node.Member("Body").ToSyntaxNode());
                //
                case "_ArrayType":
                    return SyntaxFactory.ArrayType(
                        elementType: (TypeSyntax)node.Member("ElementType").ToSyntaxNode(),
                        rankSpecifiers: node.Member("RankSpecifiers").ToSyntaxList<ArrayRankSpecifierSyntax>());
                case "_ArrayRankSpecifier":
                    return SyntaxFactory.ArrayRankSpecifier(
                        openBracketToken: node.Member("OpenBracketToken").ToSyntaxToken(),
                        sizes: node.Member("Sizes").ToSeparatedSyntaxList<ExpressionSyntax>(SyntaxFactory.OmittedArraySizeExpression),
                        closeBracketToken: node.Member("CloseBracketToken").ToSyntaxToken());
                case "_NullableType":
                    return SyntaxFactory.NullableType(
                        elementType: (TypeSyntax)node.Member("ElementType").ToSyntaxNode(),
                        questionToken: node.Member("QuestionToken").ToSyntaxToken());
                case "_PredefinedType":
                    return SyntaxFactory.PredefinedType(
                        keyword: node.Member("Keyword").ToSyntaxToken());
                case "_PointerType":
                    return SyntaxFactory.PointerType(
                        elementType: (TypeSyntax)node.Member("ElementType").ToSyntaxNode(),
                        asteriskToken: node.Member("AsteriskToken").ToSyntaxToken());
                //
                case "_IdentifierName":
                    return SyntaxFactory.IdentifierName(
                        identifier: node.Member("Identifier").ToSyntaxToken());
                case "_GenericName":
                    return SyntaxFactory.GenericName(
                        identifier: node.Member("Identifier").ToSyntaxToken(),
                        typeArgumentList: (TypeArgumentListSyntax)node.Member("TypeArgumentList").ToSyntaxNode());
                case "_TypeArgumentList":
                    return SyntaxFactory.TypeArgumentList(
                        lessThanToken: node.Member("LessThanToken").ToSyntaxToken(),
                        arguments: node.Member("Arguments").ToSeparatedSyntaxList<TypeSyntax>(SyntaxFactory.OmittedTypeArgument),
                        greaterThanToken: node.Member("GreaterThanToken").ToSyntaxToken());
                case "_QualifiedName":
                    return SyntaxFactory.QualifiedName(
                        left: (NameSyntax)node.Member("Left").ToSyntaxNode(),
                        dotToken: node.Member("DotToken").ToSyntaxToken(),
                        right: (SimpleNameSyntax)node.Member("Right").ToSyntaxNode());
                case "_AliasQualifiedName":
                    return SyntaxFactory.AliasQualifiedName(
                        alias: (IdentifierNameSyntax)node.Member("Alias").ToSyntaxNode(),
                        colonColonToken: node.Member("ColonColonToken").ToSyntaxToken(),
                        name: (SimpleNameSyntax)node.Member("Name").ToSyntaxNode());

                default: throw new ArgumentException("Invalid node lable: " + node.Label);
            }
        }
        internal static ExpressionSyntax ToExpressionSyntax(this Node node) {
            return (ExpressionSyntax)node.ToSyntaxNode();
        }
        internal static TypeSyntax ToTypeSyntax(this Node node) {
            return (TypeSyntax)node.ToSyntaxNode();
        }
        internal static SyntaxList<T> ToSyntaxList<T>(this Node node) where T : CSharpSyntaxNode {
            return SyntaxFactory.List<T>(node.Items.Select(itemNode => (T)itemNode.ToSyntaxNode()));
        }
        internal static SyntaxList<T> ToSyntaxList<T>(this Node node, bool filterNonCSNode) where T : CSharpSyntaxNode {
            IEnumerable<Node> itemNodes = node.Items;
            if (filterNonCSNode) itemNodes = itemNodes.CSNodes();
            return SyntaxFactory.List<T>(itemNodes.Select(itemNode => (T)itemNode.ToSyntaxNode()));
        }
        private static SeparatedSyntaxList<T> ToSeparatedSyntaxList<T>(this Node node, Func<T> omittedNodeGetter = null)
            where T : CSharpSyntaxNode {
            var itemNodes = node.Items;
            if (omittedNodeGetter != null) {
                if (node.Label == "__") {
                    return SyntaxFactory.SeparatedList<T>(Extensions.Repeat(omittedNodeGetter, itemNodes.Count + 1),
                        itemNodes.Select(itemNode => itemNode.ToSyntaxToken()));
                }
                if (itemNodes.Count == 0) {
                    return SyntaxFactory.SingletonSeparatedList<T>(omittedNodeGetter());
                }
            }
            return SyntaxFactory.SeparatedList<T>(itemNodes.Select(itemNode => {
                if (itemNode.IsCSToken()) {
                    return (SyntaxNodeOrToken)itemNode.ToSyntaxToken();
                }
                return (SyntaxNodeOrToken)itemNode.ToSyntaxNode();
            }));
        }
        internal static SyntaxTokenList ToSyntaxTokenList(this Node node) {
            return SyntaxFactory.TokenList(node.Items.Select(i => i.ToSyntaxToken()));
        }
        internal static SyntaxToken ToSyntaxToken(this Node node, bool setSourceSpan = true) {
            if (node.IsNull) {
                return default(SyntaxToken);
            }
            SyntaxToken token;
            var kind = node.MemberCSTokenKind();
            switch (kind) {
                case SyntaxKind.IdentifierToken:
                    token = Id(node.MemberTokenText());
                    break;
                case SyntaxKind.NumericLiteralToken:
                case SyntaxKind.CharacterLiteralToken:
                case SyntaxKind.StringLiteralToken:
                    token = SyntaxFactory.ParseToken(node.MemberTokenText());
                    break;
                default:
                    token = SyntaxFactory.Token(kind);
                    break;
            }
            if (setSourceSpan) {
                return token.SetSourceSpan(node.SourceSpan);
            }
            return token;
        }
        internal static string GetStringLiteralTokenValue(this Node node) {
            var token = node.ToSyntaxToken(false);
            //if (token.CSharpKind() != SyntaxKind.StringLiteralToken) throw new InvalidOperationException();
            return (string)token.Value;
        }
        internal static void ToSyntaxNodeAndAdd<T>(List<T> nodeList, Node itemsNode, bool filterNonCSNode = false) where T : CSharpSyntaxNode {
            IEnumerable<Node> itemNodes = itemsNode.Items;
            if (filterNonCSNode) itemNodes = itemNodes.CSNodes();
            foreach (var itemNode in itemNodes) {
                nodeList.Add((T)itemNode.ToSyntaxNode());
            }
        }
        internal static SeparatedSyntaxList<T> ToSeparatedList<T>(T firstNode, SyntaxToken firstToken, Node itemsNode) where T : SyntaxNode {
            return SyntaxFactory.SeparatedList<T>(ToSeparatedListCore(firstNode, firstToken, itemsNode));
        }
        private static IEnumerable<SyntaxNodeOrToken> ToSeparatedListCore(SyntaxNode firstNode, SyntaxToken firstToken, Node itemsNode) {
            yield return firstNode;
            yield return firstToken;
            foreach (var itemNode in itemsNode.Items) {
                if (itemNode.IsCSToken()) {
                    yield return itemNode.ToSyntaxToken();
                }
                else {
                    yield return itemNode.ToSyntaxNode();
                }
            }
        }
        //internal static SeparatedSyntaxList<T> SeparatedList<T>(IEnumerable<T> nodes, SyntaxKind separator = SyntaxKind.CommaToken) where T : SyntaxNode {
        //    var separatorCount = nodes != null ? nodes.Count() - 1 : -1;
        //    if (separatorCount < 0) return default(SeparatedSyntaxList<T>);
        //    return SyntaxFactory.SeparatedList(nodes, Repeat(separator, separatorCount));
        //}
        //private static IEnumerable<SyntaxToken> Repeat(SyntaxKind tokenKind, int count) {
        //    for (var i = 0; i < count; i++)
        //        yield return SyntaxFactory.Token(tokenKind);
        //}
        //
        //
        internal static T SetAnn<T>(this T node, out SyntaxAnnotation ann) where T : SyntaxNode {
            ann = new SyntaxAnnotation();
            return node.WithAdditionalAnnotations(ann);
        }
        internal static SyntaxNode GetAnnedNode(this SyntaxNode ancestor, SyntaxAnnotation ann) {
            return ancestor.GetAnnotatedNodes(ann).FirstOrDefault();
        }
        internal static T GetAnnedNode<T>(this SyntaxNode ancestor, SyntaxAnnotation ann) where T : SyntaxNode {
            return ancestor.GetAnnotatedNodes(ann).FirstOrDefault() as T;
        }
        //
        //
        //
        internal static SyntaxToken Id(string text) {
            return SyntaxFactory.Identifier(default(SyntaxTriviaList), SyntaxKind.IdentifierToken, text, text.UnescapeIdentifier(), default(SyntaxTriviaList));
        }
        internal static IdentifierNameSyntax IdName(string name) {
            return SyntaxFactory.IdentifierName(Id(name));
        }
        internal static IdentifierNameSyntax IdName(SyntaxToken identifier) {
            return SyntaxFactory.IdentifierName(identifier);
        }
        //
        internal static SyntaxToken PrivateToken {
            get { return SyntaxFactory.Token(SyntaxKind.PrivateKeyword); }
        }
        internal static SyntaxToken ProtectedToken {
            get { return SyntaxFactory.Token(SyntaxKind.ProtectedKeyword); }
        }
        internal static SyntaxToken InternalToken {
            get { return SyntaxFactory.Token(SyntaxKind.InternalKeyword); }
        }
        internal static SyntaxToken PublicToken {
            get { return SyntaxFactory.Token(SyntaxKind.PublicKeyword); }
        }
        internal static SyntaxToken AbstractToken {
            get { return SyntaxFactory.Token(SyntaxKind.AbstractKeyword); }
        }
        internal static SyntaxToken SealedToken {
            get { return SyntaxFactory.Token(SyntaxKind.SealedKeyword); }
        }
        internal static SyntaxToken StaticToken {
            get { return SyntaxFactory.Token(SyntaxKind.StaticKeyword); }
        }
        internal static SyntaxToken PartialToken {
            get { return SyntaxFactory.Token(SyntaxKind.PartialKeyword); }
        }
        internal static SyntaxToken NewToken {
            get { return SyntaxFactory.Token(SyntaxKind.NewKeyword); }
        }
        internal static SyntaxToken ConstToken {
            get { return SyntaxFactory.Token(SyntaxKind.ConstKeyword); }
        }
        internal static SyntaxToken ReadOnlyToken {
            get { return SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword); }
        }
        internal static SyntaxToken VirtualToken {
            get { return SyntaxFactory.Token(SyntaxKind.VirtualKeyword); }
        }
        internal static SyntaxToken OverrideToken {
            get { return SyntaxFactory.Token(SyntaxKind.OverrideKeyword); }
        }
        internal static SyntaxToken VolatileToken {
            get { return SyntaxFactory.Token(SyntaxKind.VolatileKeyword); }
        }
        internal static SyntaxToken InToken {
            get { return SyntaxFactory.Token(SyntaxKind.InKeyword); }
        }
        internal static SyntaxToken RefToken {
            get { return SyntaxFactory.Token(SyntaxKind.RefKeyword); }
        }
        internal static SyntaxToken OutToken {
            get { return SyntaxFactory.Token(SyntaxKind.OutKeyword); }
        }
        internal static SyntaxToken ThisToken {
            get { return SyntaxFactory.Token(SyntaxKind.ThisKeyword); }
        }
        internal static SyntaxToken GetToken {
            get { return SyntaxFactory.Token(SyntaxKind.GetKeyword); }
        }
        internal static SyntaxToken SetToken {
            get { return SyntaxFactory.Token(SyntaxKind.SetKeyword); }
        }
        internal static SyntaxToken AddToken {
            get { return SyntaxFactory.Token(SyntaxKind.AddKeyword); }
        }
        internal static SyntaxToken RemoveToken {
            get { return SyntaxFactory.Token(SyntaxKind.RemoveKeyword); }
        }
        internal static SyntaxToken ParamsToken {
            get { return SyntaxFactory.Token(SyntaxKind.ParamsKeyword); }
        }
        internal static SyntaxToken ImplictToken {
            get { return SyntaxFactory.Token(SyntaxKind.ImplicitKeyword); }
        }
        internal static SyntaxToken ExplictToken {
            get { return SyntaxFactory.Token(SyntaxKind.ExplicitKeyword); }
        }
        internal static SyntaxToken SemicolonToken {
            get { return SyntaxFactory.Token(SyntaxKind.SemicolonToken); }
        }
        internal static SyntaxToken CommaToken {
            get { return SyntaxFactory.Token(SyntaxKind.CommaToken); }
        }
        internal static SyntaxTokenList PublicTokenList {
            get { return SyntaxFactory.TokenList(PublicToken); }
        }
        internal static SyntaxTokenList NewPublicTokenList {
            get { return SyntaxFactory.TokenList(NewToken, PublicToken); }
        }
        internal static SyntaxTokenList NewPublicPartialTokenList {
            get { return SyntaxFactory.TokenList(NewToken, PublicToken, PartialToken); }
        }
        internal static SyntaxTokenList PublicPartialTokenList {
            get { return SyntaxFactory.TokenList(PublicToken, PartialToken); }
        }
        internal static SyntaxTokenList PublicAbstractPartialTokenList {
            get { return SyntaxFactory.TokenList(PublicToken, AbstractToken, PartialToken); }
        }
        internal static SyntaxTokenList PublicVirtualTokenList {
            get { return SyntaxFactory.TokenList(PublicToken, VirtualToken); }
        }
        internal static SyntaxTokenList PublicOverrideTokenList {
            get { return SyntaxFactory.TokenList(PublicToken, OverrideToken); }
        }
        internal static SyntaxTokenList PublicSealedTokenList {
            get { return SyntaxFactory.TokenList(PublicToken, SealedToken); }
        }
        internal static SyntaxTokenList PublicStaticTokenList {
            get { return SyntaxFactory.TokenList(PublicToken, StaticToken); }
        }
        internal static SyntaxTokenList PublicStaticReadOnlyTokenList {
            get { return SyntaxFactory.TokenList(PublicToken, StaticToken, ReadOnlyToken); }
        }
        internal static SyntaxTokenList NewPublicStaticReadOnlyTokenList {
            get { return SyntaxFactory.TokenList(NewToken, PublicToken, StaticToken, ReadOnlyToken); }
        }
        internal static SyntaxTokenList PublicConstTokenList {
            get { return SyntaxFactory.TokenList(PublicToken, ConstToken); }
        }
        internal static SyntaxTokenList ConstTokenList {
            get { return SyntaxFactory.TokenList(ConstToken); }
        }
        internal static SyntaxTokenList NewPublicConstTokenList {
            get { return SyntaxFactory.TokenList(NewToken, PublicToken, ConstToken); }
        }
        internal static SyntaxTokenList ProtectedTokenList {
            get { return SyntaxFactory.TokenList(ProtectedToken); }
        }
        internal static SyntaxTokenList ProtectedOverrideTokenList {
            get { return SyntaxFactory.TokenList(ProtectedToken, OverrideToken); }
        }
        internal static SyntaxTokenList InternalTokenList {
            get { return SyntaxFactory.TokenList(InternalToken); }
        }
        internal static SyntaxTokenList InternalReadOnlyTokenList {
            get { return SyntaxFactory.TokenList(InternalToken, ReadOnlyToken); }
        }
        internal static SyntaxTokenList InternalSealedTokenList {
            get { return SyntaxFactory.TokenList(InternalToken, SealedToken); }
        }
        internal static SyntaxTokenList InternalStaticTokenList {
            get { return SyntaxFactory.TokenList(InternalToken, StaticToken); }
        }
        internal static SyntaxTokenList ProtectedInternalTokenList {
            get { return SyntaxFactory.TokenList(ProtectedToken, InternalToken); }
        }
        internal static SyntaxTokenList PrivateTokenList {
            get { return SyntaxFactory.TokenList(PrivateToken); }
        }
        internal static SyntaxTokenList PrivateStaticTokenList {
            get { return SyntaxFactory.TokenList(PrivateToken, StaticToken); }
        }
        internal static SyntaxTokenList PrivateStaticReadOnlyTokenList {
            get { return SyntaxFactory.TokenList(PrivateToken, StaticToken, ReadOnlyToken); }
        }
        internal static SyntaxTokenList PrivateStaticVolatileTokenList {
            get { return SyntaxFactory.TokenList(PrivateToken, StaticToken, VolatileToken); }
        }
        internal static SyntaxTokenList PrivateReadOnlyTokenList {
            get { return SyntaxFactory.TokenList(PrivateToken, ReadOnlyToken); }
        }
        internal static SyntaxTokenList PrivateVolatileTokenList {
            get { return SyntaxFactory.TokenList(PrivateToken, VolatileToken); }
        }
        internal static SyntaxTokenList PartialTokenList {
            get { return SyntaxFactory.TokenList(PartialToken); }
        }
        internal static SyntaxTokenList RefTokenList {
            get { return SyntaxFactory.TokenList(RefToken); }
        }
        internal static SyntaxTokenList OutTokenList {
            get { return SyntaxFactory.TokenList(OutToken); }
        }
        internal static SyntaxTokenList ThisTokenList {
            get { return SyntaxFactory.TokenList(ThisToken); }
        }
        internal static SyntaxTokenList ParamsTokenList {
            get { return SyntaxFactory.TokenList(ParamsToken); }
        }
        //
        //
        private static SyntaxList<ArrayRankSpecifierSyntax> OneDimArrayTypeRankSpecifiers {
            get { return SyntaxFactory.SingletonList(SyntaxFactory.ArrayRankSpecifier(SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(SyntaxFactory.OmittedArraySizeExpression()))); }
        }
        internal static ArrayTypeSyntax OneDimArrayType(TypeSyntax elementType) {
            return SyntaxFactory.ArrayType(elementType, OneDimArrayTypeRankSpecifiers);
        }
        //
        internal static IdentifierNameSyntax VarIdName {
            get { return IdName("var"); }
        }
        internal static IdentifierNameSyntax GlobalIdName {
            get { return IdName(SyntaxFactory.Token(SyntaxKind.GlobalKeyword)); }
        }
        internal static PredefinedTypeSyntax VoidType {
            get { return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)); }
        }
        internal static PredefinedTypeSyntax ObjectType {
            get { return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)); }
        }
        internal static ArrayTypeSyntax ObjectArrayType {
            get { return OneDimArrayType(ObjectType); }
        }
        internal static LiteralExpressionSyntax NullLiteral {
            get { return SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression); }
        }
        internal static ExpressionSyntax TryToLiteral(object value) {
            if (value == null) return NullLiteral;
            switch (Type.GetTypeCode(value.GetType())) {
                case TypeCode.String: return Literal((string)value);
                case TypeCode.Boolean: return Literal((bool)value);
                case TypeCode.DateTime: return Literal((DateTime)value);
                case TypeCode.Single: return Literal((float)value);
                case TypeCode.Double: return Literal((double)value);
                case TypeCode.Decimal: return Literal((decimal)value);
                case TypeCode.Int64: return Literal((long)value);
                case TypeCode.Int32: return Literal((int)value);
                case TypeCode.Int16: return Literal((short)value);
                case TypeCode.SByte: return Literal((sbyte)value);
                case TypeCode.UInt64: return Literal((ulong)value);
                case TypeCode.UInt32: return Literal((uint)value);
                case TypeCode.UInt16: return Literal((ushort)value);
                case TypeCode.Byte: return Literal((byte)value);
            }
            var bytes = value as byte[];
            if (bytes != null) return Literal(bytes);
            if (value is TimeSpan) return Literal((TimeSpan)value);
            //if (value is DateTimeOffset) return Literal((DateTimeOffset)value);
            var xNs = value as System.Xml.Linq.XNamespace;
            if (xNs != null) return Literal(xNs);
            //var xName = value as System.Xml.Linq.XName;
            //if (xName != null) return Literal(xName);
            //if (value is Guid) return Literal((Guid)value);
            //var uri = value as Uri;
            //if (uri != null) return Literal(uri);
            return null;
        }
        //
        internal static PredefinedTypeSyntax BoolType {
            get { return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)); }
        }
        internal static NullableTypeSyntax BoolNullableType {
            get { return SyntaxFactory.NullableType(BoolType); }
        }
        internal static LiteralExpressionSyntax TrueLiteral {
            get { return SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression); }
        }
        internal static LiteralExpressionSyntax FalseLiteral {
            get { return SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression); }
        }
        internal static LiteralExpressionSyntax Literal(bool value) {
            return value ? TrueLiteral : FalseLiteral;
        }
        internal static ExpressionSyntax Literal(bool? value) {
            return SyntaxFactory.CastExpression(BoolNullableType, value == null ? NullLiteral : Literal(value.Value));
        }
        //
        internal static PredefinedTypeSyntax StringType {
            get { return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)); }
        }
        internal static ArrayTypeSyntax StringArrayType {
            get { return OneDimArrayType(StringType); }
        }
        internal static ExpressionSyntax Literal(string value) {
            if (value == null) return SyntaxFactory.CastExpression(StringType, NullLiteral);
            return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(value));
        }
        //internal static ExpressionSyntax Literal(string[] value) {
        //    if (value == null) return SyntaxFactory.CastExpression(StringArrayType, NullLiteral);
        //    return 
        //}
        //
        internal static PredefinedTypeSyntax IntType {
            get { return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)); }
        }
        internal static NullableTypeSyntax IntNullableType {
            get { return SyntaxFactory.NullableType(IntType); }
        }
        internal static LiteralExpressionSyntax Literal(int value) {
            return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(value));
        }
        internal static ExpressionSyntax Literal(int? value) {
            return SyntaxFactory.CastExpression(IntNullableType, value == null ? NullLiteral : Literal(value.Value));
        }
        //
        internal static PredefinedTypeSyntax UIntType {
            get { return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.UIntKeyword)); }
        }
        internal static NullableTypeSyntax UIntNullableType {
            get { return SyntaxFactory.NullableType(UIntType); }
        }
        internal static LiteralExpressionSyntax Literal(uint value) {
            return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(value));
        }
        internal static ExpressionSyntax Literal(uint? value) {
            return SyntaxFactory.CastExpression(UIntNullableType, value == null ? NullLiteral : Literal(value.Value));
        }
        //
        internal static PredefinedTypeSyntax LongType {
            get { return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.LongKeyword)); }
        }
        internal static NullableTypeSyntax LongNullableType {
            get { return SyntaxFactory.NullableType(LongType); }
        }
        internal static LiteralExpressionSyntax Literal(long value) {
            return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(value));
        }
        internal static ExpressionSyntax Literal(long? value) {
            return SyntaxFactory.CastExpression(LongNullableType, value == null ? NullLiteral : Literal(value.Value));
        }
        //
        internal static PredefinedTypeSyntax ULongType {
            get { return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ULongKeyword)); }
        }
        internal static NullableTypeSyntax ULongNullableType {
            get { return SyntaxFactory.NullableType(ULongType); }
        }
        internal static LiteralExpressionSyntax Literal(ulong value) {
            return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(value));
        }
        internal static ExpressionSyntax Literal(ulong? value) {
            return SyntaxFactory.CastExpression(ULongNullableType, value == null ? NullLiteral : Literal(value.Value));
        }
        //
        internal static PredefinedTypeSyntax ShortType {
            get { return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ShortKeyword)); }
        }
        internal static NullableTypeSyntax ShortNullableType {
            get { return SyntaxFactory.NullableType(ShortType); }
        }
        internal static ExpressionSyntax Literal(short value) {
            return SyntaxFactory.CastExpression(ShortType, Literal((int)value));
        }
        internal static ExpressionSyntax Literal(short? value) {
            return SyntaxFactory.CastExpression(ShortNullableType, value == null ? NullLiteral : Literal((int)value.Value));
        }
        //
        internal static PredefinedTypeSyntax UShortType {
            get { return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.UShortKeyword)); }
        }
        internal static NullableTypeSyntax UShortNullableType {
            get { return SyntaxFactory.NullableType(UShortType); }
        }
        internal static ExpressionSyntax Literal(ushort value) {
            return SyntaxFactory.CastExpression(UShortType, Literal((int)value));
        }
        internal static ExpressionSyntax Literal(ushort? value) {
            return SyntaxFactory.CastExpression(UShortNullableType, value == null ? NullLiteral : Literal((int)value.Value));
        }
        //
        internal static PredefinedTypeSyntax ByteType {
            get { return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ByteKeyword)); }
        }
        internal static NullableTypeSyntax ByteNullableType {
            get { return SyntaxFactory.NullableType(ByteType); }
        }
        internal static ArrayTypeSyntax ByteArrayType {
            get { return OneDimArrayType(ByteType); }
        }
        internal static ExpressionSyntax Literal(byte value) {
            return SyntaxFactory.CastExpression(ByteType, Literal((int)value));
        }
        internal static ExpressionSyntax Literal(byte? value) {
            return SyntaxFactory.CastExpression(ByteNullableType, value == null ? NullLiteral : Literal((int)value.Value));
        }
        internal static ExpressionSyntax Literal(byte[] value) {
            if (value == null) return SyntaxFactory.CastExpression(ByteArrayType, NullLiteral);
            return NewArrExpr(ByteArrayType, value.Select(i => Literal((int)i)));
        }
        //
        internal static PredefinedTypeSyntax SByteType {
            get { return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.SByteKeyword)); }
        }
        internal static NullableTypeSyntax SByteNullableType {
            get { return SyntaxFactory.NullableType(SByteType); }
        }
        internal static ExpressionSyntax Literal(sbyte value) {
            return SyntaxFactory.CastExpression(SByteType, Literal((int)value));
        }
        internal static ExpressionSyntax Literal(sbyte? value) {
            return SyntaxFactory.CastExpression(SByteNullableType, value == null ? NullLiteral : Literal((int)value.Value));
        }
        //
        internal static PredefinedTypeSyntax DecimalType {
            get { return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.DecimalKeyword)); }
        }
        internal static NullableTypeSyntax DecimalNullableType {
            get { return SyntaxFactory.NullableType(DecimalType); }
        }
        internal static LiteralExpressionSyntax Literal(decimal value) {
            return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(value));
        }
        internal static ExpressionSyntax Literal(decimal? value) {
            return SyntaxFactory.CastExpression(DecimalNullableType, value == null ? NullLiteral : Literal(value.Value));
        }
        //
        internal static PredefinedTypeSyntax FloatType {
            get { return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.FloatKeyword)); }
        }
        internal static NullableTypeSyntax FloatNullableType {
            get { return SyntaxFactory.NullableType(FloatType); }
        }
        internal static LiteralExpressionSyntax Literal(float value) {
            return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(value));
        }
        internal static ExpressionSyntax Literal(float? value) {
            return SyntaxFactory.CastExpression(FloatNullableType, value == null ? NullLiteral : Literal(value.Value));
        }
        //
        internal static PredefinedTypeSyntax DoubleType {
            get { return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.DoubleKeyword)); }
        }
        internal static NullableTypeSyntax DoubleNullableType {
            get { return SyntaxFactory.NullableType(DoubleType); }
        }
        internal static LiteralExpressionSyntax Literal(double value) {
            return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(value));
        }
        internal static ExpressionSyntax Literal(double? value) {
            return SyntaxFactory.CastExpression(DoubleNullableType, value == null ? NullLiteral : Literal(value.Value));
        }
        //
        //global::XXX
        internal static AliasQualifiedNameSyntax GlobalAliasQualifiedName(SimpleNameSyntax name) {
            return SyntaxFactory.AliasQualifiedName(GlobalIdName, name);
        }
        internal static AliasQualifiedNameSyntax GlobalAliasQualifiedName(string name) {
            return GlobalAliasQualifiedName(IdName(name));
        }
        internal static QualifiedNameSyntax QualifiedName(NameSyntax left, string right) {
            return SyntaxFactory.QualifiedName(left, IdName(right));
        }
        internal static GenericNameSyntax GenericName(string identifier, IEnumerable<TypeSyntax> types) {
            return SyntaxFactory.GenericName(Id(identifier), SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(types)));
        }
        internal static GenericNameSyntax GenericName(string identifier, params TypeSyntax[] types) {
            return GenericName(identifier, (IEnumerable<TypeSyntax>)types);
        }
        internal static GenericNameSyntax GenericName(string identifier, TypeSyntax type) {
            return SyntaxFactory.GenericName(Id(identifier), SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(type)));
        }
        //private static TypeArgumentListSyntax TypeArgumentList(IEnumerable<TypeSyntax> types) {
        //    return SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(types));
        //}
        //internal static TypeArgumentListSyntax TypeArgumentList(params TypeSyntax[] types) {
        //    return TypeArgumentList((IEnumerable<TypeSyntax>)types);
        //}
        //
        //global::System
        internal static AliasQualifiedNameSyntax GlobalSystemName {
            get { return GlobalAliasQualifiedName("System"); }
        }
        //global::System.DateTime
        internal static QualifiedNameSyntax DateTimeName {
            get { return QualifiedName(GlobalSystemName, "DateTime"); }
        }
        internal static NullableTypeSyntax DateTimeNullableType {
            get { return SyntaxFactory.NullableType(DateTimeName); }
        }
        //global::System.DateTimeKind
        internal static QualifiedNameSyntax DateTimeKindName {
            get { return QualifiedName(GlobalSystemName, "DateTimeKind"); }
        }
        internal static ExpressionSyntax Literal(DateTimeKind value) {
            return SyntaxFactory.CastExpression(DateTimeKindName, Literal((int)value));
        }
        internal static ExpressionSyntax Literal(DateTime value) {
            return NewObjExpr(DateTimeName, Literal(value.Ticks), Literal(value.Kind));
        }
        internal static ExpressionSyntax Literal(DateTime? value) {
            return SyntaxFactory.CastExpression(DateTimeNullableType, value == null ? NullLiteral : Literal(value.Value));
        }
        //global::System.DateTimeOffset
        internal static QualifiedNameSyntax DateTimeOffsetName {
            get { return QualifiedName(GlobalSystemName, "DateTimeOffset"); }
        }
        internal static NullableTypeSyntax DateTimeOffsetNullableType {
            get { return SyntaxFactory.NullableType(DateTimeOffsetName); }
        }
        internal static ExpressionSyntax Literal(DateTimeOffset value) {
            return NewObjExpr(DateTimeOffsetName, Literal(value.Ticks), Literal(value.Offset));
        }
        internal static ExpressionSyntax Literal(DateTimeOffset? value) {
            return SyntaxFactory.CastExpression(DateTimeOffsetNullableType, value == null ? NullLiteral : Literal(value.Value));
        }
        //global::System.TimeSpan
        internal static QualifiedNameSyntax TimeSpanName {
            get { return QualifiedName(GlobalSystemName, "TimeSpan"); }
        }
        internal static NullableTypeSyntax TimeSpanNullableType {
            get { return SyntaxFactory.NullableType(TimeSpanName); }
        }
        internal static ExpressionSyntax Literal(TimeSpan value) {
            return NewObjExpr(TimeSpanName, Literal(value.Ticks));
        }
        internal static ExpressionSyntax Literal(TimeSpan? value) {
            return SyntaxFactory.CastExpression(TimeSpanNullableType, value == null ? NullLiteral : Literal(value.Value));
        }
        //global::System.Guid
        internal static QualifiedNameSyntax GuidName {
            get { return QualifiedName(GlobalSystemName, "Guid"); }
        }
        internal static NullableTypeSyntax GuidNullableType {
            get { return SyntaxFactory.NullableType(GuidName); }
        }
        internal static ExpressionSyntax Literal(Guid value) {
            return NewObjExpr(GuidName, Literal(value.ToByteArray()));
        }
        internal static ExpressionSyntax Literal(Guid? value) {
            return SyntaxFactory.CastExpression(GuidNullableType, value == null ? NullLiteral : Literal(value.Value));
        }
        //global::System.Uri
        internal static QualifiedNameSyntax UriName {
            get { return QualifiedName(GlobalSystemName, "Uri"); }
        }
        internal static ExpressionSyntax Literal(Uri value) {
            if (value == null) return SyntaxFactory.CastExpression(UriName, NullLiteral);
            return NewObjExpr(UriName, Literal(value.ToString()));
        }
        //global::System.IDisposable
        internal static QualifiedNameSyntax IDisposableName {
            get { return QualifiedName(GlobalSystemName, "IDisposable"); }
        }
        //global::System.Action
        internal static QualifiedNameSyntax ActionName {
            get { return QualifiedName(GlobalSystemName, "Action"); }
        }
        //global::System.Action<...>
        internal static QualifiedNameSyntax ActionOf(IEnumerable<TypeSyntax> types) {
            return SyntaxFactory.QualifiedName(GlobalSystemName, GenericName("Action", types));
        }
        internal static QualifiedNameSyntax ActionOf(params TypeSyntax[] types) {
            return ActionOf((IEnumerable<TypeSyntax>)types);
        }
        internal static QualifiedNameSyntax ActionOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemName, GenericName("Action", type));
        }
        //global::System.Func<...>
        internal static QualifiedNameSyntax FuncOf(IEnumerable<TypeSyntax> types) {
            return SyntaxFactory.QualifiedName(GlobalSystemName, GenericName("Func", types));
        }
        internal static QualifiedNameSyntax FuncOf(params TypeSyntax[] types) {
            return FuncOf((IEnumerable<TypeSyntax>)types);
        }
        internal static QualifiedNameSyntax FuncOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemName, GenericName("Func", type));
        }

        //global::System.Exception
        internal static QualifiedNameSyntax ExceptionName {
            get { return QualifiedName(GlobalSystemName, "Exception"); }
        }
        //global::System.SerializableAttribute
        internal static QualifiedNameSyntax SerializableAttributeName {
            get { return QualifiedName(GlobalSystemName, "SerializableAttribute"); }
        }
        internal static AttributeListSyntax SerializableAttributeList {
            get { return AttributeList(SerializableAttributeName); }
        }
        //
        //global::System.Attribute
        internal static QualifiedNameSyntax AttributeName {
            get { return QualifiedName(GlobalSystemName, "Attribute"); }
        }
        //global::System.AttributeUsageAttribute
        internal static QualifiedNameSyntax AttributeUsageAttributeName {
            get { return QualifiedName(GlobalSystemName, "AttributeUsageAttribute"); }
        }
        //global::System.AttributeTargets
        internal static QualifiedNameSyntax AttributeTargetsName {
            get { return QualifiedName(GlobalSystemName, "AttributeTargets"); }
        }
        internal static ExpressionSyntax Literal(AttributeTargets value) {
            return SyntaxFactory.CastExpression(AttributeTargetsName, Literal((int)value));
        }
        //global::System.Reflection
        internal static QualifiedNameSyntax GlobalSystemReflectionName {
            get { return QualifiedName(GlobalSystemName, "Reflection"); }
        }
        //global::System.Reflection.PropertyInfo
        internal static QualifiedNameSyntax PropertyInfoName {
            get { return QualifiedName(GlobalSystemReflectionName, "PropertyInfo"); }
        }
        //global::System.Reflection.BindingFlags
        internal static QualifiedNameSyntax BindingFlagsName {
            get { return QualifiedName(GlobalSystemReflectionName, "BindingFlags"); }
        }
        internal static ExpressionSyntax Literal(System.Reflection.BindingFlags value) {
            return SyntaxFactory.CastExpression(BindingFlagsName, Literal((int)value));
        }

        //global::System.Collections
        internal static QualifiedNameSyntax GlobalSystemCollectionName {
            get { return QualifiedName(GlobalSystemName, "Collections"); }
        }
        //global::System.Collections.IEnumerable
        internal static QualifiedNameSyntax IEnumerableName {
            get { return QualifiedName(GlobalSystemCollectionName, "IEnumerable"); }
        }
        //global::System.Collections.IEnumerator
        internal static QualifiedNameSyntax IEnumeratorName {
            get { return QualifiedName(GlobalSystemCollectionName, "IEnumerator"); }
        }
        //
        //global::System.Collections.Generic
        internal static QualifiedNameSyntax GlobalSystemCollectionGenericName {
            get { return QualifiedName(GlobalSystemCollectionName, "Generic"); }
        }
        //global::System.Collection.Generic.IEnumerable<T>
        internal static QualifiedNameSyntax IEnumerableOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemCollectionGenericName, GenericName("IEnumerable", type));
        }
        //global::System.Collection.Generic.IEnumerator<T>
        internal static QualifiedNameSyntax IEnumeratorOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemCollectionGenericName, GenericName("IEnumerator", type));
        }
        //global::System.Collection.Generic.ICollection<T>
        internal static QualifiedNameSyntax ICollectionOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemCollectionGenericName, GenericName("ICollection", type));
        }
        //global::System.Collection.Generic.IList<T>
        internal static QualifiedNameSyntax IListOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemCollectionGenericName, GenericName("IList", type));
        }
        //global::System.Collection.Generic.IReadOnlyList<T>
        internal static QualifiedNameSyntax IReadOnlyListOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemCollectionGenericName, GenericName("IReadOnlyList", type));
        }
        //global::System.Collection.Generic.IDictionary<TKey,TValue>
        internal static QualifiedNameSyntax IDictionaryOf(TypeSyntax keyType, TypeSyntax valueType) {
            return SyntaxFactory.QualifiedName(GlobalSystemCollectionGenericName, GenericName("IDictionary", keyType, valueType));
        }
        //global::System.Collection.Generic.IReadOnlyDictionary<TKey,TValue>
        internal static QualifiedNameSyntax IReadOnlyDictionaryOf(TypeSyntax keyType, TypeSyntax valueType) {
            return SyntaxFactory.QualifiedName(GlobalSystemCollectionGenericName, GenericName("IReadOnlyDictionary", keyType, valueType));
        }
        //global::System.Collection.Generic.Dictionary<TKey,TValue>
        internal static QualifiedNameSyntax DictionaryOf(TypeSyntax keyType, TypeSyntax valueType) {
            return SyntaxFactory.QualifiedName(GlobalSystemCollectionGenericName, GenericName("Dictionary", keyType, valueType));
        }
        //global::System.Collection.Generic.KeyValuePair<TKey,TValue>
        internal static QualifiedNameSyntax KeyValuePairOf(TypeSyntax keyType, TypeSyntax valueType) {
            return SyntaxFactory.QualifiedName(GlobalSystemCollectionGenericName, GenericName("KeyValuePair", keyType, valueType));
        }

        //global::System.Collections.ObjectModel
        internal static QualifiedNameSyntax GlobalSystemCollectionObjectModelName {
            get { return QualifiedName(GlobalSystemCollectionName, "ObjectModel"); }
        }
        //global::System.Collection.ObjectModel.Collection<T>
        internal static QualifiedNameSyntax CollectionOf(TypeSyntax type) {
            return SyntaxFactory.QualifiedName(GlobalSystemCollectionObjectModelName, GenericName("Collection", type));
        }

        //
        //global::System.Text
        internal static QualifiedNameSyntax GlobalSystemTextName {
            get { return QualifiedName(GlobalSystemName, "Text"); }
        }
        //global::System.Text.RegularExpressions
        internal static QualifiedNameSyntax GlobalSystemTextRegularExpressionsName {
            get { return QualifiedName(GlobalSystemTextName, "RegularExpressions"); }
        }
        //global::System.Text.RegularExpressions.Regex
        internal static QualifiedNameSyntax RegexName {
            get { return QualifiedName(GlobalSystemTextRegularExpressionsName, "Regex"); }
        }
        //global::System.Text.RegularExpressions.RegexOptions
        internal static QualifiedNameSyntax RegexOptionsName {
            get { return QualifiedName(GlobalSystemTextRegularExpressionsName, "RegexOptions"); }
        }
        internal static ExpressionSyntax Literal(System.Text.RegularExpressions.RegexOptions value) {
            return SyntaxFactory.CastExpression(RegexOptionsName, Literal((int)value));
        }
        //
        //global::System.Xml
        internal static QualifiedNameSyntax GlobalSystemXmlName {
            get { return QualifiedName(GlobalSystemName, "Xml"); }
        }
        internal static QualifiedNameSyntax XmlReaderName {
            get { return QualifiedName(GlobalSystemXmlName, "XmlReader"); }
        }
        //global::System.Xml.Linq
        internal static QualifiedNameSyntax GlobalSystemXmlLinqName {
            get { return QualifiedName(GlobalSystemXmlName, "Linq"); }
        }
        //global::System.Xml.Linq.XName
        internal static QualifiedNameSyntax XNameName {
            get { return QualifiedName(GlobalSystemXmlLinqName, "XName"); }
        }
        internal static ExpressionSyntax Literal(System.Xml.Linq.XName value) {
            if (value == null) return SyntaxFactory.CastExpression(XNameName, NullLiteral);
            return InvoExpr(MemberAccessExpr(XNameName, "Get"), SyntaxFactory.Argument(Literal(value.LocalName)), SyntaxFactory.Argument(Literal(value.NamespaceName)));
        }
        //global::System.Xml.Linq.XNamespace
        internal static QualifiedNameSyntax XNamespaceName {
            get { return QualifiedName(GlobalSystemXmlLinqName, "XNamespace"); }
        }
        internal static ExpressionSyntax Literal(System.Xml.Linq.XNamespace value) {
            if (value == null) return SyntaxFactory.CastExpression(XNamespaceName, NullLiteral);
            return InvoExpr(MemberAccessExpr(XNamespaceName, "Get"), SyntaxFactory.Argument(Literal(value.NamespaceName)));
        }
        //
        //global::System.Data
        internal static QualifiedNameSyntax GlobalSystemDataName {
            get { return QualifiedName(GlobalSystemName, "Data"); }
        }
        //global::System.Data.Spatial
        internal static QualifiedNameSyntax GlobalSystemDataSpatialName {
            get { return QualifiedName(GlobalSystemDataName, "Spatial"); }
        }
        //global::System.Data.Spatial.DbGeography
        internal static QualifiedNameSyntax DbGeographyName {
            get { return QualifiedName(GlobalSystemDataSpatialName, "DbGeography"); }
        }
        //global::System.Data.Spatial.DbGeometry
        internal static QualifiedNameSyntax DbGeometryName {
            get { return QualifiedName(GlobalSystemDataSpatialName, "DbGeometry"); }
        }
        //
        //global::System.Transactions
        internal static QualifiedNameSyntax GlobalSystemTransactionsName {
            get { return QualifiedName(GlobalSystemName, "Transactions"); }
        }
        //global::System.Transactions.IsolationLevel
        internal static QualifiedNameSyntax IsolationLevelName {
            get { return QualifiedName(GlobalSystemTransactionsName, "IsolationLevel"); }
        }
        //
        //global::System.Runtime
        internal static QualifiedNameSyntax GlobalSystemRuntimeName {
            get { return QualifiedName(GlobalSystemName, "Runtime"); }
        }
        //global::System.Runtime.Serialization
        internal static QualifiedNameSyntax GlobalSystemRuntimeSerializationName {
            get { return QualifiedName(GlobalSystemRuntimeName, "Serialization"); }
        }
        //global::System.Runtime.Serialization.ISerializable
        internal static QualifiedNameSyntax ISerializableName {
            get { return QualifiedName(GlobalSystemRuntimeSerializationName, "ISerializable"); }
        }
        //global::System.Runtime.Serialization.SerializationInfo
        internal static QualifiedNameSyntax SerializationInfoName {
            get { return QualifiedName(GlobalSystemRuntimeSerializationName, "SerializationInfo"); }
        }
        //global::System.Runtime.Serialization.StreamingContext
        internal static QualifiedNameSyntax StreamingContextName {
            get { return QualifiedName(GlobalSystemRuntimeSerializationName, "StreamingContext"); }
        }
        //global::System.Runtime.Serialization.DataContractAttribute
        internal static QualifiedNameSyntax DataContractAttributeName {
            get { return QualifiedName(GlobalSystemRuntimeSerializationName, "DataContractAttribute"); }
        }
        //global::System.Runtime.Serialization.DataMemberAttribute
        internal static QualifiedNameSyntax DataMemberAttributeName {
            get { return QualifiedName(GlobalSystemRuntimeSerializationName, "DataMemberAttribute"); }
        }
        //global::System.Runtime.Serialization.KnownTypeAttribute
        internal static QualifiedNameSyntax KnownTypeAttributeName {
            get { return QualifiedName(GlobalSystemRuntimeSerializationName, "KnownTypeAttribute"); }
        }
        //global::System.Runtime.Serialization.OnSerializingAttribute
        internal static QualifiedNameSyntax OnSerializingAttributeName {
            get { return QualifiedName(GlobalSystemRuntimeSerializationName, "OnSerializingAttribute"); }
        }
        //global::System.Runtime.Serialization.OnSerializedAttribute
        internal static QualifiedNameSyntax OnSerializedAttributeName {
            get { return QualifiedName(GlobalSystemRuntimeSerializationName, "OnSerializedAttribute"); }
        }
        //global::System.Runtime.Serialization.OnDeserializedAttribute
        internal static QualifiedNameSyntax OnDeserializedAttributeName {
            get { return QualifiedName(GlobalSystemRuntimeSerializationName, "OnDeserializedAttribute"); }
        }
        //
        //global::Metah
        internal static AliasQualifiedNameSyntax GlobalMetahName {
            get { return GlobalAliasQualifiedName("Metah"); }
        }
        //
        //
        //global::System.NotImplementedException
        internal static QualifiedNameSyntax NotImplementedExceptionName {
            get { return QualifiedName(GlobalSystemName, "NotImplementedException"); }
        }
        //throw new global::System.NotImplementedException();
        internal static ThrowStatementSyntax ThrowNotImplemented {
            get { return SyntaxFactory.ThrowStatement(NewObjExpr(NotImplementedExceptionName)); }
        }
        //{throw new global::System.NotImplementedException();}
        internal static BlockSyntax ThrowNotImplementedBlock {
            get { return SyntaxFactory.Block(SyntaxFactory.SingletonList<StatementSyntax>(ThrowNotImplemented)); }
        }
        //global::System.NotSupportedException
        internal static QualifiedNameSyntax NotSupportedExceptionName {
            get { return QualifiedName(GlobalSystemName, "NotSupportedException"); }
        }
        //throw new global::System.NotSupportedException();
        internal static ThrowStatementSyntax ThrowNotSupported {
            get { return SyntaxFactory.ThrowStatement(NewObjExpr(NotSupportedExceptionName)); }
        }
        //{throw new global::System.NotSupportedException();}
        internal static BlockSyntax ThrowNotSupportedBlock {
            get { return SyntaxFactory.Block(SyntaxFactory.SingletonList<StatementSyntax>(ThrowNotSupported)); }
        }
        //global::System.ArgumentNullException
        internal static QualifiedNameSyntax ArgumentNullExceptionName {
            get { return QualifiedName(GlobalSystemName, "ArgumentNullException"); }
        }
        //throw new global::System.ArgumentNullException("paramName");
        internal static ThrowStatementSyntax ThrowArgumentNull(string paramName) {
            return SyntaxFactory.ThrowStatement(NewObjExpr(ArgumentNullExceptionName, Literal(paramName)));
        }
        //if(condition) throw new global::System.ArgumentNullException("paramName");
        internal static IfStatementSyntax IfThrowArgumentNull(ExpressionSyntax condition, string paramName) {
            return SyntaxFactory.IfStatement(condition, ThrowArgumentNull(paramName));
        }
        //if(paramName == null) throw new global::System.ArgumentNullException("paramName");
        internal static IfStatementSyntax IfNullThrowArgumentNull(string paramName) {
            return IfThrowArgumentNull(EqualsExpr(IdName(paramName), NullLiteral), paramName);
        }
        //global::System.InvalidOperationException
        internal static QualifiedNameSyntax InvalidOperationExceptionName {
            get { return QualifiedName(GlobalSystemName, "InvalidOperationException"); }
        }
        //throw new global::System.InvalidOperationException("message");
        internal static ThrowStatementSyntax ThrowInvalidOperation(string message) {
            return SyntaxFactory.ThrowStatement(NewObjExpr(InvalidOperationExceptionName, Literal(message)));
        }
        //if(condition) throw new global::System.InvalidOperationException("message");
        internal static IfStatementSyntax IfThrowInvalidOperation(ExpressionSyntax condition, string message) {
            return SyntaxFactory.IfStatement(condition, ThrowInvalidOperation(message));
        }
        //
        internal static VariableDeclarationSyntax VarDecl(TypeSyntax type, SyntaxToken identifier, ExpressionSyntax initializer = null) {
            return SyntaxFactory.VariableDeclaration(
                type: type,
                variables: SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.VariableDeclarator(
                        identifier: identifier,
                        argumentList: null,
                        initializer: initializer == null ? null : SyntaxFactory.EqualsValueClause(initializer))));
        }
        internal static VariableDeclarationSyntax VarDecl(TypeSyntax type, string identifier, ExpressionSyntax initializer = null) {
            return VarDecl(type, Id(identifier), initializer);
        }
        internal static LocalDeclarationStatementSyntax LocalDeclStm(TypeSyntax type, SyntaxToken identifier, ExpressionSyntax initializer = null) {
            return SyntaxFactory.LocalDeclarationStatement(VarDecl(type, identifier, initializer));
        }
        internal static LocalDeclarationStatementSyntax LocalDeclStm(TypeSyntax type, string identifier, ExpressionSyntax initializer = null) {
            return SyntaxFactory.LocalDeclarationStatement(VarDecl(type, identifier, initializer));
        }
        internal static LocalDeclarationStatementSyntax LocalConstDeclStm(TypeSyntax type, SyntaxToken identifier, ExpressionSyntax initializer) {
            return SyntaxFactory.LocalDeclarationStatement(ConstTokenList, VarDecl(type, identifier, initializer));
        }
        internal static TryStatementSyntax TryFinallyStm(IEnumerable<StatementSyntax> bodyStms, IEnumerable<StatementSyntax> finallyStms) {
            return SyntaxFactory.TryStatement(SyntaxFactory.Block(bodyStms),
                default(SyntaxList<CatchClauseSyntax>),
                SyntaxFactory.FinallyClause(SyntaxFactory.Block(finallyStms)));
        }
        internal static ReturnStatementSyntax ReturnStm(ExpressionSyntax expr) {
            return SyntaxFactory.ReturnStatement(expr);
        }
        internal static ExpressionStatementSyntax ExprStm(ExpressionSyntax expr) {
            return SyntaxFactory.ExpressionStatement(expr);
        }
        //>(..) => body
        internal static ParenthesizedLambdaExpressionSyntax ParedLambdaExpr(IEnumerable<ParameterSyntax> parameters, CSharpSyntaxNode body) {
            return SyntaxFactory.ParenthesizedLambdaExpression(ParameterList(parameters), body);
        }
        //>para => body
        internal static SimpleLambdaExpressionSyntax SimpleLambdaExpr(string para, CSharpSyntaxNode body) {
            return SyntaxFactory.SimpleLambdaExpression(Parameter(para), body);
        }
        //>para => { ... }
        internal static SimpleLambdaExpressionSyntax SimpleLambdaExpr(string para, IEnumerable<StatementSyntax> stms) {
            return SimpleLambdaExpr(para, SyntaxFactory.Block(stms));
        }
        //>((lambdaType)(() => block))();
        internal static InvocationExpressionSyntax InvoLambdaExpr(TypeSyntax lambdaType, BlockSyntax block) {
            return InvoExpr(ParedExpr(CastExpr(lambdaType, ParedExpr(ParedLambdaExpr(null, block)))));
        }
        //>obj.Method(para => body)
        internal static InvocationExpressionSyntax InvoWithLambdaArgExpr(ExpressionSyntax obj, string method, string para, CSharpSyntaxNode body) {
            return InvoExpr(MemberAccessExpr(obj, method), SimpleLambdaExpr(para, body));
        }
        //>obj.Method(para => { ... })
        internal static InvocationExpressionSyntax InvoWithLambdaArgExpr(ExpressionSyntax obj, string method, string para, IEnumerable<StatementSyntax> stms) {
            return InvoWithLambdaArgExpr(obj, method, para, SyntaxFactory.Block(stms));
        }
        //>new type(para => body)
        internal static ObjectCreationExpressionSyntax NewObjWithLambdaArgExpr(TypeSyntax type, string para, CSharpSyntaxNode body) {
            return NewObjExpr(type, SimpleLambdaExpr(para, body));
        }
        //>new type(para => { ... })
        internal static ObjectCreationExpressionSyntax NewObjWithLambdaArgExpr(TypeSyntax type, string para, IEnumerable<StatementSyntax> stms) {
            return NewObjWithLambdaArgExpr(type, para, SyntaxFactory.Block(stms));
        }
        //>obj.property = value;
        internal static ExpressionStatementSyntax AssignStm(ExpressionSyntax obj, string property, ExpressionSyntax value) {
            return AssignStm(MemberAccessExpr(obj, property), value);
        }
        //>obj.Method(...)
        internal static InvocationExpressionSyntax InvoExpr(ExpressionSyntax obj, string method, params ExpressionSyntax[] argExprs) {
            return InvoExpr(MemberAccessExpr(obj, method), argExprs);
        }
        internal static InvocationExpressionSyntax InvoExpr(string obj, string method, params ExpressionSyntax[] argExprs) {
            return InvoExpr(IdName(obj), method, argExprs);
        }
        //>obj.Add(value);
        internal static ExpressionStatementSyntax AddInvoStm(ExpressionSyntax obj, ExpressionSyntax value) {
            return ExprStm(InvoExpr(obj, "Add", value));
        }
        //>obj.property.Add(value);
        internal static ExpressionStatementSyntax AddInvoStm(ExpressionSyntax obj, string property, ExpressionSyntax value) {
            return AddInvoStm(MemberAccessExpr(obj, property), value);
        }
        //>obj.property.Add(key, value);
        internal static ExpressionStatementSyntax AddInvoStm(ExpressionSyntax obj, string property, ExpressionSyntax key, ExpressionSyntax value) {
            return ExprStm(InvoExpr(MemberAccessExpr(MemberAccessExpr(obj, property), "Add"), key, value));
        }

        //Parentheses
        internal static ParenthesizedExpressionSyntax ParedExpr(ExpressionSyntax expr) {
            return SyntaxFactory.ParenthesizedExpression(expr);
        }
        internal static SyntaxNode GetNonPareParent(this SyntaxNode node) {
            var parent = node.Parent;
            if (parent is ParenthesizedExpressionSyntax) return GetNonPareParent(parent);
            return parent;
        }
        internal static ExpressionSyntax StripPareExpr(this ExpressionSyntax expr) {
            var paredExpr = expr as ParenthesizedExpressionSyntax;
            if (paredExpr != null) return StripPareExpr(paredExpr.Expression);
            return expr;
        }
        internal static T AsNonPareExpr<T>(this ExpressionSyntax expr) where T : ExpressionSyntax {
            var t = expr as T;
            if (t != null) return t;
            var paredExpr = expr as ParenthesizedExpressionSyntax;
            if (paredExpr != null) return AsNonPareExpr<T>(paredExpr.Expression);
            return null;
        }
        internal static CastExpressionSyntax CastExpr(TypeSyntax type, ExpressionSyntax expr) {
            return SyntaxFactory.CastExpression(type, expr);
        }
        internal static AssignmentExpressionSyntax AssignExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, left, right);
        }
        internal static AssignmentExpressionSyntax AssignExpr(string left, ExpressionSyntax right) {
            return AssignExpr(IdName(left), right);
        }
        internal static ExpressionStatementSyntax AssignStm(ExpressionSyntax left, ExpressionSyntax right) {
            return ExprStm(AssignExpr(left, right));
        }
        internal static ExpressionStatementSyntax AssignStm(string left, ExpressionSyntax right) {
            return AssignStm(IdName(left), right);
        }
        //
        internal static BinaryExpressionSyntax AddExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, left, right);
        }
        internal static BinaryExpressionSyntax SubtractExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.SubtractExpression, left, right);
        }
        internal static BinaryExpressionSyntax MultiplyExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.MultiplyExpression, left, right);
        }
        internal static BinaryExpressionSyntax DivideExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.DivideExpression, left, right);
        }
        internal static BinaryExpressionSyntax ModuloExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.ModuloExpression, left, right);
        }
        internal static BinaryExpressionSyntax BitwiseAndExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.BitwiseAndExpression, left, right);
        }
        internal static BinaryExpressionSyntax LogicalAndExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression, left, right);
        }
        internal static BinaryExpressionSyntax BitwiseOrExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.BitwiseOrExpression, left, right);
        }
        internal static BinaryExpressionSyntax LogicalOrExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.LogicalOrExpression, left, right);
        }
        internal static BinaryExpressionSyntax ExclusiveOrExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.ExclusiveOrExpression, left, right);
        }
        internal static BinaryExpressionSyntax LeftShiftExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.LeftShiftExpression, left, right);
        }
        internal static BinaryExpressionSyntax RightShiftExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.RightShiftExpression, left, right);
        }
        internal static PrefixUnaryExpressionSyntax PreIncrementExpr(ExpressionSyntax operand) {
            return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.PreIncrementExpression, operand);
        }
        internal static PrefixUnaryExpressionSyntax PreDecrementExpr(ExpressionSyntax operand) {
            return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.PreDecrementExpression, operand);
        }
        internal static BinaryExpressionSyntax AsExpr(ExpressionSyntax left, TypeSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.AsExpression, left, right);
        }
        internal static BinaryExpressionSyntax EqualsExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression, left, right);
        }
        internal static BinaryExpressionSyntax NotEqualsExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.NotEqualsExpression, left, right);
        }
        internal static BinaryExpressionSyntax LessThanExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.LessThanExpression, left, right);
        }
        internal static BinaryExpressionSyntax GreaterThanExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.GreaterThanExpression, left, right);
        }
        internal static BinaryExpressionSyntax CoalesceExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return SyntaxFactory.BinaryExpression(SyntaxKind.CoalesceExpression, left, right);
        }
        //
        internal static MemberAccessExpressionSyntax MemberAccessExpr(ExpressionSyntax expression, SimpleNameSyntax name) {
            return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, expression, name);
        }
        internal static MemberAccessExpressionSyntax MemberAccessExpr(ExpressionSyntax expression, string name) {
            return MemberAccessExpr(expression, IdName(name));
        }
        internal static ElementAccessExpressionSyntax ElementAccessExpr(ExpressionSyntax expression, IEnumerable<ExpressionSyntax> argExprs) {
            return SyntaxFactory.ElementAccessExpression(expression, BracketedArgumentList(argExprs));
        }
        internal static ElementAccessExpressionSyntax ElementAccessExpr(ExpressionSyntax expression, params ExpressionSyntax[] argExprs) {
            return ElementAccessExpr(expression, (IEnumerable<ExpressionSyntax>)argExprs);
        }
        internal static MemberAccessExpressionSyntax BaseMemberAccessExpr(SimpleNameSyntax name) {
            return MemberAccessExpr(SyntaxFactory.BaseExpression(), name);
        }
        internal static MemberAccessExpressionSyntax BaseMemberAccessExpr(string name) {
            return MemberAccessExpr(SyntaxFactory.BaseExpression(), name);
        }
        internal static ElementAccessExpressionSyntax BaseElementAccessExpr(params ExpressionSyntax[] arguments) {
            return ElementAccessExpr(SyntaxFactory.BaseExpression(), arguments);
        }
        internal static InvocationExpressionSyntax InvoExpr(ExpressionSyntax expression) {
            return SyntaxFactory.InvocationExpression(expression, SyntaxFactory.ArgumentList());
        }
        internal static InvocationExpressionSyntax InvoExpr(ExpressionSyntax expression, IEnumerable<ArgumentSyntax> arguments) {
            return SyntaxFactory.InvocationExpression(expression, ArgumentList(arguments));
        }
        internal static InvocationExpressionSyntax InvoExpr(ExpressionSyntax expression, params ArgumentSyntax[] arguments) {
            return InvoExpr(expression, (IEnumerable<ArgumentSyntax>)arguments);
        }
        internal static InvocationExpressionSyntax InvoExpr(ExpressionSyntax expression, IEnumerable<ExpressionSyntax> argExprs) {
            return SyntaxFactory.InvocationExpression(expression, ArgumentList(argExprs));
        }
        internal static InvocationExpressionSyntax InvoExpr(ExpressionSyntax expression, params ExpressionSyntax[] argExprs) {
            return InvoExpr(expression, (IEnumerable<ExpressionSyntax>)argExprs);
        }

        //
        internal static ObjectCreationExpressionSyntax NewObjExpr(TypeSyntax type, IEnumerable<ExpressionSyntax> argExprs, InitializerExpressionSyntax initializer) {
            return SyntaxFactory.ObjectCreationExpression(type, ArgumentList(argExprs), initializer);
        }
        internal static ObjectCreationExpressionSyntax NewObjExpr(TypeSyntax type, IEnumerable<ExpressionSyntax> argExprs, IEnumerable<ExpressionSyntax> initExprs) {
            return NewObjExpr(type, argExprs, ObjectInitializer(initExprs));
        }
        internal static ObjectCreationExpressionSyntax NewObjExpr(TypeSyntax type, params ExpressionSyntax[] argExprs) {
            return NewObjExpr(type, (IEnumerable<ExpressionSyntax>)argExprs, (InitializerExpressionSyntax)null);
        }
        internal static ObjectCreationExpressionSyntax NewObjExpr(TypeSyntax type) {
            return SyntaxFactory.ObjectCreationExpression(type, SyntaxFactory.ArgumentList(), null);
        }
        internal static ObjectCreationExpressionSyntax NewObjWithCollInitExpr(TypeSyntax type, IEnumerable<ExpressionSyntax> initExprs) {
            return SyntaxFactory.ObjectCreationExpression(type, SyntaxFactory.ArgumentList(), CollectionInitializer(initExprs));
        }
        internal static ObjectCreationExpressionSyntax NewObjWithCollInitExpr(TypeSyntax type, IEnumerable<IEnumerable<ExpressionSyntax>> initExprs) {
            return SyntaxFactory.ObjectCreationExpression(type, SyntaxFactory.ArgumentList(), CollectionInitializer(initExprs));
        }
        private static InitializerExpressionSyntax ObjectInitializer(IEnumerable<ExpressionSyntax> exprs) {
            var exprList = SyntaxFactory.SeparatedList(exprs);
            if (exprList.Count == 0) return null;
            return SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, exprList);
        }
        private static InitializerExpressionSyntax CollectionInitializer(IEnumerable<ExpressionSyntax> exprs) {
            var exprList = SyntaxFactory.SeparatedList(exprs);
            if (exprList.Count == 0) return null;
            return SyntaxFactory.InitializerExpression(SyntaxKind.CollectionInitializerExpression, exprList);
        }
        private static InitializerExpressionSyntax CollectionInitializer(IEnumerable<IEnumerable<ExpressionSyntax>> exprs) {
            if (exprs == null) return null;
            var exprList = SyntaxFactory.SeparatedList<ExpressionSyntax>(exprs.Select(i =>
                SyntaxFactory.InitializerExpression(SyntaxKind.ComplexElementInitializerExpression, SyntaxFactory.SeparatedList(i))));
            if (exprList.Count == 0) return null;
            return SyntaxFactory.InitializerExpression(SyntaxKind.CollectionInitializerExpression, exprList);
        }
        internal static ArrayCreationExpressionSyntax NewArrExpr(ArrayTypeSyntax type, IEnumerable<ExpressionSyntax> initExprs) {
            return SyntaxFactory.ArrayCreationExpression(type, ArrayInitializer(SyntaxFactory.SeparatedList(initExprs)));
        }
        internal static ArrayCreationExpressionSyntax NewArrExpr(ArrayTypeSyntax type, params ExpressionSyntax[] initExprs) {
            return NewArrExpr(type, (IEnumerable<ExpressionSyntax>)initExprs);
        }
        internal static ExpressionSyntax NewArrOrNullExpr(ArrayTypeSyntax type, IEnumerable<ExpressionSyntax> initExprs) {
            var exprList = SyntaxFactory.SeparatedList(initExprs);
            if (exprList.Count == 0) return NullLiteral;
            return SyntaxFactory.ArrayCreationExpression(type, ArrayInitializer(exprList));
        }
        internal static ExpressionSyntax NewArrOrNullExpr(ArrayTypeSyntax type, params ExpressionSyntax[] initExprs) {
            return NewArrOrNullExpr(type, (IEnumerable<ExpressionSyntax>)initExprs);
        }
        private static InitializerExpressionSyntax ArrayInitializer(SeparatedSyntaxList<ExpressionSyntax> exprList) {
            return SyntaxFactory.InitializerExpression(SyntaxKind.ArrayInitializerExpression, exprList);
        }
        //
        //
        internal static ParameterSyntax Parameter(SyntaxTokenList modifiers, TypeSyntax type, SyntaxToken identifier, ExpressionSyntax @default = null) {
            return SyntaxFactory.Parameter(
                attributeLists: default(SyntaxList<AttributeListSyntax>),
                modifiers: modifiers,
                type: type,
                identifier: identifier,
                @default: @default == null ? null : SyntaxFactory.EqualsValueClause(@default));
        }
        internal static ParameterSyntax Parameter(SyntaxTokenList modifiers, TypeSyntax type, string identifier, ExpressionSyntax @default = null) {
            return Parameter(modifiers, type, Id(identifier), @default);
        }
        internal static ParameterSyntax Parameter(TypeSyntax type, string identifier, ExpressionSyntax @default = null) {
            return Parameter(default(SyntaxTokenList), type, identifier, @default);
        }
        internal static ParameterSyntax Parameter(string identifier) {
            return Parameter(null, identifier);
        }
        internal static ParameterSyntax OutParameter(TypeSyntax type, string identifier) {
            return Parameter(OutTokenList, type, identifier);
        }
        internal static ParameterSyntax RefParameter(TypeSyntax type, string identifier) {
            return Parameter(RefTokenList, type, identifier);
        }
        internal static ParameterSyntax ThisParameter(TypeSyntax type, string identifier) {
            return Parameter(ThisTokenList, type, identifier);
        }
        internal static ParameterListSyntax ParameterList(IEnumerable<ParameterSyntax> parameters) {
            return SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters));
        }
        internal static ParameterListSyntax ParameterList(params ParameterSyntax[] parameters) {
            return ParameterList((IEnumerable<ParameterSyntax>)parameters);
        }
        internal static BracketedParameterListSyntax BracketedParameterList(IEnumerable<ParameterSyntax> parameters) {
            return SyntaxFactory.BracketedParameterList(SyntaxFactory.SeparatedList(parameters));
        }
        internal static BracketedParameterListSyntax BracketedParameterList(params ParameterSyntax[] parameters) {
            return BracketedParameterList((IEnumerable<ParameterSyntax>)parameters);
        }
        internal static ArgumentSyntax OutArgument(string identifier) {
            return SyntaxFactory.Argument(null, OutToken, IdName(identifier));
        }
        internal static ArgumentSyntax RefArgument(string identifier) {
            return SyntaxFactory.Argument(null, RefToken, IdName(identifier));
        }
        internal static ArgumentListSyntax ArgumentList(IEnumerable<ArgumentSyntax> arguments) {
            return SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments));
        }
        internal static ArgumentListSyntax ArgumentList(params ArgumentSyntax[] arguments) {
            return ArgumentList((IEnumerable<ArgumentSyntax>)arguments);
        }
        internal static ArgumentListSyntax ArgumentList(IEnumerable<ExpressionSyntax> argExprs) {
            return ArgumentList(argExprs == null ? null : argExprs.Select(i => SyntaxFactory.Argument(i)));
        }
        internal static ArgumentListSyntax ArgumentList(params ExpressionSyntax[] argExprs) {
            return ArgumentList((IEnumerable<ExpressionSyntax>)argExprs);
        }
        internal static BracketedArgumentListSyntax BracketedArgumentList(IEnumerable<ExpressionSyntax> argExprs) {
            return SyntaxFactory.BracketedArgumentList(SyntaxFactory.SeparatedList(argExprs == null ? null : argExprs.Select(i => SyntaxFactory.Argument(i))));
        }
        internal static BracketedArgumentListSyntax BracketedArgumentList(params ExpressionSyntax[] argExprs) {
            return BracketedArgumentList((IEnumerable<ExpressionSyntax>)argExprs);
        }
        internal static TypeParameterListSyntax TypeParameterList(IEnumerable<TypeParameterSyntax> parameters) {
            var parameterList = SyntaxFactory.SeparatedList(parameters);
            if (parameterList.Count == 0) return null;
            return SyntaxFactory.TypeParameterList(parameterList);
        }
        internal static TypeParameterConstraintClauseSyntax TypeParameterConstraintClause(IdentifierNameSyntax name, IEnumerable<TypeParameterConstraintSyntax> constraints) {
            return SyntaxFactory.TypeParameterConstraintClause(name, SyntaxFactory.SeparatedList(constraints));
        }
        internal static TypeParameterConstraintClauseSyntax TypeParameterConstraintClause(IdentifierNameSyntax name, IEnumerable<TypeSyntax> types) {
            return TypeParameterConstraintClause(name, types.Select(type => SyntaxFactory.TypeConstraint(type)));
        }
        internal static TypeParameterConstraintClauseSyntax TypeParameterConstraintClause(IdentifierNameSyntax name, TypeParameterConstraintSyntax constraint) {
            return SyntaxFactory.TypeParameterConstraintClause(name, SyntaxFactory.SingletonSeparatedList(constraint));
        }
        internal static TypeParameterConstraintClauseSyntax TypeParameterConstraintClause(IdentifierNameSyntax name, TypeSyntax type) {
            return TypeParameterConstraintClause(name, SyntaxFactory.TypeConstraint(type));
        }
        internal static BaseListSyntax BaseList(TypeSyntax type) {
            return SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(SyntaxFactory.SimpleBaseType(type)));
        }
        internal static BaseListSyntax BaseList(IEnumerable<TypeSyntax> types) {
            if (types == null) return null;
            var baseTypeList = SyntaxFactory.SeparatedList<BaseTypeSyntax>(types.Select(type => SyntaxFactory.SimpleBaseType(type)));
            if (baseTypeList.Count == 0) return null;
            return SyntaxFactory.BaseList(baseTypeList);
        }
        internal static BaseListSyntax BaseList(params TypeSyntax[] types) {
            return BaseList((IEnumerable<TypeSyntax>)types);
        }
        internal static AttributeListSyntax AttributeList(NameSyntax name, params AttributeArgumentSyntax[] arguments) {
            return SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(name, SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(arguments)))));
        }
        internal static AttributeArgumentSyntax AttributeArgument(IdentifierNameSyntax name, ExpressionSyntax expr) {
            return SyntaxFactory.AttributeArgument(
                nameEquals: SyntaxFactory.NameEquals(name),
                nameColon: null,
                expression: expr);
        }
        internal static AttributeArgumentSyntax AttributeArgument(string name, ExpressionSyntax value) {
            return AttributeArgument(IdName(name), value);
        }
        private static AttributeListSyntax AttributeUsageAttributeList(AttributeTargets validOn, bool allowMultiple = false, bool inherited = true) {
            return AttributeList(AttributeUsageAttributeName,
                SyntaxFactory.AttributeArgument(Literal(validOn)),
                AttributeArgument("AllowMultiple", Literal(allowMultiple)),
                AttributeArgument("Inherited", Literal(inherited)));
        }
        internal static ClassDeclarationSyntax AttributeDecl(SyntaxTokenList modifiers, SyntaxToken identifier,
            IEnumerable<MemberDeclarationSyntax> members, AttributeTargets validOn, bool allowMultiple = false, bool inherited = true) {
            return Class(new[] { AttributeUsageAttributeList(validOn, allowMultiple, inherited) }, modifiers, identifier, new[] { AttributeName }, members);
        }

        //
        //
        internal static ClassDeclarationSyntax Class(IEnumerable<AttributeListSyntax> attributeLists, SyntaxTokenList modifiers, SyntaxToken identifier,
            IEnumerable<TypeParameterSyntax> typeParameters, IEnumerable<TypeSyntax> baseTypes, IEnumerable<TypeParameterConstraintClauseSyntax> constraintClauses,
            IEnumerable<MemberDeclarationSyntax> members) {
            return SyntaxFactory.ClassDeclaration(
                attributeLists: SyntaxFactory.List(attributeLists),
                modifiers: modifiers,
                identifier: identifier,
                typeParameterList: TypeParameterList(typeParameters),
                //parameterList: null,
                baseList: BaseList(baseTypes),
                constraintClauses: SyntaxFactory.List(constraintClauses),
                members: SyntaxFactory.List(members));
        }
        internal static ClassDeclarationSyntax Class(IEnumerable<AttributeListSyntax> attributeLists, SyntaxTokenList modifiers, SyntaxToken identifier,
            IEnumerable<TypeSyntax> baseTypes, IEnumerable<MemberDeclarationSyntax> members) {
            return Class(attributeLists, modifiers, identifier, null, baseTypes, null, members);
        }
        internal static ClassDeclarationSyntax Class(IEnumerable<AttributeListSyntax> attributeLists, SyntaxTokenList modifiers, string identifier,
            IEnumerable<TypeSyntax> baseTypes, IEnumerable<MemberDeclarationSyntax> members) {
            return Class(attributeLists, modifiers, Id(identifier), baseTypes, members);
        }
        internal static ClassDeclarationSyntax Class(IEnumerable<AttributeListSyntax> attributeLists, SyntaxTokenList modifiers, string identifier,
            IEnumerable<TypeSyntax> baseTypes, params MemberDeclarationSyntax[] members) {
            return Class(attributeLists, modifiers, identifier, baseTypes, (IEnumerable<MemberDeclarationSyntax>)members);
        }
        internal static PropertyDeclarationSyntax Property(IEnumerable<AttributeListSyntax> attributeLists, SyntaxTokenList modifiers,
            TypeSyntax type, SyntaxToken identifier, AccessorListSyntax accessorList) {
            return SyntaxFactory.PropertyDeclaration(
                attributeLists: SyntaxFactory.List(attributeLists),
                modifiers: modifiers,
                type: type,
                explicitInterfaceSpecifier: null,
                identifier: identifier,
                accessorList: accessorList,
                expressionBody: null,
                initializer: null);
        }
        internal static PropertyDeclarationSyntax Property(IEnumerable<AttributeListSyntax> attributeLists, SyntaxTokenList modifiers,
            TypeSyntax type, SyntaxToken identifier, bool getterOnly,
            SyntaxTokenList getterModifiers, IEnumerable<StatementSyntax> getterStatements,
            SyntaxTokenList setterModifiers = default(SyntaxTokenList), IEnumerable<StatementSyntax> setterStatements = null) {
            var getter = AccessorDecl(SyntaxKind.GetAccessorDeclaration, getterModifiers, getterStatements);
            AccessorDeclarationSyntax setter = null;
            if (!getterOnly) setter = AccessorDecl(SyntaxKind.SetAccessorDeclaration, setterModifiers, setterStatements);
            return Property(attributeLists, modifiers, type, identifier,
                SyntaxFactory.AccessorList(setter == null ? SyntaxFactory.SingletonList(getter) : SyntaxFactory.List(new[] { getter, setter }))
                );
        }
        internal static PropertyDeclarationSyntax Property(SyntaxTokenList modifiers, TypeSyntax type, SyntaxToken identifier, bool getterOnly,
            SyntaxTokenList getterModifiers, IEnumerable<StatementSyntax> getterStatements,
            SyntaxTokenList setterModifiers = default(SyntaxTokenList), IEnumerable<StatementSyntax> setterStatements = null) {
            return Property(null, modifiers, type, identifier, getterOnly, getterModifiers, getterStatements, setterModifiers, setterStatements);
        }
        internal static PropertyDeclarationSyntax Property(SyntaxTokenList modifiers, TypeSyntax type, string identifier, bool getterOnly,
            SyntaxTokenList getterModifiers, IEnumerable<StatementSyntax> getterStatements,
            SyntaxTokenList setterModifiers = default(SyntaxTokenList), IEnumerable<StatementSyntax> setterStatements = null) {
            return Property(modifiers, type, Id(identifier), getterOnly, getterModifiers, getterStatements, setterModifiers, setterStatements);
        }
        private static AccessorDeclarationSyntax AccessorDecl(SyntaxKind kind, SyntaxTokenList modifiers, IEnumerable<StatementSyntax> statements) {
            return SyntaxFactory.AccessorDeclaration(
                kind: kind,
                attributeLists: default(SyntaxList<AttributeListSyntax>),
                modifiers: modifiers,
                body: SyntaxFactory.Block(statements));
        }
        //{ get; set; }
        internal static AccessorListSyntax GetSetAccessorList {
            get {
                return SyntaxFactory.AccessorList(SyntaxFactory.List(new[] {
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration, default(SyntaxList<AttributeListSyntax>), default(SyntaxTokenList), SyntaxFactory.Token(SyntaxKind.GetKeyword), null, SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration, default(SyntaxList<AttributeListSyntax>), default(SyntaxTokenList), SyntaxFactory.Token(SyntaxKind.SetKeyword), null, SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                }));
            }
        }
        internal static IndexerDeclarationSyntax Indexer(SyntaxTokenList modifiers, TypeSyntax type, IEnumerable<ParameterSyntax> parameters, bool getterOnly,
            SyntaxTokenList getterModifiers, IEnumerable<StatementSyntax> getterStatements,
            SyntaxTokenList setterModifiers = default(SyntaxTokenList), IEnumerable<StatementSyntax> setterStatements = null) {
            var getter = AccessorDecl(SyntaxKind.GetAccessorDeclaration, getterModifiers, getterStatements);
            AccessorDeclarationSyntax setter = null;
            if (!getterOnly) setter = AccessorDecl(SyntaxKind.SetAccessorDeclaration, setterModifiers, setterStatements);
            return SyntaxFactory.IndexerDeclaration(
                attributeLists: default(SyntaxList<AttributeListSyntax>),
                modifiers: modifiers,
                type: type,
                explicitInterfaceSpecifier: null,
                parameterList: BracketedParameterList(parameters),
                accessorList: SyntaxFactory.AccessorList(setter == null ? SyntaxFactory.SingletonList(getter) : SyntaxFactory.List(new[] { getter, setter })));
        }
        internal static MethodDeclarationSyntax Method(IEnumerable<AttributeListSyntax> attributeLists, SyntaxTokenList modifiers,
            TypeSyntax returnType, SyntaxToken identifier, IEnumerable<TypeParameterSyntax> typeParameters,
            IEnumerable<ParameterSyntax> parameters, IEnumerable<TypeParameterConstraintClauseSyntax> constraintClauses,
            IEnumerable<StatementSyntax> statements) {
            return SyntaxFactory.MethodDeclaration(
                attributeLists: SyntaxFactory.List(attributeLists),
                modifiers: modifiers,
                returnType: returnType,
                explicitInterfaceSpecifier: null,
                identifier: identifier,
                typeParameterList: TypeParameterList(typeParameters),
                parameterList: ParameterList(parameters),
                constraintClauses: SyntaxFactory.List(constraintClauses),
                body: SyntaxFactory.Block(statements),
                expressionBody: null);
        }
        internal static MethodDeclarationSyntax Method(IEnumerable<AttributeListSyntax> attributeLists, SyntaxTokenList modifiers,
            TypeSyntax returnType, SyntaxToken identifier, IEnumerable<ParameterSyntax> parameters, IEnumerable<StatementSyntax> statements) {
            return Method(attributeLists, modifiers, returnType, identifier, null, parameters, null, statements);
        }
        internal static MethodDeclarationSyntax Method(SyntaxTokenList modifiers, TypeSyntax returnType, SyntaxToken identifier,
            IEnumerable<ParameterSyntax> parameters, IEnumerable<StatementSyntax> statements) {
            return Method(null, modifiers, returnType, identifier, parameters, statements);
        }
        internal static MethodDeclarationSyntax Method(SyntaxTokenList modifiers, TypeSyntax returnType, string identifier,
            IEnumerable<ParameterSyntax> parameters, params StatementSyntax[] statements) {
            return Method(modifiers, returnType, Id(identifier), parameters, statements);
        }
        internal static ConstructorDeclarationSyntax Constructor(SyntaxTokenList modifiers, SyntaxToken identifier, IEnumerable<ParameterSyntax> parameters,
            ConstructorInitializerSyntax initializer, IEnumerable<StatementSyntax> statements) {
            return SyntaxFactory.ConstructorDeclaration(
                attributeLists: default(SyntaxList<AttributeListSyntax>),
                modifiers: modifiers,
                identifier: identifier,
                parameterList: ParameterList(parameters),
                initializer: initializer,
                body: SyntaxFactory.Block(statements));
        }
        internal static ConstructorDeclarationSyntax Constructor(SyntaxTokenList modifiers, string identifier, IEnumerable<ParameterSyntax> parameters,
            ConstructorInitializerSyntax initializer, params StatementSyntax[] statements) {
            return Constructor(modifiers, Id(identifier), parameters, initializer, statements);
        }
        internal static ConstructorInitializerSyntax ConstructorInitializer(bool isBase, IEnumerable<ExpressionSyntax> argExprs) {
            return SyntaxFactory.ConstructorInitializer(
                kind: isBase ? SyntaxKind.BaseConstructorInitializer : SyntaxKind.ThisConstructorInitializer,
                argumentList: ArgumentList(argExprs));
        }
        internal static ConstructorInitializerSyntax ConstructorInitializer(bool isBase, params ExpressionSyntax[] argExprs) {
            return ConstructorInitializer(isBase, (IEnumerable<ExpressionSyntax>)argExprs);
        }
        internal static ConversionOperatorDeclarationSyntax ConversionOperator(bool isImplict, TypeSyntax type,
            IEnumerable<ParameterSyntax> parameters, IEnumerable<StatementSyntax> statements) {
            return SyntaxFactory.ConversionOperatorDeclaration(
                attributeLists: default(SyntaxList<AttributeListSyntax>),
                modifiers: PublicStaticTokenList,
                implicitOrExplicitKeyword: isImplict ? ImplictToken : ExplictToken,
                type: type,
                parameterList: ParameterList(parameters),
                body: SyntaxFactory.Block(statements),
                expressionBody: null);
        }
        internal static ConversionOperatorDeclarationSyntax ConversionOperator(bool isImplict, TypeSyntax type,
            IEnumerable<ParameterSyntax> parameters, params StatementSyntax[] statements) {
            return ConversionOperator(isImplict, type, parameters, (IEnumerable<StatementSyntax>)statements);
        }
        internal static FieldDeclarationSyntax Field(SyntaxTokenList modifiers, TypeSyntax type, SyntaxToken identifier, ExpressionSyntax initializer = null) {
            return SyntaxFactory.FieldDeclaration(
                attributeLists: default(SyntaxList<AttributeListSyntax>),
                modifiers: modifiers,
                declaration: VarDecl(type, identifier, initializer));
        }
        internal static FieldDeclarationSyntax Field(SyntaxTokenList modifiers, TypeSyntax type, string identifier, ExpressionSyntax initializer = null) {
            return Field(modifiers, type, Id(identifier), initializer);
        }


        //
        //
        //symbols
        //
        internal static TypeSyntax ToTypeSyntax(this ITypeSymbol symbol) {
            return SyntaxFactory.ParseTypeName(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }
        internal static NameSyntax ToNameSyntax(this ITypeSymbol symbol) {
            return SyntaxFactory.ParseName(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }
        internal static List<TypeParameterSyntax> ToTypeParameterSyntaxList(ImmutableArray<ITypeParameterSymbol> symbols,
            out List<TypeParameterConstraintClauseSyntax> constraintClauseList) {
            List<TypeParameterSyntax> parameterList = null;
            constraintClauseList = null;
            if (symbols.Length > 0) {
                parameterList = new List<TypeParameterSyntax>();
                foreach (var symbol in symbols) {
                    var identifier = Id(symbol.Name.EscapeIdentifier());
                    SyntaxToken varianceKeyword = default(SyntaxToken);
                    //switch (symbol.Variance) {
                    //    case VarianceKind.In:
                    //        varianceKeyword = InToken;
                    //        break;
                    //    case VarianceKind.Out:
                    //        varianceKeyword = OutToken;
                    //        break;
                    //}
                    parameterList.Add(SyntaxFactory.TypeParameter(
                        attributeLists: default(SyntaxList<AttributeListSyntax>),
                        varianceKeyword: varianceKeyword,
                        identifier: identifier));
                    //
                    List<TypeParameterConstraintSyntax> constraintList = null;
                    if (symbol.HasConstructorConstraint) {
                        Extensions.CreateAndAdd(ref constraintList, SyntaxFactory.ConstructorConstraint());
                    }
                    if (symbol.HasReferenceTypeConstraint) {
                        Extensions.CreateAndAdd(ref constraintList, SyntaxFactory.ClassOrStructConstraint(SyntaxKind.ClassConstraint));
                    }
                    else if (symbol.HasValueTypeConstraint) {
                        Extensions.CreateAndAdd(ref constraintList, SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint));
                    }
                    foreach (var ct in symbol.ConstraintTypes) {
                        Extensions.CreateAndAdd(ref constraintList, SyntaxFactory.TypeConstraint(ct.ToTypeSyntax()));
                    }
                    if (constraintList != null && constraintList.Count > 0) {
                        Extensions.CreateAndAdd(ref constraintClauseList, SyntaxFactory.TypeParameterConstraintClause(
                            name: IdName(identifier),
                            constraints: SyntaxFactory.SeparatedList(constraintList)));
                    }
                }
            }
            return parameterList;
        }
        //eg: symbol.IsFullNameEquals("List`1", "Generic", "Collections", "System")
        internal static bool IsFullNameEquals(this ISymbol symbol, params string[] nameParts) {
            if (symbol == null) throw new ArgumentNullException("symbol");
            if (nameParts == null || nameParts.Length == 0) throw new ArgumentNullException("nameParts");
            var idx = 0;
            for (; symbol != null; symbol = symbol.ContainingSymbol) {
                var name = symbol.MetadataName;
                if (string.IsNullOrEmpty(name)) break;
                if (idx == nameParts.Length) return false;
                if (name != nameParts[idx]) return false;
                idx++;
            }
            return idx == nameParts.Length;
        }
        //eg: var idx = symbol.MatchFullName(new []{"List`1", "Dictionary`2"}, new []{"Generic", "Collections", "System"});
        //return value: -1: none; 0: symbol is List`1; 1: symbol is Dictionary`2 
        internal static int MatchFullName(this ISymbol symbol, string[] typeNames, string[] outerNameParts) {
            if (symbol == null) throw new ArgumentNullException("symbol");
            if (typeNames == null || typeNames.Length == 0) throw new ArgumentNullException("typeNames");
            var fullLength = 1 + (outerNameParts != null ? outerNameParts.Length : 0);
            int idx = 0, result = -1;
            for (; symbol != null; symbol = symbol.ContainingSymbol) {
                var name = symbol.MetadataName;
                if (string.IsNullOrEmpty(name)) break;
                if (idx == fullLength) return -1;
                if (idx == 0) {
                    for (var i = 0; i < typeNames.Length; i++) {
                        if (name == typeNames[i]) {
                            result = i;
                            break;
                        }
                    }
                    if (result == -1) return -1;
                }
                else {
                    if (name != outerNameParts[idx - 1]) return -1;
                }
                idx++;
            }
            if (idx == fullLength) return result;
            return -1;
        }
        internal static INamedTypeSymbol TryGetBaseTypeSymbol(this INamedTypeSymbol symbol, params string[] fullNames) {
            if (symbol == null) throw new ArgumentNullException("symbol");
            //if (symbol.TypeKind != TypeKind.Class) return null;
            for (symbol = symbol.BaseType; symbol != null; symbol = symbol.BaseType) {
                if (symbol.IsFullNameEquals(fullNames)) return symbol;
            }
            return null;
        }
        internal static IPropertySymbol TryGetPropertySymbol(this INamedTypeSymbol symbol, string propertyName) {
            return symbol.GetMembers(propertyName).OfType<IPropertySymbol>().FirstOrDefault();
        }
        internal static IEnumerable<IPropertySymbol> GetPropertySymbols(this INamedTypeSymbol symbol) {
            return symbol.GetMembers().OfType<IPropertySymbol>();
        }
        internal static IEnumerable<IPropertySymbol> GetPropertySymbolsAfter(this INamedTypeSymbol thisSymbol, INamedTypeSymbol baseSymbol) {
            if (thisSymbol.BaseType.Equals(baseSymbol)) return thisSymbol.GetPropertySymbols();
            var pSymbolList = new List<IPropertySymbol>();
            for (; thisSymbol != null; thisSymbol = thisSymbol.BaseType) {
                if (thisSymbol.Equals(baseSymbol)) break;
                pSymbolList.InsertRange(0, thisSymbol.GetPropertySymbols());
            }
            return pSymbolList;
        }
        //
        //
        //
        internal static void ReportDiagnostics(IEnumerable<Diagnostic> diags, IEnumerable<string> nonCSFilePaths, bool errorAndWarningsOnly = true) {
            Dictionary<string, CompilationUnitSyntax> nonCSFilePathDict = null;
            foreach (var diag in diags) {
                var errorSeverity = (ErrorSeverity)diag.Severity;
                if (errorAndWarningsOnly && errorSeverity != ErrorSeverity.Error && errorSeverity != ErrorSeverity.Warning) continue;
                var location = diag.Location;
                SourceSpan resultSourceSpan = null;
                var syntaxTree = location.SourceTree;
                if (syntaxTree != null) {
                    if (nonCSFilePathDict == null) {
                        nonCSFilePathDict = new Dictionary<string, CompilationUnitSyntax>();
                        foreach (var i in nonCSFilePaths) {
                            nonCSFilePathDict.Add(i, null);
                        }
                    }
                    var filePath = syntaxTree.FilePath;
                    CompilationUnitSyntax cuSyntax;
                    if (nonCSFilePathDict.TryGetValue(filePath, out cuSyntax)) {
                        if (cuSyntax == null) {
                            cuSyntax = (CompilationUnitSyntax)syntaxTree.GetRoot();
                            nonCSFilePathDict[filePath] = cuSyntax;
                        }
                        var tokens = cuSyntax.DescendantTokens(location.SourceSpan).ToArray();
                        foreach (var token in tokens) {
                            var sourceSpan = token.GetSourceSpan();
                            if (sourceSpan != null) {
                                if (resultSourceSpan == null) {
                                    resultSourceSpan = sourceSpan;
                                }
                                else {
                                    resultSourceSpan = resultSourceSpan.MergeWith(sourceSpan);
                                }
                            }
                        }
                        if (resultSourceSpan == null && tokens.Length > 0) {
                            foreach (var token in tokens) {
                                for (var node = token.Parent; node != null; node = node.Parent) {
                                    var sourceSpan = node.GetSourceSpan();
                                    if (sourceSpan != null) {
                                        resultSourceSpan = sourceSpan;
                                        break;
                                    }
                                }
                                if (resultSourceSpan != null) break;
                            }
                        }
                        if (resultSourceSpan == null) {
                            resultSourceSpan = new SourceSpan(filePath, 0, 0, new SourcePosition(1, 1), new SourcePosition(1, 1));
                        }
                    }
                    else {//cs file
                        var sourceSpan = location.SourceSpan;
                        var lineSpan = location.GetLineSpan();
                        resultSourceSpan = new SourceSpan(lineSpan.Path, sourceSpan.Start, sourceSpan.Length, SourcePosition.From(lineSpan.StartLinePosition), SourcePosition.From(lineSpan.EndLinePosition));
                    }
                }
                CompilationContextBase.Report(new Error(CompilationContextBase.Subkind, errorSeverity, resultSourceSpan, 0, diag.Id, diag.GetMessage()));
            }
        }

    }

    public static class CSTokens {
        private volatile static HashSet<string> _keywordSet;
        public static HashSet<string> KeywordSet {
            get { return _keywordSet ?? (_keywordSet = new HashSet<string>(SyntaxFacts.GetKeywordKinds().Select(i => SyntaxFacts.GetText(i)))); }
        }
        //private volatile static HashSet<string> _reservedKeywordSet;
        //public static HashSet<string> ReservedKeywordSet {
        //    get { return _reservedKeywordSet ?? (_reservedKeywordSet = new HashSet<string>(SyntaxFacts.GetReservedKeywordKinds().Select(i => SyntaxFacts.GetText(i)))); }
        //}
        //public static string EscapeIdentifier(this string identifier) {
        //    if (string.IsNullOrEmpty(identifier)) throw new ArgumentNullException("identifier");
        //    if (ReservedKeywordSet.Contains(identifier)) return "@" + identifier;
        //    return identifier;
        //}
        internal static string EscapeIdentifier(this string identifier) {
            //if (string.IsNullOrEmpty(identifier)) throw new ArgumentNullException("identifier");
            if (SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None) return "@" + identifier;
            return identifier;
        }
        internal static string UnescapeIdentifier(this string identifier) {
            //if (string.IsNullOrEmpty(identifier)) throw new ArgumentNullException("identifier");
            return identifier[0] == '@' ? identifier.Substring(1) : identifier;
        }
        //
        //
        internal static readonly Node TildeTokenKind = Node.Atom(SyntaxKind.TildeToken);
        internal static readonly Node ExclamationTokenKind = Node.Atom(SyntaxKind.ExclamationToken);
        internal static readonly Node DollarTokenKind = Node.Atom(SyntaxKind.DollarToken);
        internal static readonly Node PercentTokenKind = Node.Atom(SyntaxKind.PercentToken);
        internal static readonly Node CaretTokenKind = Node.Atom(SyntaxKind.CaretToken);
        internal static readonly Node AmpersandTokenKind = Node.Atom(SyntaxKind.AmpersandToken);
        internal static readonly Node AsteriskTokenKind = Node.Atom(SyntaxKind.AsteriskToken);
        internal static readonly Node OpenParenTokenKind = Node.Atom(SyntaxKind.OpenParenToken);
        internal static readonly Node CloseParenTokenKind = Node.Atom(SyntaxKind.CloseParenToken);
        internal static readonly Node MinusTokenKind = Node.Atom(SyntaxKind.MinusToken);
        internal static readonly Node PlusTokenKind = Node.Atom(SyntaxKind.PlusToken);
        internal static readonly Node EqualsTokenKind = Node.Atom(SyntaxKind.EqualsToken);
        internal static readonly Node OpenBraceTokenKind = Node.Atom(SyntaxKind.OpenBraceToken);
        internal static readonly Node CloseBraceTokenKind = Node.Atom(SyntaxKind.CloseBraceToken);
        internal static readonly Node OpenBracketTokenKind = Node.Atom(SyntaxKind.OpenBracketToken);
        internal static readonly Node CloseBracketTokenKind = Node.Atom(SyntaxKind.CloseBracketToken);
        internal static readonly Node BarTokenKind = Node.Atom(SyntaxKind.BarToken);
        internal static readonly Node BackslashTokenKind = Node.Atom(SyntaxKind.BackslashToken);
        internal static readonly Node ColonTokenKind = Node.Atom(SyntaxKind.ColonToken);
        internal static readonly Node SemicolonTokenKind = Node.Atom(SyntaxKind.SemicolonToken);
        internal static readonly Node DoubleQuoteTokenKind = Node.Atom(SyntaxKind.DoubleQuoteToken);
        internal static readonly Node SingleQuoteTokenKind = Node.Atom(SyntaxKind.SingleQuoteToken);
        internal static readonly Node LessThanTokenKind = Node.Atom(SyntaxKind.LessThanToken);
        internal static readonly Node CommaTokenKind = Node.Atom(SyntaxKind.CommaToken);
        internal static readonly Node GreaterThanTokenKind = Node.Atom(SyntaxKind.GreaterThanToken);
        internal static readonly Node DotTokenKind = Node.Atom(SyntaxKind.DotToken);
        internal static readonly Node QuestionTokenKind = Node.Atom(SyntaxKind.QuestionToken);
        internal static readonly Node HashTokenKind = Node.Atom(SyntaxKind.HashToken);
        internal static readonly Node SlashTokenKind = Node.Atom(SyntaxKind.SlashToken);
        //internal static readonly Node SlashGreaterThanTokenKind = Node.Atom(SyntaxKind.SlashGreaterThanToken);
        //internal static readonly Node LessThanSlashTokenKind = Node.Atom(SyntaxKind.LessThanSlashToken);
        //internal static readonly Node XmlCommentStartTokenKind = Node.Atom(SyntaxKind.XmlCommentStartToken);
        //internal static readonly Node XmlCommentEndTokenKind = Node.Atom(SyntaxKind.XmlCommentEndToken);
        //internal static readonly Node XmlCDataStartTokenKind = Node.Atom(SyntaxKind.XmlCDataStartToken);
        //internal static readonly Node XmlCDataEndTokenKind = Node.Atom(SyntaxKind.XmlCDataEndToken);
        //internal static readonly Node XmlProcessingInstructionStartTokenKind = Node.Atom(SyntaxKind.XmlProcessingInstructionStartToken);
        //internal static readonly Node XmlProcessingInstructionEndTokenKind = Node.Atom(SyntaxKind.XmlProcessingInstructionEndToken);
        internal static readonly Node BarBarTokenKind = Node.Atom(SyntaxKind.BarBarToken);
        internal static readonly Node AmpersandAmpersandTokenKind = Node.Atom(SyntaxKind.AmpersandAmpersandToken);
        internal static readonly Node MinusMinusTokenKind = Node.Atom(SyntaxKind.MinusMinusToken);
        internal static readonly Node PlusPlusTokenKind = Node.Atom(SyntaxKind.PlusPlusToken);
        internal static readonly Node ColonColonTokenKind = Node.Atom(SyntaxKind.ColonColonToken);
        internal static readonly Node QuestionQuestionTokenKind = Node.Atom(SyntaxKind.QuestionQuestionToken);
        internal static readonly Node MinusGreaterThanTokenKind = Node.Atom(SyntaxKind.MinusGreaterThanToken);
        internal static readonly Node ExclamationEqualsTokenKind = Node.Atom(SyntaxKind.ExclamationEqualsToken);
        internal static readonly Node EqualsEqualsTokenKind = Node.Atom(SyntaxKind.EqualsEqualsToken);
        internal static readonly Node EqualsGreaterThanTokenKind = Node.Atom(SyntaxKind.EqualsGreaterThanToken);
        internal static readonly Node LessThanEqualsTokenKind = Node.Atom(SyntaxKind.LessThanEqualsToken);
        internal static readonly Node LessThanLessThanTokenKind = Node.Atom(SyntaxKind.LessThanLessThanToken);
        internal static readonly Node LessThanLessThanEqualsTokenKind = Node.Atom(SyntaxKind.LessThanLessThanEqualsToken);
        internal static readonly Node GreaterThanEqualsTokenKind = Node.Atom(SyntaxKind.GreaterThanEqualsToken);
        internal static readonly Node GreaterThanGreaterThanTokenKind = Node.Atom(SyntaxKind.GreaterThanGreaterThanToken);
        internal static readonly Node GreaterThanGreaterThanEqualsTokenKind = Node.Atom(SyntaxKind.GreaterThanGreaterThanEqualsToken);
        internal static readonly Node SlashEqualsTokenKind = Node.Atom(SyntaxKind.SlashEqualsToken);
        internal static readonly Node AsteriskEqualsTokenKind = Node.Atom(SyntaxKind.AsteriskEqualsToken);
        internal static readonly Node BarEqualsTokenKind = Node.Atom(SyntaxKind.BarEqualsToken);
        internal static readonly Node AmpersandEqualsTokenKind = Node.Atom(SyntaxKind.AmpersandEqualsToken);
        internal static readonly Node PlusEqualsTokenKind = Node.Atom(SyntaxKind.PlusEqualsToken);
        internal static readonly Node MinusEqualsTokenKind = Node.Atom(SyntaxKind.MinusEqualsToken);
        internal static readonly Node CaretEqualsTokenKind = Node.Atom(SyntaxKind.CaretEqualsToken);
        internal static readonly Node PercentEqualsTokenKind = Node.Atom(SyntaxKind.PercentEqualsToken);
        //
        internal static readonly Node BoolKeywordKind = Node.Atom(SyntaxKind.BoolKeyword);
        internal static readonly Node ByteKeywordKind = Node.Atom(SyntaxKind.ByteKeyword);
        internal static readonly Node SByteKeywordKind = Node.Atom(SyntaxKind.SByteKeyword);
        internal static readonly Node ShortKeywordKind = Node.Atom(SyntaxKind.ShortKeyword);
        internal static readonly Node UShortKeywordKind = Node.Atom(SyntaxKind.UShortKeyword);
        internal static readonly Node IntKeywordKind = Node.Atom(SyntaxKind.IntKeyword);
        internal static readonly Node UIntKeywordKind = Node.Atom(SyntaxKind.UIntKeyword);
        internal static readonly Node LongKeywordKind = Node.Atom(SyntaxKind.LongKeyword);
        internal static readonly Node ULongKeywordKind = Node.Atom(SyntaxKind.ULongKeyword);
        internal static readonly Node DoubleKeywordKind = Node.Atom(SyntaxKind.DoubleKeyword);
        internal static readonly Node FloatKeywordKind = Node.Atom(SyntaxKind.FloatKeyword);
        internal static readonly Node DecimalKeywordKind = Node.Atom(SyntaxKind.DecimalKeyword);
        internal static readonly Node StringKeywordKind = Node.Atom(SyntaxKind.StringKeyword);
        internal static readonly Node CharKeywordKind = Node.Atom(SyntaxKind.CharKeyword);
        internal static readonly Node VoidKeywordKind = Node.Atom(SyntaxKind.VoidKeyword);
        internal static readonly Node ObjectKeywordKind = Node.Atom(SyntaxKind.ObjectKeyword);
        internal static readonly Node TypeOfKeywordKind = Node.Atom(SyntaxKind.TypeOfKeyword);
        internal static readonly Node SizeOfKeywordKind = Node.Atom(SyntaxKind.SizeOfKeyword);
        internal static readonly Node NullKeywordKind = Node.Atom(SyntaxKind.NullKeyword);
        internal static readonly Node TrueKeywordKind = Node.Atom(SyntaxKind.TrueKeyword);
        internal static readonly Node FalseKeywordKind = Node.Atom(SyntaxKind.FalseKeyword);
        internal static readonly Node IfKeywordKind = Node.Atom(SyntaxKind.IfKeyword);
        internal static readonly Node ElseKeywordKind = Node.Atom(SyntaxKind.ElseKeyword);
        internal static readonly Node WhileKeywordKind = Node.Atom(SyntaxKind.WhileKeyword);
        internal static readonly Node ForKeywordKind = Node.Atom(SyntaxKind.ForKeyword);
        internal static readonly Node ForEachKeywordKind = Node.Atom(SyntaxKind.ForEachKeyword);
        internal static readonly Node DoKeywordKind = Node.Atom(SyntaxKind.DoKeyword);
        internal static readonly Node SwitchKeywordKind = Node.Atom(SyntaxKind.SwitchKeyword);
        internal static readonly Node CaseKeywordKind = Node.Atom(SyntaxKind.CaseKeyword);
        internal static readonly Node DefaultKeywordKind = Node.Atom(SyntaxKind.DefaultKeyword);
        internal static readonly Node TryKeywordKind = Node.Atom(SyntaxKind.TryKeyword);
        internal static readonly Node CatchKeywordKind = Node.Atom(SyntaxKind.CatchKeyword);
        internal static readonly Node FinallyKeywordKind = Node.Atom(SyntaxKind.FinallyKeyword);
        internal static readonly Node LockKeywordKind = Node.Atom(SyntaxKind.LockKeyword);
        internal static readonly Node GotoKeywordKind = Node.Atom(SyntaxKind.GotoKeyword);
        internal static readonly Node BreakKeywordKind = Node.Atom(SyntaxKind.BreakKeyword);
        internal static readonly Node ContinueKeywordKind = Node.Atom(SyntaxKind.ContinueKeyword);
        internal static readonly Node ReturnKeywordKind = Node.Atom(SyntaxKind.ReturnKeyword);
        internal static readonly Node ThrowKeywordKind = Node.Atom(SyntaxKind.ThrowKeyword);
        internal static readonly Node PublicKeywordKind = Node.Atom(SyntaxKind.PublicKeyword);
        internal static readonly Node PrivateKeywordKind = Node.Atom(SyntaxKind.PrivateKeyword);
        internal static readonly Node InternalKeywordKind = Node.Atom(SyntaxKind.InternalKeyword);
        internal static readonly Node ProtectedKeywordKind = Node.Atom(SyntaxKind.ProtectedKeyword);
        internal static readonly Node StaticKeywordKind = Node.Atom(SyntaxKind.StaticKeyword);
        internal static readonly Node ReadOnlyKeywordKind = Node.Atom(SyntaxKind.ReadOnlyKeyword);
        internal static readonly Node SealedKeywordKind = Node.Atom(SyntaxKind.SealedKeyword);
        internal static readonly Node ConstKeywordKind = Node.Atom(SyntaxKind.ConstKeyword);
        internal static readonly Node FixedKeywordKind = Node.Atom(SyntaxKind.FixedKeyword);
        internal static readonly Node StackAllocKeywordKind = Node.Atom(SyntaxKind.StackAllocKeyword);
        internal static readonly Node VolatileKeywordKind = Node.Atom(SyntaxKind.VolatileKeyword);
        internal static readonly Node NewKeywordKind = Node.Atom(SyntaxKind.NewKeyword);
        internal static readonly Node OverrideKeywordKind = Node.Atom(SyntaxKind.OverrideKeyword);
        internal static readonly Node AbstractKeywordKind = Node.Atom(SyntaxKind.AbstractKeyword);
        internal static readonly Node VirtualKeywordKind = Node.Atom(SyntaxKind.VirtualKeyword);
        internal static readonly Node EventKeywordKind = Node.Atom(SyntaxKind.EventKeyword);
        internal static readonly Node ExternKeywordKind = Node.Atom(SyntaxKind.ExternKeyword);
        internal static readonly Node RefKeywordKind = Node.Atom(SyntaxKind.RefKeyword);
        internal static readonly Node OutKeywordKind = Node.Atom(SyntaxKind.OutKeyword);
        internal static readonly Node InKeywordKind = Node.Atom(SyntaxKind.InKeyword);
        internal static readonly Node IsKeywordKind = Node.Atom(SyntaxKind.IsKeyword);
        internal static readonly Node AsKeywordKind = Node.Atom(SyntaxKind.AsKeyword);
        internal static readonly Node ParamsKeywordKind = Node.Atom(SyntaxKind.ParamsKeyword);
        internal static readonly Node ArgListKeywordKind = Node.Atom(SyntaxKind.ArgListKeyword);
        internal static readonly Node MakeRefKeywordKind = Node.Atom(SyntaxKind.MakeRefKeyword);
        internal static readonly Node RefTypeKeywordKind = Node.Atom(SyntaxKind.RefTypeKeyword);
        internal static readonly Node RefValueKeywordKind = Node.Atom(SyntaxKind.RefValueKeyword);
        internal static readonly Node ThisKeywordKind = Node.Atom(SyntaxKind.ThisKeyword);
        internal static readonly Node BaseKeywordKind = Node.Atom(SyntaxKind.BaseKeyword);
        internal static readonly Node NamespaceKeywordKind = Node.Atom(SyntaxKind.NamespaceKeyword);
        internal static readonly Node UsingKeywordKind = Node.Atom(SyntaxKind.UsingKeyword);
        internal static readonly Node ClassKeywordKind = Node.Atom(SyntaxKind.ClassKeyword);
        internal static readonly Node StructKeywordKind = Node.Atom(SyntaxKind.StructKeyword);
        internal static readonly Node InterfaceKeywordKind = Node.Atom(SyntaxKind.InterfaceKeyword);
        internal static readonly Node EnumKeywordKind = Node.Atom(SyntaxKind.EnumKeyword);
        internal static readonly Node DelegateKeywordKind = Node.Atom(SyntaxKind.DelegateKeyword);
        internal static readonly Node CheckedKeywordKind = Node.Atom(SyntaxKind.CheckedKeyword);
        internal static readonly Node UncheckedKeywordKind = Node.Atom(SyntaxKind.UncheckedKeyword);
        internal static readonly Node UnsafeKeywordKind = Node.Atom(SyntaxKind.UnsafeKeyword);
        internal static readonly Node OperatorKeywordKind = Node.Atom(SyntaxKind.OperatorKeyword);
        internal static readonly Node ExplicitKeywordKind = Node.Atom(SyntaxKind.ExplicitKeyword);
        internal static readonly Node ImplicitKeywordKind = Node.Atom(SyntaxKind.ImplicitKeyword);
        //
        //contextual keyword kind
        //
        internal static readonly Node YieldKeywordKind = Node.Atom(SyntaxKind.YieldKeyword);
        internal static readonly Node PartialKeywordKind = Node.Atom(SyntaxKind.PartialKeyword);
        internal static readonly Node AliasKeywordKind = Node.Atom(SyntaxKind.AliasKeyword);
        internal static readonly Node GlobalKeywordKind = Node.Atom(SyntaxKind.GlobalKeyword);
        internal static readonly Node AssemblyKeywordKind = Node.Atom(SyntaxKind.AssemblyKeyword);
        internal static readonly Node ModuleKeywordKind = Node.Atom(SyntaxKind.ModuleKeyword);
        internal static readonly Node TypeKeywordKind = Node.Atom(SyntaxKind.TypeKeyword);
        internal static readonly Node FieldKeywordKind = Node.Atom(SyntaxKind.FieldKeyword);
        internal static readonly Node MethodKeywordKind = Node.Atom(SyntaxKind.MethodKeyword);
        internal static readonly Node ParamKeywordKind = Node.Atom(SyntaxKind.ParamKeyword);
        internal static readonly Node PropertyKeywordKind = Node.Atom(SyntaxKind.PropertyKeyword);
        internal static readonly Node TypeVarKeywordKind = Node.Atom(SyntaxKind.TypeVarKeyword);
        internal static readonly Node GetKeywordKind = Node.Atom(SyntaxKind.GetKeyword);
        internal static readonly Node SetKeywordKind = Node.Atom(SyntaxKind.SetKeyword);
        internal static readonly Node AddKeywordKind = Node.Atom(SyntaxKind.AddKeyword);
        internal static readonly Node RemoveKeywordKind = Node.Atom(SyntaxKind.RemoveKeyword);
        internal static readonly Node WhereKeywordKind = Node.Atom(SyntaxKind.WhereKeyword);
        internal static readonly Node FromKeywordKind = Node.Atom(SyntaxKind.FromKeyword);
        internal static readonly Node GroupKeywordKind = Node.Atom(SyntaxKind.GroupKeyword);
        internal static readonly Node JoinKeywordKind = Node.Atom(SyntaxKind.JoinKeyword);
        internal static readonly Node IntoKeywordKind = Node.Atom(SyntaxKind.IntoKeyword);
        internal static readonly Node LetKeywordKind = Node.Atom(SyntaxKind.LetKeyword);
        internal static readonly Node ByKeywordKind = Node.Atom(SyntaxKind.ByKeyword);
        internal static readonly Node SelectKeywordKind = Node.Atom(SyntaxKind.SelectKeyword);
        internal static readonly Node OrderByKeywordKind = Node.Atom(SyntaxKind.OrderByKeyword);
        internal static readonly Node OnKeywordKind = Node.Atom(SyntaxKind.OnKeyword);
        internal static readonly Node EqualsKeywordKind = Node.Atom(SyntaxKind.EqualsKeyword);
        internal static readonly Node AscendingKeywordKind = Node.Atom(SyntaxKind.AscendingKeyword);
        internal static readonly Node DescendingKeywordKind = Node.Atom(SyntaxKind.DescendingKeyword);
        internal static readonly Node AsyncKeywordKind = Node.Atom(SyntaxKind.AsyncKeyword);
        internal static readonly Node AwaitKeywordKind = Node.Atom(SyntaxKind.AwaitKeyword);
        //
        //
        //
        internal static readonly Node IdentifierTokenKind = Node.Atom(SyntaxKind.IdentifierToken);
        internal static readonly Node NumericLiteralTokenKind = Node.Atom(SyntaxKind.NumericLiteralToken);
        internal static readonly Node CharacterLiteralTokenKind = Node.Atom(SyntaxKind.CharacterLiteralToken);
        internal static readonly Node StringLiteralTokenKind = Node.Atom(SyntaxKind.StringLiteralToken);
        //
        //
        //
        internal static readonly Node ThisConstructorInitializerKind = Node.Atom(SyntaxKind.ThisConstructorInitializer);
        internal static readonly Node BaseConstructorInitializerKind = Node.Atom(SyntaxKind.BaseConstructorInitializer);
        internal static readonly Node GetAccessorDeclarationKind = Node.Atom(SyntaxKind.GetAccessorDeclaration);
        internal static readonly Node SetAccessorDeclarationKind = Node.Atom(SyntaxKind.SetAccessorDeclaration);
        internal static readonly Node AddAccessorDeclarationKind = Node.Atom(SyntaxKind.AddAccessorDeclaration);
        internal static readonly Node RemoveAccessorDeclarationKind = Node.Atom(SyntaxKind.RemoveAccessorDeclaration);
        internal static readonly Node ClassConstraintKind = Node.Atom(SyntaxKind.ClassConstraint);
        internal static readonly Node StructConstraintKind = Node.Atom(SyntaxKind.StructConstraint);
        internal static readonly Node CaseSwitchLabelKind = Node.Atom(SyntaxKind.CaseSwitchLabel);
        internal static readonly Node DefaultSwitchLabelKind = Node.Atom(SyntaxKind.DefaultSwitchLabel);
        internal static readonly Node GotoCaseStatementKind = Node.Atom(SyntaxKind.GotoCaseStatement);
        internal static readonly Node GotoDefaultStatementKind = Node.Atom(SyntaxKind.GotoDefaultStatement);
        internal static readonly Node GotoStatementKind = Node.Atom(SyntaxKind.GotoStatement);
        internal static readonly Node CheckedStatementKind = Node.Atom(SyntaxKind.CheckedStatement);
        internal static readonly Node UncheckedStatementKind = Node.Atom(SyntaxKind.UncheckedStatement);
        internal static readonly Node YieldReturnStatementKind = Node.Atom(SyntaxKind.YieldReturnStatement);
        internal static readonly Node YieldBreakStatementKind = Node.Atom(SyntaxKind.YieldBreakStatement);
        internal static readonly Node SimpleAssignmentExpressionKind = Node.Atom(SyntaxKind.SimpleAssignmentExpression);
        internal static readonly Node AddAssignmentExpressionKind = Node.Atom(SyntaxKind.AddAssignmentExpression);
        internal static readonly Node SubtractAssignmentExpressionKind = Node.Atom(SyntaxKind.SubtractAssignmentExpression);
        internal static readonly Node MultiplyAssignmentExpressionKind = Node.Atom(SyntaxKind.MultiplyAssignmentExpression);
        internal static readonly Node DivideAssignmentExpressionKind = Node.Atom(SyntaxKind.DivideAssignmentExpression);
        internal static readonly Node ModuloAssignmentExpressionKind = Node.Atom(SyntaxKind.ModuloAssignmentExpression);
        internal static readonly Node AndAssignmentExpressionKind = Node.Atom(SyntaxKind.AndAssignmentExpression);
        internal static readonly Node ExclusiveOrAssignmentExpressionKind = Node.Atom(SyntaxKind.ExclusiveOrAssignmentExpression);
        internal static readonly Node OrAssignmentExpressionKind = Node.Atom(SyntaxKind.OrAssignmentExpression);
        internal static readonly Node LeftShiftAssignmentExpressionKind = Node.Atom(SyntaxKind.LeftShiftAssignmentExpression);
        internal static readonly Node RightShiftAssignmentExpressionKind = Node.Atom(SyntaxKind.RightShiftAssignmentExpression);
        internal static readonly Node CoalesceExpressionKind = Node.Atom(SyntaxKind.CoalesceExpression);
        internal static readonly Node LogicalOrExpressionKind = Node.Atom(SyntaxKind.LogicalOrExpression);
        internal static readonly Node LogicalAndExpressionKind = Node.Atom(SyntaxKind.LogicalAndExpression);
        internal static readonly Node BitwiseOrExpressionKind = Node.Atom(SyntaxKind.BitwiseOrExpression);
        internal static readonly Node ExclusiveOrExpressionKind = Node.Atom(SyntaxKind.ExclusiveOrExpression);
        internal static readonly Node BitwiseAndExpressionKind = Node.Atom(SyntaxKind.BitwiseAndExpression);
        internal static readonly Node EqualsExpressionKind = Node.Atom(SyntaxKind.EqualsExpression);
        internal static readonly Node NotEqualsExpressionKind = Node.Atom(SyntaxKind.NotEqualsExpression);
        internal static readonly Node LessThanExpressionKind = Node.Atom(SyntaxKind.LessThanExpression);
        internal static readonly Node LessThanOrEqualExpressionKind = Node.Atom(SyntaxKind.LessThanOrEqualExpression);
        internal static readonly Node GreaterThanExpressionKind = Node.Atom(SyntaxKind.GreaterThanExpression);
        internal static readonly Node GreaterThanOrEqualExpressionKind = Node.Atom(SyntaxKind.GreaterThanOrEqualExpression);
        internal static readonly Node IsExpressionKind = Node.Atom(SyntaxKind.IsExpression);
        internal static readonly Node AsExpressionKind = Node.Atom(SyntaxKind.AsExpression);
        internal static readonly Node LeftShiftExpressionKind = Node.Atom(SyntaxKind.LeftShiftExpression);
        internal static readonly Node RightShiftExpressionKind = Node.Atom(SyntaxKind.RightShiftExpression);
        internal static readonly Node AddExpressionKind = Node.Atom(SyntaxKind.AddExpression);
        internal static readonly Node SubtractExpressionKind = Node.Atom(SyntaxKind.SubtractExpression);
        internal static readonly Node MultiplyExpressionKind = Node.Atom(SyntaxKind.MultiplyExpression);
        internal static readonly Node DivideExpressionKind = Node.Atom(SyntaxKind.DivideExpression);
        internal static readonly Node ModuloExpressionKind = Node.Atom(SyntaxKind.ModuloExpression);
        internal static readonly Node UnaryPlusExpressionKind = Node.Atom(SyntaxKind.UnaryPlusExpression);
        internal static readonly Node UnaryMinusExpressionKind = Node.Atom(SyntaxKind.UnaryMinusExpression);
        internal static readonly Node LogicalNotExpressionKind = Node.Atom(SyntaxKind.LogicalNotExpression);
        internal static readonly Node BitwiseNotExpressionKind = Node.Atom(SyntaxKind.BitwiseNotExpression);
        internal static readonly Node AddressOfExpressionKind = Node.Atom(SyntaxKind.AddressOfExpression);
        internal static readonly Node PointerIndirectionExpressionKind = Node.Atom(SyntaxKind.PointerIndirectionExpression);
        internal static readonly Node PreIncrementExpressionKind = Node.Atom(SyntaxKind.PreIncrementExpression);
        internal static readonly Node PreDecrementExpressionKind = Node.Atom(SyntaxKind.PreDecrementExpression);
        internal static readonly Node AwaitExpressionKind = Node.Atom(SyntaxKind.AwaitExpression);
        internal static readonly Node PostIncrementExpressionKind = Node.Atom(SyntaxKind.PostIncrementExpression);
        internal static readonly Node PostDecrementExpressionKind = Node.Atom(SyntaxKind.PostDecrementExpression);
        internal static readonly Node SimpleMemberAccessExpressionKind = Node.Atom(SyntaxKind.SimpleMemberAccessExpression);
        internal static readonly Node PointerMemberAccessExpressionKind = Node.Atom(SyntaxKind.PointerMemberAccessExpression);
        internal static readonly Node TrueLiteralExpressionKind = Node.Atom(SyntaxKind.TrueLiteralExpression);
        internal static readonly Node FalseLiteralExpressionKind = Node.Atom(SyntaxKind.FalseLiteralExpression);
        internal static readonly Node NullLiteralExpressionKind = Node.Atom(SyntaxKind.NullLiteralExpression);
        internal static readonly Node NumericLiteralExpressionKind = Node.Atom(SyntaxKind.NumericLiteralExpression);
        internal static readonly Node CharacterLiteralExpressionKind = Node.Atom(SyntaxKind.CharacterLiteralExpression);
        internal static readonly Node StringLiteralExpressionKind = Node.Atom(SyntaxKind.StringLiteralExpression);
        internal static readonly Node CheckedExpressionKind = Node.Atom(SyntaxKind.CheckedExpression);
        internal static readonly Node UncheckedExpressionKind = Node.Atom(SyntaxKind.UncheckedExpression);
        internal static readonly Node ObjectInitializerExpressionKind = Node.Atom(SyntaxKind.ObjectInitializerExpression);
        internal static readonly Node CollectionInitializerExpressionKind = Node.Atom(SyntaxKind.CollectionInitializerExpression);
        internal static readonly Node ComplexElementInitializerExpressionKind = Node.Atom(SyntaxKind.ComplexElementInitializerExpression);
        internal static readonly Node ArrayInitializerExpressionKind = Node.Atom(SyntaxKind.ArrayInitializerExpression);
        internal static readonly Node AscendingOrderingKind = Node.Atom(SyntaxKind.AscendingOrdering);
        internal static readonly Node DescendingOrderingKind = Node.Atom(SyntaxKind.DescendingOrdering);

    }
    public enum XTokenKind {
        //
        //reserved keywords and tokens
        //
        AliasKeyword = 20000,
        AttributeKeyword,
        AttributesKeyword,
        ChoiceKeyword,
        ElementKeyword,
        ImportKeyword,
        SeqKeyword,
        TypeKeyword,
        UnorderedKeyword,
        XNamespaceKeyword,
        //
        DotDotToken,
        HashHashToken,
        AsteriskAsteriskToken,
        AtToken,
        //
        //contextual keywords
        //
        AllKeyword = 21000,
        AnyKeyword,
        AttributeRefKeyword,
        AttributesRefKeyword,
        ChildrenKeyword,
        ChildStructRefKeyword,
        CollapseKeyword,
        DerivationProhibitionKeyword,
        DigitsKeyword,
        ElementRefKeyword,
        EnumsKeyword,
        ExtendKeyword,
        FacetsKeyword,
        InstanceProhibitionKeyword,
        KeyKeyword,
        KeyRefKeyword,
        LengthRangeKeyword,
        ListKeyword,
        MemberKeyword,
        MemberNameKeyword,
        MixedKeyword,
        MustValidateKeyword,
        NoneKeyword,
        NullableKeyword,
        OtherKeyword,
        PatternsKeyword,
        PreserveKeyword,
        QualifiedKeyword,
        ReplaceKeyword,
        RestrictKeyword,
        SkipValidateKeyword,
        SplitListValueKeyword,
        SubstituteKeyword,
        TryValidateKeyword,
        UniqueKeyword,
        UniteKeyword,
        UnqualifiedKeyword,
        ValueRangeKeyword,
        WhitespaceKeyword,
        WildcardKeyword,
    }
    public static class XTokens {
        private volatile static HashSet<string> _keywordSet;
        public static HashSet<string> KeywordSet {
            get { return _keywordSet ?? (_keywordSet = new HashSet<string>(Enum.GetValues(typeof(XTokenKind)).Cast<XTokenKind>().Select(i => GetText(i)))); }
        }
        public static string GetText(this XTokenKind kind) {
            switch (kind) {
                case XTokenKind.AliasKeyword: return "alias";
                case XTokenKind.AttributeKeyword: return "attribute";
                case XTokenKind.AttributesKeyword: return "attributes";
                case XTokenKind.ChoiceKeyword: return "choice";
                case XTokenKind.ElementKeyword: return "element";
                case XTokenKind.ImportKeyword: return "import";
                case XTokenKind.SeqKeyword: return "seq";
                case XTokenKind.TypeKeyword: return "type";
                case XTokenKind.UnorderedKeyword: return "unordered";
                case XTokenKind.XNamespaceKeyword: return "xnamespace";
                case XTokenKind.DotDotToken: return "..";
                case XTokenKind.HashHashToken: return "##";
                case XTokenKind.AsteriskAsteriskToken: return @"**";
                case XTokenKind.AtToken: return "@";
                //
                //
                case XTokenKind.AllKeyword: return "all";
                case XTokenKind.AnyKeyword: return "any";
                case XTokenKind.AttributeRefKeyword: return "attributeref";
                case XTokenKind.AttributesRefKeyword: return "attributesref";
                case XTokenKind.ChildrenKeyword: return "children";
                case XTokenKind.ChildStructRefKeyword: return "childstructref";
                case XTokenKind.CollapseKeyword: return "collapse";
                case XTokenKind.DerivationProhibitionKeyword: return "derivationprohibition";
                case XTokenKind.DigitsKeyword: return "digits";
                case XTokenKind.ElementRefKeyword: return "elementref";
                case XTokenKind.EnumsKeyword: return "enums";
                case XTokenKind.ExtendKeyword: return "extend";
                case XTokenKind.FacetsKeyword: return "facets";
                case XTokenKind.InstanceProhibitionKeyword: return "instanceprohibition";
                case XTokenKind.LengthRangeKeyword: return "lengthrange";
                case XTokenKind.ListKeyword: return "list";
                case XTokenKind.KeyKeyword: return "key";
                case XTokenKind.KeyRefKeyword: return "keyref";
                case XTokenKind.MemberKeyword: return "member";
                case XTokenKind.MemberNameKeyword: return "membername";
                case XTokenKind.MixedKeyword: return "mixed";
                case XTokenKind.MustValidateKeyword: return "mustvalidate";
                case XTokenKind.NoneKeyword: return "none";
                case XTokenKind.NullableKeyword: return "nullable";
                case XTokenKind.OtherKeyword: return "other";
                case XTokenKind.PatternsKeyword: return "patterns";
                case XTokenKind.PreserveKeyword: return "preserve";
                case XTokenKind.QualifiedKeyword: return "qualified";
                case XTokenKind.ReplaceKeyword: return "replace";
                case XTokenKind.RestrictKeyword: return "restrict";
                case XTokenKind.SkipValidateKeyword: return "skipvalidate";
                case XTokenKind.SplitListValueKeyword: return "splitlistvalue";
                case XTokenKind.SubstituteKeyword: return "substitute";
                case XTokenKind.TryValidateKeyword: return "tryvalidate";
                case XTokenKind.UniqueKeyword: return "unique";
                case XTokenKind.UniteKeyword: return "unite";
                case XTokenKind.UnqualifiedKeyword: return "unqualified";
                case XTokenKind.ValueRangeKeyword: return "valuerange";
                case XTokenKind.WhitespaceKeyword: return "whitespace";
                case XTokenKind.WildcardKeyword: return "wildcard";

                default: throw new ArgumentException("Invalid X token kind: " + kind);
            }
        }
        //
        internal static readonly Node AliasKeywordKind = Node.Atom(XTokenKind.AliasKeyword);
        internal static readonly Node AttributeKeywordKind = Node.Atom(XTokenKind.AttributeKeyword);
        internal static readonly Node AttributesKeywordKind = Node.Atom(XTokenKind.AttributesKeyword);
        internal static readonly Node ChoiceKeywordKind = Node.Atom(XTokenKind.ChoiceKeyword);
        internal static readonly Node ElementKeywordKind = Node.Atom(XTokenKind.ElementKeyword);
        internal static readonly Node ImportKeywordKind = Node.Atom(XTokenKind.ImportKeyword);
        internal static readonly Node SeqKeywordKind = Node.Atom(XTokenKind.SeqKeyword);
        internal static readonly Node TypeKeywordKind = Node.Atom(XTokenKind.TypeKeyword);
        internal static readonly Node UnorderedKeywordKind = Node.Atom(XTokenKind.UnorderedKeyword);
        internal static readonly Node XNamespaceKeywordKind = Node.Atom(XTokenKind.XNamespaceKeyword);
        internal static readonly Node DotDotTokenKind = Node.Atom(XTokenKind.DotDotToken);
        internal static readonly Node HashHashTokenKind = Node.Atom(XTokenKind.HashHashToken);
        internal static readonly Node AsteriskAsteriskTokenKind = Node.Atom(XTokenKind.AsteriskAsteriskToken);
        internal static readonly Node AtTokenKind = Node.Atom(XTokenKind.AtToken);
        internal static readonly Node AllKeywordKind = Node.Atom(XTokenKind.AllKeyword);
        internal static readonly Node AnyKeywordKind = Node.Atom(XTokenKind.AnyKeyword);
        internal static readonly Node AttributeRefKeywordKind = Node.Atom(XTokenKind.AttributeRefKeyword);
        internal static readonly Node AttributesRefKeywordKind = Node.Atom(XTokenKind.AttributesRefKeyword);
        internal static readonly Node ChildrenKeywordKind = Node.Atom(XTokenKind.ChildrenKeyword);
        internal static readonly Node ChildStructRefKeywordKind = Node.Atom(XTokenKind.ChildStructRefKeyword);
        internal static readonly Node CollapseKeywordKind = Node.Atom(XTokenKind.CollapseKeyword);
        internal static readonly Node DerivationProhibitionKeywordKind = Node.Atom(XTokenKind.DerivationProhibitionKeyword);
        internal static readonly Node DigitsKeywordKind = Node.Atom(XTokenKind.DigitsKeyword);
        internal static readonly Node ElementRefKeywordKind = Node.Atom(XTokenKind.ElementRefKeyword);
        internal static readonly Node EnumsKeywordKind = Node.Atom(XTokenKind.EnumsKeyword);
        internal static readonly Node ExtendKeywordKind = Node.Atom(XTokenKind.ExtendKeyword);
        internal static readonly Node FacetsKeywordKind = Node.Atom(XTokenKind.FacetsKeyword);
        internal static readonly Node InstanceProhibitionKeywordKind = Node.Atom(XTokenKind.InstanceProhibitionKeyword);
        internal static readonly Node KeyKeywordKind = Node.Atom(XTokenKind.KeyKeyword);
        internal static readonly Node KeyRefKeywordKind = Node.Atom(XTokenKind.KeyRefKeyword);
        internal static readonly Node LengthRangeKeywordKind = Node.Atom(XTokenKind.LengthRangeKeyword);
        internal static readonly Node ListKeywordKind = Node.Atom(XTokenKind.ListKeyword);
        internal static readonly Node MemberKeywordKind = Node.Atom(XTokenKind.MemberKeyword);
        internal static readonly Node MemberNameKeywordKind = Node.Atom(XTokenKind.MemberNameKeyword);
        internal static readonly Node MixedKeywordKind = Node.Atom(XTokenKind.MixedKeyword);
        internal static readonly Node MustValidateKeywordKind = Node.Atom(XTokenKind.MustValidateKeyword);
        internal static readonly Node NoneKeywordKind = Node.Atom(XTokenKind.NoneKeyword);
        internal static readonly Node NullableKeywordKind = Node.Atom(XTokenKind.NullableKeyword);
        internal static readonly Node OtherKeywordKind = Node.Atom(XTokenKind.OtherKeyword);
        internal static readonly Node PatternsKeywordKind = Node.Atom(XTokenKind.PatternsKeyword);
        internal static readonly Node PreserveKeywordKind = Node.Atom(XTokenKind.PreserveKeyword);
        internal static readonly Node QualifiedKeywordKind = Node.Atom(XTokenKind.QualifiedKeyword);
        internal static readonly Node ReplaceKeywordKind = Node.Atom(XTokenKind.ReplaceKeyword);
        internal static readonly Node RestrictKeywordKind = Node.Atom(XTokenKind.RestrictKeyword);
        internal static readonly Node SkipValidateKeywordKind = Node.Atom(XTokenKind.SkipValidateKeyword);
        internal static readonly Node SplitListValueKeywordKind = Node.Atom(XTokenKind.SplitListValueKeyword);
        internal static readonly Node SubstituteKeywordKind = Node.Atom(XTokenKind.SubstituteKeyword);
        internal static readonly Node TryValidateKeywordKind = Node.Atom(XTokenKind.TryValidateKeyword);
        internal static readonly Node UniqueKeywordKind = Node.Atom(XTokenKind.UniqueKeyword);
        internal static readonly Node UniteKeywordKind = Node.Atom(XTokenKind.UniteKeyword);
        internal static readonly Node UnqualifiedKeywordKind = Node.Atom(XTokenKind.UnqualifiedKeyword);
        internal static readonly Node ValueRangeKeywordKind = Node.Atom(XTokenKind.ValueRangeKeyword);
        internal static readonly Node WhitespaceKeywordKind = Node.Atom(XTokenKind.WhitespaceKeyword);
        internal static readonly Node WildcardKeywordKind = Node.Atom(XTokenKind.WildcardKeyword);
    }
    public enum WTokenKind {
        //
        //reserved keywords and tokens
        //
        ActivityKeyword = 30000,
        CancellableKeyword,
        ConfirmKeyword,
        CompensableKeyword,
        CompensateKeyword,
        ContentCorrKeyword,
        DelayKeyword,
        FlowKeyword,
        FIfKeyword,
        FSwitchKeyword,
        ImportKeyword,
        NoPersistKeyword,
        ParallelKeyword,
        PersistKeyword,
        PForEachKeyword,
        PickKeyword,
        ReceiveKeyword,
        ReceiveReplyKeyword,
        SendKeyword,
        SendReplyKeyword,
        StateMachineKeyword,
        TerminateKeyword,
        TransactedKeyword,
        TransactedReceiveKeyword,
        //
        HashHashToken,
        TildeGreaterThanToken,
        LessThanTildeToken,
        //
        //contextual keywords
        //
        CallbackCorrKeyword = 31000,
        CancelKeyword,
        ContextCorrKeyword,
        EndpointAddressKeyword,
        InitKeyword,
        RequestCorrKeyword,
        TimeoutKeyword,
        UntilKeyword,
    }
    public static class WTokens {
        private volatile static HashSet<string> _keywordSet;
        public static HashSet<string> KeywordSet {
            get { return _keywordSet ?? (_keywordSet = new HashSet<string>(Enum.GetValues(typeof(WTokenKind)).Cast<WTokenKind>().Select(i => GetText(i)))); }
        }
        public static string GetText(this WTokenKind kind) {
            switch (kind) {
                case WTokenKind.ActivityKeyword: return "activity";
                case WTokenKind.CancellableKeyword: return "cancellable";
                case WTokenKind.ConfirmKeyword: return "confirm";
                case WTokenKind.CompensableKeyword: return "compensable";
                case WTokenKind.CompensateKeyword: return "compensate";
                case WTokenKind.ContentCorrKeyword: return "contentcorr";
                case WTokenKind.DelayKeyword: return "delay";
                case WTokenKind.FlowKeyword: return "flow";
                case WTokenKind.FIfKeyword: return "fif";
                case WTokenKind.FSwitchKeyword: return "fswitch";
                case WTokenKind.ImportKeyword: return "import";
                case WTokenKind.NoPersistKeyword: return "nopersist";
                case WTokenKind.ParallelKeyword: return "parallel";
                case WTokenKind.PersistKeyword: return "persist";
                case WTokenKind.PForEachKeyword: return "pforeach";
                case WTokenKind.PickKeyword: return "pick";
                case WTokenKind.ReceiveKeyword: return "receive";
                case WTokenKind.ReceiveReplyKeyword: return "receivereply";
                case WTokenKind.SendKeyword: return "send";
                case WTokenKind.SendReplyKeyword: return "sendreply";
                case WTokenKind.StateMachineKeyword: return "statemachine";
                case WTokenKind.TerminateKeyword: return "terminate";
                case WTokenKind.TransactedKeyword: return "transacted";
                case WTokenKind.TransactedReceiveKeyword: return "transactedreceive";
                //
                case WTokenKind.HashHashToken: return "##";
                case WTokenKind.TildeGreaterThanToken: return "~>";
                case WTokenKind.LessThanTildeToken: return "<~";
                //
                case WTokenKind.CallbackCorrKeyword: return "callbackcorr";
                case WTokenKind.CancelKeyword: return "cancel";
                case WTokenKind.ContextCorrKeyword: return "contextcorr";
                case WTokenKind.EndpointAddressKeyword: return "endpointaddress";
                case WTokenKind.InitKeyword: return "init";
                case WTokenKind.RequestCorrKeyword: return "requestcorr";
                case WTokenKind.TimeoutKeyword: return "timeout";
                case WTokenKind.UntilKeyword: return "until";
                default: throw new ArgumentException("Invalid W token kind: " + kind);
            }
        }
        //
        internal static readonly Node ActivityKeywordKind = Node.Atom(WTokenKind.ActivityKeyword);
        internal static readonly Node CancellableKeywordKind = Node.Atom(WTokenKind.CancellableKeyword);
        internal static readonly Node ConfirmKeywordKind = Node.Atom(WTokenKind.ConfirmKeyword);
        internal static readonly Node CompensableKeywordKind = Node.Atom(WTokenKind.CompensableKeyword);
        internal static readonly Node CompensateKeywordKind = Node.Atom(WTokenKind.CompensateKeyword);
        internal static readonly Node ContentCorrKeywordKind = Node.Atom(WTokenKind.ContentCorrKeyword);
        internal static readonly Node DelayKeywordKind = Node.Atom(WTokenKind.DelayKeyword);
        internal static readonly Node FlowKeywordKind = Node.Atom(WTokenKind.FlowKeyword);
        internal static readonly Node FIfKeywordKind = Node.Atom(WTokenKind.FIfKeyword);
        internal static readonly Node FSwitchKeywordKind = Node.Atom(WTokenKind.FSwitchKeyword);
        internal static readonly Node ImportKeywordKind = Node.Atom(WTokenKind.ImportKeyword);
        internal static readonly Node NoPersistKeywordKind = Node.Atom(WTokenKind.NoPersistKeyword);
        internal static readonly Node ParallelKeywordKind = Node.Atom(WTokenKind.ParallelKeyword);
        internal static readonly Node PersistKeywordKind = Node.Atom(WTokenKind.PersistKeyword);
        internal static readonly Node PForEachKeywordKind = Node.Atom(WTokenKind.PForEachKeyword);
        internal static readonly Node PickKeywordKind = Node.Atom(WTokenKind.PickKeyword);
        internal static readonly Node ReceiveKeywordKind = Node.Atom(WTokenKind.ReceiveKeyword);
        internal static readonly Node ReceiveReplyKeywordKind = Node.Atom(WTokenKind.ReceiveReplyKeyword);
        internal static readonly Node SendKeywordKind = Node.Atom(WTokenKind.SendKeyword);
        internal static readonly Node SendReplyKeywordKind = Node.Atom(WTokenKind.SendReplyKeyword);
        internal static readonly Node StateMachineKeywordKind = Node.Atom(WTokenKind.StateMachineKeyword);
        internal static readonly Node TerminateKeywordKind = Node.Atom(WTokenKind.TerminateKeyword);
        internal static readonly Node TransactedKeywordKind = Node.Atom(WTokenKind.TransactedKeyword);
        internal static readonly Node TransactedReceiveKeywordKind = Node.Atom(WTokenKind.TransactedReceiveKeyword);
        internal static readonly Node HashHashTokenKind = Node.Atom(WTokenKind.HashHashToken);
        internal static readonly Node TildeGreaterThanTokenKind = Node.Atom(WTokenKind.TildeGreaterThanToken);
        internal static readonly Node LessThanTildeTokenKind = Node.Atom(WTokenKind.LessThanTildeToken);
        internal static readonly Node CallbackCorrKeywordKind = Node.Atom(WTokenKind.CallbackCorrKeyword);
        internal static readonly Node CancelKeywordKind = Node.Atom(WTokenKind.CancelKeyword);
        internal static readonly Node ContextCorrKeywordKind = Node.Atom(WTokenKind.ContextCorrKeyword);
        internal static readonly Node EndpointAddressKeywordKind = Node.Atom(WTokenKind.EndpointAddressKeyword);
        internal static readonly Node InitKeywordKind = Node.Atom(WTokenKind.InitKeyword);
        internal static readonly Node RequestCorrKeywordKind = Node.Atom(WTokenKind.RequestCorrKeyword);
        internal static readonly Node TimeoutKeywordKind = Node.Atom(WTokenKind.TimeoutKeyword);
        internal static readonly Node UntilKeywordKind = Node.Atom(WTokenKind.UntilKeyword);
    }
    //
    //
    //
    [Serializable]
    public struct SourcePosition : IEquatable<SourcePosition> {
        public SourcePosition(int line, int character) {
            if (line < 1) throw new ArgumentOutOfRangeException("line");
            if (character < 1) throw new ArgumentOutOfRangeException("character");
            Line = line;
            Character = character;
        }
        internal static SourcePosition From(LinePosition lp) {
            return new SourcePosition(lp.Line + 1, lp.Character + 1);
        }
        public readonly int Line;//1-based
        public readonly int Character;//1-based
        public override string ToString() {
            return Line + "," + Character;
        }
        public bool Equals(SourcePosition other) {
            return other.Line == Line && other.Character == Character;
        }
        public override bool Equals(object obj) {
            return obj is SourcePosition && Equals((SourcePosition)obj);
        }
        public override int GetHashCode() {
            return Extensions.CombineHash(Line, Character);
        }
        public static bool operator ==(SourcePosition left, SourcePosition right) {
            return left.Equals(right);
        }
        public static bool operator !=(SourcePosition left, SourcePosition right) {
            return !(left == right);
        }
    }
    [Serializable]
    public sealed class SourceSpan : /*SyntaxAnnotation,*/ IEquatable<SourceSpan> {
        internal SourceSpan(string filePath, int startIndex, int length, SourcePosition startPosition, SourcePosition endPosition) {
            if (filePath == null) throw new ArgumentNullException("filePath");
            if (startIndex < 0) throw new ArgumentOutOfRangeException("startIndex");
            if (length < 0) throw new ArgumentOutOfRangeException("length");
            FilePath = filePath;
            StartIndex = startIndex;
            Length = length;
            StartPosition = startPosition;
            EndPosition = endPosition;
        }
        public readonly string FilePath;
        public readonly int StartIndex;//0-based character index
        public readonly int Length;
        public int EndIndex { get { return StartIndex + Length; } }
        public readonly SourcePosition StartPosition;//Line and Character are 1-based
        public readonly SourcePosition EndPosition;
        private string _displayString;
        public override string ToString() {
            return _displayString ?? (_displayString = FilePath + ": (" + StartPosition.ToString() + ")-(" + EndPosition.ToString() + ")");
        }
        //
        public bool Equals(SourceSpan other) {
            if (object.ReferenceEquals(this, other)) return true;
            if (object.ReferenceEquals(other, null)) return false;
            return other.FilePath == FilePath && other.StartIndex == StartIndex && other.Length == Length;
        }
        public override bool Equals(object obj) {
            return Equals(obj as SourceSpan);
        }
        public override int GetHashCode() {
            return Extensions.CombineHash(FilePath.GetHashCode(), StartIndex, Length);
        }
        public static bool operator ==(SourceSpan left, SourceSpan right) {
            if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
            return left.Equals(right);
        }
        public static bool operator !=(SourceSpan left, SourceSpan right) {
            return !(left == right);
        }
        //
        internal bool IsContiguousWith(SourceSpan follower) {
            if ((object)follower == null) throw new ArgumentNullException("follower");
            return EndIndex == follower.StartIndex;
        }
        internal SourceSpan MergeWith(SourceSpan other) {
            if ((object)other == null) throw new ArgumentNullException("other");
            if (other.FilePath != FilePath) throw new ArgumentException("other");
            if (this == other) return this;
            int startIndex = StartIndex, endIndex = EndIndex;
            SourcePosition startPosition = StartPosition, endPosition = EndPosition;
            if (other.StartIndex < StartIndex) {
                startIndex = other.StartIndex;
                startPosition = other.StartPosition;
            }
            if (other.EndIndex > EndIndex) {
                endIndex = other.EndIndex;
                endPosition = other.EndPosition;
            }
            return new SourceSpan(FilePath, startIndex, endIndex - startIndex, startPosition, endPosition);
        }
    }
    public interface ISourceSpanProvider {
        SourceSpan SourceSpan { get; }
    }
    public sealed class Node : ISourceSpanProvider {
        internal static readonly Node Null = new Node { _kind = NodeKind.Null };
        internal static Node Atom(object value, SourceSpan sourceSpan = null) {
            return new Node { _kind = NodeKind.Atom, _sourceSpan = sourceSpan, _value = value };
        }
        internal static Node Box(string label, SourceSpan sourceSpan, Node singleton) {
            return new Node { _kind = NodeKind.Box, _label = label, _sourceSpan = sourceSpan, _value = singleton ?? Null };
        }
        internal static Node Struct(string label, SourceSpan sourceSpan, params KeyValuePair<string, Node>[] members) {
            if (members == null) throw new ArgumentNullException("members");
            var memberDict = new Dictionary<string, Node>();
            foreach (var pair in members)
                memberDict.Add(pair.Key, pair.Value ?? Null);
            return new Node { _kind = NodeKind.Struct, _label = label, _sourceSpan = sourceSpan, _value = memberDict };
        }
        internal static Node List(SourceSpan sourceSpan, List<Node> itemList, string label = null) {
            if (itemList != null) {
                for (var i = 0; i < itemList.Count; i++)
                    if (itemList[i] == null)
                        itemList[i] = Null;
            }
            return new Node { _kind = NodeKind.List, _label = label, _sourceSpan = sourceSpan, _value = itemList ?? _emptyListValue };
        }
        internal Node AddListItem(Node item) {
            var itemList = Items.ToList();
            if (item == null) item = Null;
            itemList.Add(item);
            var sourceSpan = _sourceSpan;
            if (sourceSpan == null) sourceSpan = item._sourceSpan;
            else if (item._sourceSpan != null) sourceSpan = sourceSpan.MergeWith(item._sourceSpan);
            return List(sourceSpan, itemList, _label);
        }
        private Node() { }
        private static readonly IReadOnlyList<Node> _emptyListValue = new List<Node>();
        private enum NodeKind { Null = 0, Atom, Box, Struct, List }
        private NodeKind _kind;
        private string _label;
        private SourceSpan _sourceSpan;
        private object _value;
        //
        //
        public string Label { get { return _label; } }//opt
        public SourceSpan SourceSpan { get { return _sourceSpan; } }//opt
        public bool IsNull { get { return _kind == NodeKind.Null; } }
        public bool IsNotNull { get { return _kind != NodeKind.Null; } }
        //
        public bool IsAtom { get { return _kind == NodeKind.Atom; } }
        public object Value {
            get {
                if (!IsAtom) throw new InvalidOperationException("Node is not atom");
                return _value;
            }
        }
        //
        public bool IsBox { get { return _kind == NodeKind.Box; } }
        public Node Singleton {
            get {
                if (!IsBox) throw new InvalidOperationException("Node is not box");
                return (Node)_value;
            }
        }
        //
        public bool IsStruct { get { return _kind == NodeKind.Struct; } }
        private IReadOnlyDictionary<string, Node> GetDictionary() {
            if (!IsStruct) throw new InvalidOperationException("Node is not struct");
            return (IReadOnlyDictionary<string, Node>)_value;
        }
        public IReadOnlyDictionary<string, Node> Members { get { return GetDictionary(); } }
        public bool HasMember(string name) { return GetDictionary().ContainsKey(name); }
        public Node Member(string name) { return GetDictionary()[name]; }
        //
        public bool IsListOrNull { get { return _kind == NodeKind.List || _kind == NodeKind.Null; } }
        public bool IsList { get { return _kind == NodeKind.List; } }
        public IReadOnlyList<Node> Items {
            get {
                if (!IsListOrNull) throw new InvalidOperationException("Node is not list or null");
                return (IReadOnlyList<Node>)_value ?? _emptyListValue;
            }
        }
    }
    internal static class NodeExtensions {
        internal const string CSTokenLabel = "_SyntaxToken";
        internal const string XTokenLabel = "XToken";
        internal const string WTokenLabel = "WToken";
        internal const string TokenKindName = "Kind";
        internal const string TokenTextName = "Text";
        internal static bool IsCSToken(this Node tokenNode) { return tokenNode.Label == CSTokenLabel; }
        internal static bool IsXToken(this Node tokenNode) { return tokenNode.Label == XTokenLabel; }
        internal static bool IsWToken(this Node tokenNode) { return tokenNode.Label == WTokenLabel; }
        internal static SyntaxKind CSTokenKind(this Node kindNode) { return (SyntaxKind)kindNode.Value; }
        internal static XTokenKind XTokenKind(this Node kindNode) { return (XTokenKind)kindNode.Value; }
        internal static WTokenKind WTokenKind(this Node kindNode) { return (WTokenKind)kindNode.Value; }
        internal static SyntaxKind MemberCSTokenKind(this Node tokenNode) { return tokenNode.Member(TokenKindName).CSTokenKind(); }
        internal static XTokenKind MemberXTokenKind(this Node tokenNode) { return tokenNode.Member(TokenKindName).XTokenKind(); }
        internal static WTokenKind MemberWTokenKind(this Node tokenNode) { return tokenNode.Member(TokenKindName).WTokenKind(); }
        internal static string MemberTokenText(this Node tokenNode) { return (string)tokenNode.Member(TokenTextName).Value; }
        //
        internal static bool IsCSNodeLabel(this string label) { return label[0] == '_'; }
        internal static bool IsNotCSNodeLabel(this string label) { return !label.IsCSNodeLabel(); }
        internal static bool IsCSNode(this Node node) { return node.Label.IsCSNodeLabel(); }
        internal static bool IsNotCSNode(this Node node) { return !node.IsCSNode(); }
        internal static IEnumerable<Node> CSNodes(this IEnumerable<Node> source) { return source.Where(i => i.IsCSNode()); }
        internal static IEnumerable<Node> NonCSNodes(this IEnumerable<Node> source) { return source.Where(i => i.IsNotCSNode()); }
        //
        internal static void ProcessAnnotations(Node node, Func<string, Node, bool> handler, X.ErrorKind duplicateAnnotationErrorKind) {
            ProcessAnnotations(node, handler, (int)duplicateAnnotationErrorKind);
        }
        internal static void ProcessAnnotations(Node node, Func<string, Node, bool> handler, W.ErrorKind duplicateAnnotationErrorKind) {
            ProcessAnnotations(node, handler, (int)duplicateAnnotationErrorKind);
        }
        internal const string AnnotationsLabel = "Annotations";
        internal static void ProcessAnnotations(Node node, Func<string, Node, bool> handler, int duplicateAnnotationErrorCode) {
            if (node.IsStruct) {
                Node annotationsNode;
                if (node.Members.TryGetValue(AnnotationsLabel, out annotationsNode)) {
                    if (annotationsNode.IsNotNull) {
                        foreach (var annotationNode in annotationsNode.Items)
                            if (!handler(annotationNode.Label, annotationNode))
                                CompilationContextBase.Throw(annotationNode, duplicateAnnotationErrorCode, annotationNode.Label);
                    }
                }
            }
        }
        //
        internal static void Dump(this Node node, TextBuffer buf) {
            if (node == null) { buf.Write("<error>"); return; }
            if (node.IsNull) buf.Write("null");
            else if (node.IsAtom) buf.Write(node.Value.ToString());
            else {
                var label = node.Label;
                if (label != null) buf.Write(label);
                if (node.IsBox) {
                    buf.WriteLine("(");
                    buf.PushIndent();
                    node.Singleton.Dump(buf);
                    buf.WriteLine();
                    buf.PopIndent();
                    buf.Write(")");
                }
                else if (node.IsList) {
                    buf.WriteLine("[");
                    buf.PushIndent();
                    foreach (var i in node.Items) {
                        i.Dump(buf);
                        buf.WriteLine();
                    }
                    buf.PopIndent();
                    buf.Write("]");
                }
                else {
                    buf.WriteLine("{");
                    buf.PushIndent();
                    foreach (var i in node.Members) {
                        buf.Write(i.Key + " = ");
                        i.Value.Dump(buf);
                        buf.WriteLine();
                    }
                    buf.PopIndent();
                    buf.Write("}");
                }
            }
            var sourceSpan = node.SourceSpan;
            if (sourceSpan != null) buf.Write("<{0}>", sourceSpan.ToString());
        }
    }
    //
    //
    //
    public sealed class CompilationInputFile {
        public CompilationInputFile(string filePath, string text = null) {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException("filePath");
            FilePath = filePath;
            _text = text;
        }
        public readonly string FilePath;
        private string _text;
        public string Text { get { return _text ?? (_text = File.ReadAllText(FilePath)); } }
    }
    public sealed class CompilationInputFileList : List<CompilationInputFile> {
        public CompilationInputFileList() { }
        public CompilationInputFileList(IEnumerable<CompilationInputFile> items) : base(items) { }
    }
    public sealed class PreprocessorSymbolList : List<string> {
        public PreprocessorSymbolList() { }
        public PreprocessorSymbolList(IEnumerable<string> items) : base(items) { }
    }
    public sealed class MetadataReferenceList : List<MetadataReference> {
        public MetadataReferenceList() { }
        public MetadataReferenceList(IEnumerable<MetadataReference> items) : base(items) { }
    }
    public abstract class CompilationInput {
        protected CompilationInput(PreprocessorSymbolList preprocessorSymbolList) {
            if (preprocessorSymbolList == null) throw new ArgumentNullException("preprocessorSymbolList");
            PreprocessorSymbolList = preprocessorSymbolList;
        }
        public readonly PreprocessorSymbolList PreprocessorSymbolList;
        private ImmutableArray<string>? _preprocessorSymbolArray;
        internal ImmutableArray<string> PreprocessorSymbolArray {
            get {
                if (_preprocessorSymbolArray == null)
                    _preprocessorSymbolArray = PreprocessorSymbolList.ToImmutableArray();
                return _preprocessorSymbolArray.Value;
            }
        }
        public abstract bool NeedCompile { get; }
    }
    public abstract class CSharpCompilationInput : CompilationInput {
        protected CSharpCompilationInput(PreprocessorSymbolList preprocessorSymbolList, CompilationInputFileList cSharpFileList, MetadataReferenceList metadataReferenceList)
            : base(preprocessorSymbolList) {
            if (cSharpFileList == null) throw new ArgumentNullException("cSharpFileList");
            if (metadataReferenceList == null) throw new ArgumentNullException("metadataReferenceList");
            CSharpFileList = cSharpFileList;
            MetadataReferenceList = metadataReferenceList;
        }
        public readonly CompilationInputFileList CSharpFileList;
        public readonly MetadataReferenceList MetadataReferenceList;
    }
    public abstract class CompilationOutput {
        protected CompilationOutput(ErrorList errorList) {
            if (errorList == null) throw new ArgumentNullException("errorList");
            ErrorList = errorList;
        }
        public readonly ErrorList ErrorList;
        public bool HasErrors { get { return ErrorList.HasErrors(0); } }
    }
    //
    //
    //
    public sealed class XCompilationInput : CSharpCompilationInput {
        public XCompilationInput(PreprocessorSymbolList preprocessorSymbolList, CompilationInputFileList cSharpFileList, MetadataReferenceList metadataReferenceList,
            CompilationInputFileList xCSharpFileList, CompilationInputFileList xFileList)
            : base(preprocessorSymbolList, cSharpFileList, metadataReferenceList) {
            if (xCSharpFileList == null) throw new ArgumentNullException("xCSharpFileList");
            if (xFileList == null) throw new ArgumentNullException("xFileList");
            XCSharpFileList = xCSharpFileList;
            XFileList = xFileList;
        }
        public readonly CompilationInputFileList XCSharpFileList;
        public readonly CompilationInputFileList XFileList;
        //public readonly CompilationInputFileList XsdFileList;
        public override bool NeedCompile { get { return XCSharpFileList.Count > 0 || XFileList.Count > 0; } }
    }
    public sealed class XCompilationOutput : CompilationOutput {
        internal XCompilationOutput(ErrorList errorList, X.Analyzer analyzer)
            : base(errorList) {
            Analyzer = analyzer;
        }
        public readonly X.Analyzer Analyzer;//opt
    }
    public sealed class WCompilationInput : CSharpCompilationInput {
        public WCompilationInput(PreprocessorSymbolList preprocessorSymbolList, CompilationInputFileList cSharpFileList, MetadataReferenceList metadataReferenceList,
            CompilationInputFileList wFileList)
            : base(preprocessorSymbolList, cSharpFileList, metadataReferenceList) {
            if (wFileList == null) throw new ArgumentNullException("wFileList");
            WFileList = wFileList;
        }
        public readonly CompilationInputFileList WFileList;
        public override bool NeedCompile { get { return WFileList.Count > 0; } }
    }
    public sealed class WCompilationOutput : CompilationOutput {
        internal WCompilationOutput(ErrorList errorList, W.Analyzer analyzer)
            : base(errorList) {
            Analyzer = analyzer;
        }
        public readonly W.Analyzer Analyzer;//opt
    }
    //
    //
    //
    internal abstract class AnalyzerInputItem {
        internal AnalyzerInputItem(CompilationInputFile compilationInputFile) {
            CompilationInputFile = compilationInputFile;
        }
        internal readonly CompilationInputFile CompilationInputFile;
        internal string FilePath { get { return CompilationInputFile.FilePath; } }
        //internal string Text { get { return CompilationInputFile.Text; } }
    }
    internal sealed class NodeAnalyzerInputItem : AnalyzerInputItem {
        internal NodeAnalyzerInputItem(CompilationInputFile compilationInputFile, Node node)
            : base(compilationInputFile) {
            if (node == null) throw new ArgumentNullException("node");
            _node = node;
        }
        private Node _node;
        internal Node GetNodeOnce() {
            if (_node == null) throw new InvalidOperationException();
            var node = _node; _node = null; return node;
        }
    }
    internal sealed class NodeAnalyzerInputItemList : List<NodeAnalyzerInputItem> { }
    internal sealed class CSharpAnalyzerInputItem : AnalyzerInputItem {
        internal CSharpAnalyzerInputItem(CompilationInputFile compilationInputFile, CSharpCompilationInput csCompilationInput)
            : base(compilationInputFile) {
            CSharpCompilationInput = csCompilationInput;
        }
        internal readonly CSharpCompilationInput CSharpCompilationInput;
        private SyntaxTree _syntaxTree;
        internal SyntaxTree SyntaxTree {
            get {
                return _syntaxTree ?? (_syntaxTree = CSharpSyntaxTree.ParseText(text: CompilationInputFile.Text,
                    options: new CSharpParseOptions(documentationMode: DocumentationMode.None, preprocessorSymbols: CSharpCompilationInput.PreprocessorSymbolArray),
                    path: CompilationInputFile.FilePath));
            }
        }
    }
    internal sealed class CSharpAnalyzerInputItemList : List<CSharpAnalyzerInputItem> { }
    internal abstract class AnalyzerInput {
        internal AnalyzerInput(CompilationInput compilationInput) {
            CompilationInput = compilationInput;
        }
        internal readonly CompilationInput CompilationInput;
    }
    internal abstract class CSharpAnalyzerInput : AnalyzerInput {
        internal CSharpAnalyzerInput(CSharpCompilationInput compilationInput, CSharpAnalyzerInputItemList cSharpItemList)
            : base(compilationInput) {
            CSharpItemList = cSharpItemList;
        }
        internal readonly CSharpAnalyzerInputItemList CSharpItemList;
    }
    internal sealed class XAnalyzerInput : CSharpAnalyzerInput {
        internal XAnalyzerInput(XCompilationInput compilationInput, CSharpAnalyzerInputItemList cSharpItemList, NodeAnalyzerInputItemList xCSharpItemList, NodeAnalyzerInputItemList xItemList)
            : base(compilationInput, cSharpItemList) {
            XCSharpItemList = xCSharpItemList;
            XItemList = xItemList;
        }
        new internal XCompilationInput CompilationInput { get { return (XCompilationInput)base.CompilationInput; } }
        internal readonly NodeAnalyzerInputItemList XCSharpItemList;
        internal readonly NodeAnalyzerInputItemList XItemList;
        //internal CompilationInputFileList XsdFiles { get { return CompilationInput.XsdFiles; } }
    }
    internal sealed class WAnalyzerInput : CSharpAnalyzerInput {
        internal WAnalyzerInput(WCompilationInput compilationInput, CSharpAnalyzerInputItemList cSharpItemList, NodeAnalyzerInputItemList wItemList)
            : base(compilationInput, cSharpItemList) {
            WItemList = wItemList;
        }
        new internal WCompilationInput CompilationInput { get { return (WCompilationInput)base.CompilationInput; } }
        internal readonly NodeAnalyzerInputItemList WItemList;
    }
    public static class XCompiler {
        public static XCompilationOutput Compile(XCompilationInput compilationInput) {
            if (compilationInput == null) throw new ArgumentNullException("compilationInput");
            if (!compilationInput.NeedCompile) throw new InvalidOperationException("compilationInput need not compile");
            var errorList = new ErrorList();
            X.Analyzer analyzer = null;
            try {
                var xCSharpItemList = new NodeAnalyzerInputItemList();
                foreach (var xCSharpFile in compilationInput.XCSharpFileList) {
                    var node = XParser.Parse(xCSharpFile.FilePath, xCSharpFile.Text, compilationInput.PreprocessorSymbolList, errorList);
                    if (node.IsNull) goto end;
                    xCSharpItemList.Add(new NodeAnalyzerInputItem(xCSharpFile, node));
                }
                var xItemList = new NodeAnalyzerInputItemList();
                foreach (var xFile in compilationInput.XFileList) {
                    var node = XParser.Parse(xFile.FilePath, xFile.Text, compilationInput.PreprocessorSymbolList, errorList);
                    if (node.IsNull) goto end;
                    xItemList.Add(new NodeAnalyzerInputItem(xFile, node));
                }
                var cSharpItemList = new CSharpAnalyzerInputItemList();
                foreach (var csFile in compilationInput.CSharpFileList) {
                    cSharpItemList.Add(new CSharpAnalyzerInputItem(csFile, compilationInput));
                }
                var analyzerInput = new XAnalyzerInput(compilationInput, cSharpItemList, xCSharpItemList, xItemList);
                CompilationContextBase.BeginTrace(CompilationSubkind.Analyzing, X.CompilationContext.GetErrorMessageFormat, errorList);
                try {
                    analyzer = new X.Analyzer(analyzerInput);
                }
                finally { CompilationContextBase.EndTrace(); }
            end: ;
            }
            catch (CompilationErrorException ex) { errorList.Add(ex.Error); }
            catch (CompilationException) { }
            catch (Exception ex) { errorList.Add(new Error(CompilationSubkind.Unspecified, ErrorSeverity.Error, null, Error.XStart, "Internal compiler error: " + ex)); }
            return new XCompilationOutput(errorList, analyzer);
        }
    }
    public static class WCompiler {
        public static WCompilationOutput Compile(WCompilationInput compilationInput) {
            if (compilationInput == null) throw new ArgumentNullException("compilationInput");
            if (!compilationInput.NeedCompile) throw new InvalidOperationException("compilationInput need not compile");
            var errorList = new ErrorList();
            W.Analyzer analyzer = null;
            try {
                var wItemList = new NodeAnalyzerInputItemList();
                foreach (var wFile in compilationInput.WFileList) {
                    var node = WParser.Parse(wFile.FilePath, wFile.Text, compilationInput.PreprocessorSymbolList, errorList);
                    if (node.IsNull) goto end;
                    wItemList.Add(new NodeAnalyzerInputItem(wFile, node));
                }
                var cSharpItemList = new CSharpAnalyzerInputItemList();
                foreach (var csFile in compilationInput.CSharpFileList) {
                    cSharpItemList.Add(new CSharpAnalyzerInputItem(csFile, compilationInput));
                }
                var analyzerInput = new WAnalyzerInput(compilationInput, cSharpItemList, wItemList);
                CompilationContextBase.BeginTrace(CompilationSubkind.Analyzing, W.CompilationContext.GetErrorMessageFormat, errorList);
                try {
                    analyzer = new W.Analyzer(analyzerInput);
                }
                finally { CompilationContextBase.EndTrace(); }
            end: ;
            }
            catch (CompilationErrorException ex) { errorList.Add(ex.Error); }
            catch (CompilationException) { }
            catch (Exception ex) { errorList.Add(new Error(CompilationSubkind.Unspecified, ErrorSeverity.Error, null, Error.WStart, "Internal compiler error: " + ex)); }
            return new WCompilationOutput(errorList, analyzer);
        }
    }
    //
    //
    //
    public enum CompilationSubkind { Unspecified = 0, Parsing, Analyzing, /*Generating,*/ }
    public enum ErrorSeverity {
        Error = DiagnosticSeverity.Error,
        Warning = DiagnosticSeverity.Warning,
        Info = DiagnosticSeverity.Info
    }
    [Serializable]
    public sealed class Error {
        public Error(CompilationSubkind subkind, ErrorSeverity severity, SourceSpan sourceSpan, int code, string message)
            : this(subkind, severity, sourceSpan, code, GetCodeString(code), message) { }
        public Error(CompilationSubkind subkind, ErrorSeverity severity, SourceSpan sourceSpan, int code, string codeString, string message) {
            if (message == null) throw new ArgumentNullException("message");
            Subkind = subkind;
            Severity = severity;
            SourceSpan = sourceSpan;
            Code = code;
            CodeString = codeString;
            Message = message + GetMessageSuffixes();
        }
        public readonly CompilationSubkind Subkind;
        public readonly ErrorSeverity Severity;
        public readonly SourceSpan SourceSpan;//opt
        private readonly int Code;
        public readonly string CodeString;
        public readonly string Message;
        public bool IsError { get { return Severity == ErrorSeverity.Error; } }
        public bool IsWarning { get { return Severity == ErrorSeverity.Warning; } }
        public bool IsInfo { get { return Severity == ErrorSeverity.Info; } }
        public const int XStart = 20000;
        public const int WStart = 30000;
        private static string GetCodeString(int code) {
            if (code < XStart) return "CS" + code.ToString("0000", CultureInfo.InvariantCulture);
            if (code < WStart) return "X" + code.ToInvariantString();
            return "W" + code.ToInvariantString();
        }
        //
        [ThreadStatic]
        private static Stack<string> _messageSuffixStack;
        private static Stack<string> MessageSuffixStack { get { return _messageSuffixStack ?? (_messageSuffixStack = new Stack<string>()); } }
        internal static void PushMessageSuffix(string s) {
            if (s != null) MessageSuffixStack.Push(s);
        }
        internal static void PopMessageSuffix() {
            if (_messageSuffixStack != null && _messageSuffixStack.Count > 0)
                _messageSuffixStack.Pop();
        }
        internal static string GetMessageSuffixes() {
            if (_messageSuffixStack == null || _messageSuffixStack.Count == 0) return null;
            if (_messageSuffixStack.Count == 1) return _messageSuffixStack.Peek();
            var sb = new StringBuilder();
            foreach (var i in _messageSuffixStack) sb.Append(i);
            return sb.ToString();
        }
    }
    [Serializable]
    public sealed class ErrorList : List<Error> {
        public ErrorList() { }
        public ErrorList(IEnumerable<Error> items) : base(items) { }
        public bool HasErrors(int startIndex = 0) {
            for (var i = startIndex; i < Count; i++)
                if (base[i].IsError) return true;
            return false;
        }
    }
    internal class CompilationException : Exception {
        internal CompilationException() { }
    }
    internal sealed class CompilationErrorException : CompilationException {
        internal CompilationErrorException(Error error) {
            if (error == null) throw new ArgumentNullException("error");
            Error = error;
        }
        internal readonly Error Error;
    }
    internal abstract class CompilationContextBase {
        protected CompilationContextBase() { }
        [ThreadStatic]
        private static CompilationSubkind? _subkind;
        internal static CompilationSubkind Subkind {
            get {
                if (_subkind == null) throw new InvalidOperationException();
                return _subkind.Value;
            }
        }
        [ThreadStatic]
        private static Func<int, string> _errorMessageFormater;
        internal static Func<int, string> ErrorMessageFormater {
            get {
                if (_errorMessageFormater == null) throw new InvalidOperationException();
                return _errorMessageFormater;
            }
        }
        [ThreadStatic]
        private static ErrorList _errorList;
        internal static ErrorList ErrorList {
            get {
                if (_errorList == null) throw new InvalidOperationException();
                return _errorList;
            }
        }
        //f**k roslyn
        [ThreadStatic]
        private static List<SourceSpan> _sourceSpanList;
        private static List<SourceSpan> SourceSpanList {
            get {
                if (_sourceSpanList == null) throw new InvalidOperationException();
                return _sourceSpanList;
            }
        }
        internal static string AddSourceSpan(SourceSpan sourceSpan) {
            var list = SourceSpanList;
            list.Add(sourceSpan);
            return (list.Count - 1).ToInvariantString();
        }
        internal static SourceSpan GetSourceSpan(string index) {
            return SourceSpanList[index.ToInt32()];
        }
        //end f**k roslyn
        internal static void BeginTrace(CompilationSubkind subkind, Func<int, string> errorMessageFormater, ErrorList errorList = null) {
            if (errorMessageFormater == null) throw new ArgumentNullException("errorMessageFormater");
            _subkind = subkind;
            _errorMessageFormater = errorMessageFormater;
            if (errorList == null) errorList = new ErrorList();
            _errorList = errorList;
            if (_sourceSpanList == null) _sourceSpanList = new List<SourceSpan>();
            else _sourceSpanList.Clear();
        }
        internal static ErrorList EndTrace() {
            var errorList = ErrorList;
            _subkind = null;
            _errorMessageFormater = null;
            _errorList = null;
            if (_sourceSpanList != null) _sourceSpanList.Clear();
            return errorList;
        }
        internal static bool HasErrors { get { return ErrorList.HasErrors(0); } }
        internal static void ThrowIfHasErrors() { if (HasErrors) throw new CompilationException(); }
        internal static void Report(Error error) {
            if (error == null) throw new ArgumentNullException("error");
            ErrorList.Add(error);
        }
        internal static Error CreateError(ErrorSeverity severity, SourceSpan sourceSpan, int code, params object[] msgArgs) {
            //if (sourceSpan == null) throw new ArgumentNullException("sourceSpan");
            return new Error(Subkind, severity, sourceSpan, code, ErrorMessageFormater(code).InvariantFormat(msgArgs));
        }
        internal static void Throw(SourceSpan sourceSpan, int code, params object[] msgArgs) {
            throw new CompilationErrorException(CreateError(ErrorSeverity.Error, sourceSpan, code, msgArgs));
        }
        public static void Throw(SourceSpan sourceSpan, X.ErrorKind kind, params object[] msgArgs) { Throw(sourceSpan, (int)kind, msgArgs); }
        public static void Throw(SourceSpan sourceSpan, W.ErrorKind kind, params object[] msgArgs) { Throw(sourceSpan, (int)kind, msgArgs); }
        internal static void Throw(ISourceSpanProvider sourceSpanProvider, int code, params object[] msgArgs) {
            if (sourceSpanProvider == null) throw new ArgumentNullException("sourceSpanProvider");
            Throw(sourceSpanProvider.SourceSpan, code, msgArgs);
        }
        internal static void Throw(ISourceSpanProvider sourceSpanProvider, X.ErrorKind kind, params object[] msgArgs) { Throw(sourceSpanProvider, (int)kind, msgArgs); }
        internal static void Throw(ISourceSpanProvider sourceSpanProvider, W.ErrorKind kind, params object[] msgArgs) { Throw(sourceSpanProvider, (int)kind, msgArgs); }
        internal static void Error(SourceSpan sourceSpan, int code, params object[] msgArgs) { Report(CreateError(ErrorSeverity.Error, sourceSpan, code, msgArgs)); }
        internal static void Error(SourceSpan sourceSpan, X.ErrorKind kind, params object[] msgArgs) { Error(sourceSpan, (int)kind, msgArgs); }
        internal static void Error(SourceSpan sourceSpan, W.ErrorKind kind, params object[] msgArgs) { Error(sourceSpan, (int)kind, msgArgs); }
        internal static void Error(ISourceSpanProvider sourceSpanProvider, int code, params object[] msgArgs) {
            if (sourceSpanProvider == null) throw new ArgumentNullException("sourceSpanProvider");
            Error(sourceSpanProvider.SourceSpan, code, msgArgs);
        }
        internal static void Error(ISourceSpanProvider sourceSpanProvider, X.ErrorKind kind, params object[] msgArgs) { Error(sourceSpanProvider, (int)kind, msgArgs); }
        internal static void Error(ISourceSpanProvider sourceSpanProvider, W.ErrorKind kind, params object[] msgArgs) { Error(sourceSpanProvider, (int)kind, msgArgs); }
        internal static void Warning(SourceSpan sourceSpan, int code, params object[] msgArgs) { Report(CreateError(ErrorSeverity.Warning, sourceSpan, code, msgArgs)); }
        internal static void Warning(SourceSpan sourceSpan, X.ErrorKind kind, params object[] msgArgs) { Warning(sourceSpan, (int)kind, msgArgs); }
        internal static void Warning(SourceSpan sourceSpan, W.ErrorKind kind, params object[] msgArgs) { Warning(sourceSpan, (int)kind, msgArgs); }
        internal static void Warning(ISourceSpanProvider sourceSpanProvider, int code, params object[] msgArgs) {
            if (sourceSpanProvider == null) throw new ArgumentNullException("sourceSpanProvider");
            Warning(sourceSpanProvider.SourceSpan, code, msgArgs);
        }
        internal static void Warning(ISourceSpanProvider sourceSpanProvider, X.ErrorKind kind, params object[] msgArgs) { Warning(sourceSpanProvider, (int)kind, msgArgs); }
        internal static void Warning(ISourceSpanProvider sourceSpanProvider, W.ErrorKind kind, params object[] msgArgs) { Warning(sourceSpanProvider, (int)kind, msgArgs); }
    }
    //
    //
    //
    public abstract class ValueBase : ISourceSpanProvider {
        protected ValueBase(SourceSpan sourceSpan = null) { SourceSpan = sourceSpan; }
        public SourceSpan SourceSpan { get; protected set; }
        protected virtual void Initialize(Node node) { SourceSpan = node.SourceSpan; }
    }
    public sealed class SimpleToken : ValueBase {
        internal SimpleToken(Node node) { Initialize(node); }
    }
    public sealed class Identifier : ValueBase, IEquatable<Identifier> {
        internal Identifier(Node tokenNode) {
            base.Initialize(tokenNode);
            Value = tokenNode.MemberTokenText();
            PlainValue = Value.UnescapeIdentifier();
        }
        internal Identifier(string value, SourceSpan sourceSpan = null)
            : base(sourceSpan) {
            Value = value;
            PlainValue = value.UnescapeIdentifier();
        }
        internal readonly string Value;
        internal readonly string PlainValue;
        internal static bool ValueEquals(string x, string y) { return x.UnescapeIdentifier() == y.UnescapeIdentifier(); }
        public override string ToString() { return PlainValue; }
        private SyntaxToken? _csToken;
        internal SyntaxToken CSToken {
            get {
                if (_csToken == null) {
                    _csToken = CS.Id(Value).SetSourceSpan(SourceSpan);
                }
                return _csToken.Value;
            }
        }
        internal IdentifierNameSyntax CSIdName { get { return CS.IdName(CSToken); } }
        public bool Equals(Identifier other) {
            if (object.ReferenceEquals(this, other)) return true;
            if (object.ReferenceEquals(other, null)) return false;
            return PlainValue == other.PlainValue;
        }
        public override bool Equals(object obj) { return Equals(obj as Identifier); }
        public override int GetHashCode() { return PlainValue.GetHashCode(); }
        public static bool operator ==(Identifier left, Identifier right) {
            if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
            return left.Equals(right);
        }
        public static bool operator !=(Identifier left, Identifier right) { return !(left == right); }
    }
    public sealed class QualifiableName : ValueBase, IEquatable<QualifiableName> {
        internal QualifiableName(Node node) {
            base.Initialize(node);
            NameId = new Identifier(node.Member("Name"));
            AliasId = node.Member("Alias").ToIdentifierOpt();
        }
        internal QualifiableName(Identifier nameId, Identifier aliasId = null, SourceSpan sourceSpan = null)
            : base(sourceSpan) {
            if (nameId == null) throw new ArgumentNullException("nameId");
            NameId = nameId;
            AliasId = aliasId;
        }
        internal readonly Identifier NameId;
        internal readonly Identifier AliasId;//opt
        internal bool IsQualified { get { return AliasId != null; } }
        internal bool IsNotQualified { get { return !IsQualified; } }
        internal const string NodeLabel = "QualifiableName";
        public bool Equals(QualifiableName other) {
            if (object.ReferenceEquals(this, other)) return true;
            if (object.ReferenceEquals(other, null)) return false;
            return NameId == other.NameId && AliasId == other.AliasId;
        }
        public override bool Equals(object obj) { return Equals(obj as QualifiableName); }
        public override int GetHashCode() { return Extensions.CombineHash(NameId.GetHashCode(), AliasId == null ? 0 : AliasId.GetHashCode()); }
        public static bool operator ==(QualifiableName left, QualifiableName right) {
            if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
            return left.Equals(right);
        }
        public static bool operator !=(QualifiableName left, QualifiableName right) { return !(left == right); }
        private string _text;
        public override string ToString() {
            return _text ?? (_text = IsQualified ? AliasId.ToString() + ":" + NameId.ToString() : NameId.ToString());
        }
    }
    public sealed class DottedName : ValueBase, IEquatable<DottedName> {
        internal DottedName(Node node) {
            base.Initialize(node);
            var sb = new StringBuilder();
            foreach (var itemNode in node.Items) {
                var item = new Identifier(itemNode);
                ItemList.Add(item);
                if (sb.Length > 0) sb.Append('.');
                sb.Append(item.Value);
            }
            Text = sb.ToString();
        }
        internal DottedName(string text, SourceSpan sourceSpan = null) {
            if (string.IsNullOrEmpty(text)) throw new ArgumentNullException("text");
            SourceSpan = sourceSpan;
            foreach (var itemText in text.SplitByDot()) ItemList.Add(new Identifier(itemText.Trim(), sourceSpan));
            Text = text;
        }
        internal readonly List<Identifier> ItemList = new List<Identifier>();
        internal readonly string Text;
        public override string ToString() { return Text; }
        private NameSyntax _csNonGlobalFullName;//@NS1.NS2
        internal NameSyntax CSNonGlobalFullName {
            get {
                if (_csNonGlobalFullName == null) {
                    foreach (var item in ItemList) {
                        if (_csNonGlobalFullName == null) _csNonGlobalFullName = CS.IdName(item.Value);
                        else _csNonGlobalFullName = CS.QualifiedName(_csNonGlobalFullName, item.Value);
                    }
                    _csNonGlobalFullName = _csNonGlobalFullName.SetSourceSpan(SourceSpan);
                }
                return _csNonGlobalFullName;
            }
        }
        private NameSyntax _csFullName;//global::@NS1.NS2
        internal NameSyntax CSFullName {
            get {
                if (_csFullName == null) {
                    foreach (var item in ItemList) {
                        if (_csFullName == null) _csFullName = CS.GlobalAliasQualifiedName(item.Value);
                        else _csFullName = CS.QualifiedName(_csFullName, item.Value);
                    }
                    _csFullName = _csFullName.SetSourceSpan(SourceSpan);
                }
                return _csFullName;
            }
        }
        private ExpressionSyntax _csFullExp;//global::@NS1.NS2
        internal ExpressionSyntax CSFullExp {
            get {
                if (_csFullExp == null) {
                    foreach (var item in ItemList) {
                        if (_csFullExp == null) _csFullExp = CS.GlobalAliasQualifiedName(item.Value);
                        else _csFullExp = CS.MemberAccessExpr(_csFullExp, item.Value);
                    }
                    _csFullName = _csFullName.SetSourceSpan(SourceSpan);
                }
                return _csFullExp;
            }
        }
        public bool Equals(DottedName other) {
            if (object.ReferenceEquals(this, other)) return true;
            if (object.ReferenceEquals(other, null)) return false;
            if (ItemList.Count != other.ItemList.Count) return false;
            for (var i = 0; i < ItemList.Count; i++)
                if (ItemList[i] != other.ItemList[i]) return false;
            return true;
        }
        public override sealed bool Equals(object obj) { return Equals(obj as DottedName); }
        public override sealed int GetHashCode() {
            var hash = 17;
            foreach (var item in ItemList)
                hash = Extensions.AggregateHash(hash, item.GetHashCode());
            return hash;
        }
        public static bool operator ==(DottedName left, DottedName right) {
            if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
            return left.Equals(right);
        }
        public static bool operator !=(DottedName left, DottedName right) { return !(left == right); }
    }
    public abstract class SimpleValue<T> : ValueBase, IEquatable<SimpleValue<T>> {
        protected SimpleValue(IEqualityComparer<T> comparer = null) { _comparer = comparer; }
        protected SimpleValue(T value, IEqualityComparer<T> comparer = null, SourceSpan sourceSpan = null)
            : base(sourceSpan) {
            if (value == null) throw new ArgumentNullException("value");
            _comparer = comparer;
            Value = value;
        }
        public T Value { get; protected set; }
        private IEqualityComparer<T> _comparer;
        internal IEqualityComparer<T> Comparer { get { return _comparer ?? (_comparer = EqualityComparer<T>.Default); } }
        public virtual bool Equals(SimpleValue<T> other) {
            if (object.ReferenceEquals(this, other)) return true;
            if (object.ReferenceEquals(other, null)) return false;
            return Comparer.Equals(Value, other.Value);
        }
        public override sealed bool Equals(object obj) { return Equals(obj as SimpleValue<T>); }
        public override int GetHashCode() { return Comparer.GetHashCode(Value); }
        public static bool operator ==(SimpleValue<T> left, SimpleValue<T> right) {
            if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
            return left.Equals(right);
        }
        public static bool operator !=(SimpleValue<T> left, SimpleValue<T> right) { return !(left == right); }
        public override string ToString() {
            var formattable = Value as IFormattable;
            if (formattable != null) return formattable.ToString(null, CultureInfo.InvariantCulture);
            return Value.ToString();
        }
    }
    public sealed class StringValue : SimpleValue<string> {
        internal StringValue(Node tokenNode) {
            base.Initialize(tokenNode);
            Value = tokenNode.GetStringLiteralTokenValue();
        }
        internal StringValue(string value, SourceSpan sourceSpan = null) : base(value, sourceSpan: sourceSpan) { }
    }
    public sealed class UnsignedIntegerValue<T> : SimpleValue<T> where T : struct {
        internal UnsignedIntegerValue(Node tokenNode, int errorCode, T? defaultValue = null) {
            base.Initialize(tokenNode);
            if (tokenNode.IsCSToken() && tokenNode.MemberCSTokenKind() == SyntaxKind.NumericLiteralToken) {
                var text = tokenNode.MemberTokenText();
                object value = null;
                switch (Type.GetTypeCode(typeof(T))) {
                    case TypeCode.UInt64: {
                            ulong r;
                            if (ulong.TryParse(text, NumberStyles.None, NumberFormatInfo.InvariantInfo, out r))
                                value = r;
                        }
                        break;
                    case TypeCode.UInt32: {
                            uint r;
                            if (uint.TryParse(text, NumberStyles.None, NumberFormatInfo.InvariantInfo, out r))
                                value = r;
                        }
                        break;
                    case TypeCode.Byte: {
                            byte r;
                            if (byte.TryParse(text, NumberStyles.None, NumberFormatInfo.InvariantInfo, out r))
                                value = r;
                        }
                        break;
                    default: throw new InvalidOperationException();
                }
                if (value == null) CompilationContextBase.Throw(tokenNode, errorCode);
                Value = (T)value;
            }
            else {
                if (defaultValue == null) throw new InvalidOperationException();
                Value = (T)defaultValue;
            }
        }
        internal UnsignedIntegerValue(Node tokenNode, X.ErrorKind errorKind, T? defaultValue = null) : this(tokenNode, (int)errorKind, defaultValue) { }
        internal UnsignedIntegerValue(T value, SourceSpan sourceSpan = null) : base(value, sourceSpan: sourceSpan) { }
    }
    //public class ValueList<T> : ValueBase, IList<T>, IReadOnlyList<T> {
    //    internal ValueList(IEnumerable<T> items = null) {
    //        _list = new List<T>();
    //        if (items != null) AddRange(items);
    //    }
    //    private List<T> _list;
    //    protected List<T> List { get { return _list; } }
    //    public class ReadOnly : ReadOnlyCollection<T> {
    //        internal ReadOnly(ValueList<T> parent) : base(parent) { }
    //    }
    //    private ReadOnly _readOnlyObject;
    //    public ReadOnly ReadOnlyObject { get { return _readOnlyObject ?? (_readOnlyObject = new ReadOnly(this)); } }
    //    public void AddRange(IEnumerable<T> items) {
    //        if (items == null) throw new ArgumentNullException("items");
    //        foreach (var item in items) Add(item);
    //    }
    //    public void Add(T item) { _list.Add(item); }
    //    public void Insert(int index, T item) { _list.Insert(index, item); }
    //    public T this[int index] { get { return _list[index]; } set { _list[index] = value; } }
    //    public bool Remove(T item) { return _list.Remove(item); }
    //    public void RemoveAt(int index) { _list.RemoveAt(index); }
    //    public void Clear() { _list.Clear(); }
    //    public int Count { get { return _list.Count; } }
    //    public bool Contains(T item) { return _list.Contains(item); }
    //    public int IndexOf(T item) { return _list.IndexOf(item); }
    //    public IEnumerator<T> GetEnumerator() { return _list.GetEnumerator(); }
    //    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }
    //    public void CopyTo(T[] array, int arrayIndex) { _list.CopyTo(array, arrayIndex); }
    //    public bool IsReadOnly { get { return false; } }
    //}
    //public class ValueMap<TKey, TValue> : ValueBase, IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue> {
    //    internal ValueMap(IEqualityComparer<TKey> keyComparer = null, IEnumerable<KeyValuePair<TKey, TValue>> items = null) {
    //        _map = new Dictionary<TKey, TValue>(keyComparer);
    //        if (items != null) AddRange(items);
    //    }
    //    private Dictionary<TKey, TValue> _map;
    //    protected Dictionary<TKey, TValue> Map { get { return _map; } }
    //    protected ICollection<KeyValuePair<TKey, TValue>> Collection { get { return _map; } }
    //    public class ReadOnly : ReadOnlyDictionary<TKey, TValue> {
    //        internal ReadOnly(ValueMap<TKey, TValue> parent) : base(parent) { }
    //    }
    //    private ReadOnly _readOnlyObject;
    //    public ReadOnly ReadOnlyObject { get { return _readOnlyObject ?? (_readOnlyObject = new ReadOnly(this)); } }
    //    public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items) {
    //        if (items == null) throw new ArgumentNullException("items");
    //        foreach (var item in items) Add(item);
    //    }
    //    public void Add(TKey key, TValue value) { _map.Add(key, value); }
    //    public void Add(KeyValuePair<TKey, TValue> item) { Collection.Add(item); }
    //    public TValue this[TKey key] { get { return _map[key]; } set { _map[key] = value; } }
    //    public bool Remove(TKey key) { return _map.Remove(key); }
    //    public bool Remove(KeyValuePair<TKey, TValue> item) { return Collection.Remove(item); }
    //    public void Clear() { _map.Clear(); }
    //    public int Count { get { return _map.Count; } }
    //    public ICollection<TKey> Keys { get { return _map.Keys; } }
    //    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys { get { return Keys; } }
    //    public ICollection<TValue> Values { get { return _map.Values; } }
    //    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values { get { return Values; } }
    //    public bool TryGetValue(TKey key, out TValue value) { return _map.TryGetValue(key, out value); }
    //    public TValue TryGetValue(TKey key) {
    //        TValue value;
    //        TryGetValue(key, out value);
    //        return value;
    //    }
    //    public bool ContainsKey(TKey key) { return _map.ContainsKey(key); }
    //    public bool Contains(KeyValuePair<TKey, TValue> item) { return Collection.Contains(item); }
    //    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() { return _map.GetEnumerator(); }
    //    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }
    //    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) { Collection.CopyTo(array, arrayIndex); }
    //    public bool IsReadOnly { get { return false; } }
    //}
    public abstract class ObjectBase : ValueBase {
        protected ObjectBase() { }
        private ObjectBase _parent;
        public ObjectBase Parent {
            get { return _parent; }
            protected set { if (_parent == null) _parent = value; }
        }
        protected T SetParentTo<T>(T child) where T : ObjectBase {
            if (child != null) child.Parent = this;
            return child;
        }
        public T GetAncestor<T>(bool @try = false, bool testSelf = false) where T : class {
            for (var obj = testSelf ? this : _parent; obj != null; obj = obj._parent) {
                var res = obj as T;
                if (res != null) return res;
            }
            if (!@try) throw new InvalidOperationException("Cannot get ancestor of type: " + typeof(T).FullName);
            return null;
        }
    }
    internal static class Extensions {
        //f**k roslyn
        internal const string SourceSpanSyntaxAnnotationKindName = "MetahSourceSpan";
        internal static SyntaxToken SetSourceSpan(this SyntaxToken token, SourceSpan sourceSpan) {
            if (sourceSpan == null) return token;
            return token.WithAdditionalAnnotations(new SyntaxAnnotation(SourceSpanSyntaxAnnotationKindName, CompilationContextBase.AddSourceSpan(sourceSpan)));
        }
        internal static T SetSourceSpan<T>(this T node, SourceSpan sourceSpan) where T : SyntaxNode {
            if (sourceSpan == null) return node;
            return node.WithAdditionalAnnotations(new SyntaxAnnotation(SourceSpanSyntaxAnnotationKindName, CompilationContextBase.AddSourceSpan(sourceSpan)));
        }
        internal static SourceSpan GetSourceSpan(this SyntaxToken token) {
            var ann = token.GetAnnotations(SourceSpanSyntaxAnnotationKindName).FirstOrDefault();
            if (ann == null) return null;
            return CompilationContextBase.GetSourceSpan(ann.Data);
        }
        internal static SourceSpan GetSourceSpan(this SyntaxNode node) {
            var ann = node.GetAnnotations(SourceSpanSyntaxAnnotationKindName).FirstOrDefault();
            if (ann == null) return null;
            return CompilationContextBase.GetSourceSpan(ann.Data);
        }
        internal static SourceSpan GetAnySourceSpan(this SyntaxNode node) {
            foreach (var nt in node.GetAnnotatedNodesAndTokens(SourceSpanSyntaxAnnotationKindName)) {
                var snode = nt.AsNode();
                if (snode != null) return snode.GetSourceSpan();
                return nt.AsToken().GetSourceSpan();
            }
            return null;
        }
        //end f**k roslyn

        internal static SimpleToken ToSimpleTokenOpt(this Node node) {
            if (node.IsNull) return null;
            return new SimpleToken(node);
        }
        internal static Identifier ToIdentifierOpt(this Node node) {
            if (node.IsNull) return null;
            return new Identifier(node);
        }
        internal static QualifiableName ToQualifiableNameOpt(this Node node) {
            if (node.IsNull) return null;
            return new QualifiableName(node);
        }

        private volatile static char[] _dotCharArray;
        private static char[] DotCharArray { get { return _dotCharArray ?? (_dotCharArray = new char[] { '.' }); } }
        internal static string[] SplitByDot(this string s) {
            if (s == null) throw new ArgumentNullException("s");
            return s.Split(DotCharArray, StringSplitOptions.RemoveEmptyEntries);
        }
        internal static string GetSeparatedString<T>(IReadOnlyList<T> items, Func<T, string> convertor, string separator = ", ") {
            if (items == null) return null;
            var count = items.Count;
            if (count == 0) return "";
            if (count == 1) return convertor(items[0]);
            var sb = new StringBuilder();
            for (var i = 0; i < count; i++) {
                if (i > 0) sb.Append(separator);
                sb.Append(convertor(items[i]));
            }
            return sb.ToString();
        }
        internal static string InvariantFormat(this string format, params object[] args) {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }
        internal static string ToInvariantString(this int i) { return i.ToString(CultureInfo.InvariantCulture); }
        internal static string ToInvariantString(this uint i) { return i.ToString(CultureInfo.InvariantCulture); }
        internal static string ToInvariantString(this ulong i) { return i.ToString(CultureInfo.InvariantCulture); }
        internal static string ToInvariantString(this byte i) { return i.ToString(CultureInfo.InvariantCulture); }
        internal static int ToInt32(this string str) { return int.Parse(str, CultureInfo.InvariantCulture); }
        //
        internal static void CreateAndAdd<T>(ref List<T> list, T item) {
            if (list == null) list = new List<T>();
            list.Add(item);
        }
        internal static void AddRange<TKey, TValue>(this IDictionary<TKey, TValue> dict, IEnumerable<KeyValuePair<TKey, TValue>> items) {
            if (items == null) throw new ArgumentNullException("items");
            foreach (var item in items) dict.Add(item.Key, item.Value);
        }
        internal static TValue TryGetValue<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dict, TKey key) where TValue : class {
            TValue value;
            if (dict.TryGetValue(key, out value)) return value;
            return null;
        }
        internal static void AddRange<T>(this HashSet<T> set, IEnumerable<T> items) {
            if (items == null) throw new ArgumentNullException("items");
            foreach (var item in items) set.Add(item);
        }
        internal static IEnumerable<T> Append<T>(this IEnumerable<T> source, T item) {
            if (source == null) throw new ArgumentNullException("source");
            foreach (var i in source) yield return i;
            yield return item;
        }
        internal static IEnumerable<T> Repeat<T>(Func<T> item, int count) {
            if (item == null) throw new ArgumentNullException("item");
            for (var i = 0; i < count; i++) yield return item();
        }
        internal static int AggregateHash(int hash, int newValue) { unchecked { return hash * 31 + newValue; } }
        internal static int CombineHash(int a, int b) {
            unchecked {
                int hash = 17;
                hash = hash * 31 + a;
                hash = hash * 31 + b;
                return hash;
            }
        }
        internal static int CombineHash(int a, int b, int c) {
            unchecked {
                int hash = 17;
                hash = hash * 31 + a;
                hash = hash * 31 + b;
                hash = hash * 31 + c;
                return hash;
            }
        }
        internal static int CombineHash(int a, int b, int c, int d) {
            unchecked {
                int hash = 17;
                hash = hash * 31 + a;
                hash = hash * 31 + b;
                hash = hash * 31 + c;
                hash = hash * 31 + d;
                return hash;
            }
        }
        internal const string CSBanner = @"//
//Auto-generated, DO NOT EDIT
//Visit http://metah.codeplex.com for more information
//
";
        internal const string XmlBanner = @"<?xml version='1.0'?>
<!--
    Auto-generated, DO NOT EDIT
    Visit http://metah.codeplex.com for more information
-->
";
        internal static string ToXmlString(this string s) { return System.Security.SecurityElement.Escape(s); }
        internal static string ToXmlString(this System.Xml.Linq.XNamespace ns) { return ns.NamespaceName.ToXmlString(); }
    }
    public sealed class TextBuffer {
        public TextBuffer(string indentString = "    ") {
            _stringBuilder = new StringBuilder();
            _isLastNewLine = true;
            IndentString = indentString;
        }
        private readonly StringBuilder _stringBuilder;
        public StringBuilder StringBuilder { get { return _stringBuilder; } }
        public override string ToString() { return _stringBuilder.ToString(); }
        private bool _isLastNewLine;
        private static readonly string[] _newLineStrings = new string[] { "\r\n" };
        private string _indentString;
        public string IndentString {
            get { return _indentString; }
            set {
                if (string.IsNullOrEmpty(value)) throw new ArgumentException("Indent string null or empty");
                _indentString = value;
            }
        }
        private int _indentCount;
        public int IndentCount { get { return _indentCount; } }
        public int PushIndent(int count = 1) {
            if (count < 1) throw new ArgumentOutOfRangeException("count");
            return _indentCount += count;
        }
        public int PopIndent(int count = 1) {
            if (count < 1 || _indentCount - count < 0) throw new ArgumentOutOfRangeException("count");
            _indentCount -= count;
            return _indentCount;
        }
        public void WriteIndent() {
            for (var i = 0; i < _indentCount; i++)
                _stringBuilder.Append(_indentString);
        }
        public TextBuffer Write(string s) {
            if (s == null) throw new ArgumentNullException("s");
            if (s.Length > 0) {
                if (_isLastNewLine) {
                    WriteIndent();
                    _isLastNewLine = false;
                }
                var ss = s.Split(_newLineStrings, StringSplitOptions.None);
                _stringBuilder.Append(ss[0]);
                var ssCount = ss.Length;
                if (ssCount > 1) {
                    for (var i = 1; i < ssCount; i++) {
                        _stringBuilder.Append("\r\n");
                        var length = ss[i].Length;
                        if (length > 0) {
                            WriteIndent();
                            _stringBuilder.Append(ss[i]);
                        }
                        else if (i == ssCount - 1)
                            _isLastNewLine = true;
                    }
                }
            }
            return this;
        }
        public TextBuffer Write(string format, params object[] args) {
            return Write(format.InvariantFormat(args));
        }
        public TextBuffer WriteLine() {
            return Write("\r\n");
        }
        public TextBuffer WriteLine(string s) {
            return Write(s).WriteLine();
        }
        public TextBuffer WriteLine(string format, params object[] args) {
            return Write(format, args).WriteLine();
        }
    }
}
