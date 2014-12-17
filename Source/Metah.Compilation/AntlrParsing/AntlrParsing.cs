using System;
using System.Collections.Generic;
using Antlr.Runtime;

namespace Metah.Compilation {
    using Metah.Compilation.AntlrParsing;

#if false
    public static class __TestAntlrParsing {
        public static void Run() {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var tracer = new StreamWriter(@"d:\antlrTrace.txt");
            var filename = @"D:\testAntlr.txt";
            var text = File.ReadAllText(filename);
            var input = new ANTLRStringStream(text, filename);
            var lexer = new ELexer(input);
            var tokens = new CommonTokenStream(lexer);
            var parser = new EParserEx(tokens);
            Node node = null;
            try {
                node = parser.Main();
                CheckSameAsRoslyn(node, text, tracer);
            }
            catch (ParsingException pe) {
                tracer.WriteLine("ParsingException: " + pe.SourceSpan + " " + pe.Message);
            }
            catch (Exception ex) {
                tracer.WriteLine(ex);
            }
            finally {
                stopWatch.Stop();
                tracer.WriteLine("Parse time:" + stopWatch.ElapsedMilliseconds);
                tracer.Close();
            }
            //
            var buf = new TextBuffer();
            node.Dump(buf);
            using (var sw = new System.IO.StreamWriter(filename + ".dump.txt"))
                sw.Write(buf.ToString());

        }

        internal static void CheckSameAsRoslyn(Node node, string text, TextWriter tw) {
            //var tree = SyntaxTree.ParseText(text);
            //var csRoot = tree.GetRoot();
            //var myRoot = node.ToCSSyntaxNode();
            //var csNTs = csRoot.DescendantNodesAndTokens().ToArray();
            //var myNTs = myRoot.DescendantNodesAndTokens().ToArray();
            //if (csNTs.Length != myNTs.Length)
            //    tw.WriteLine("myNTs.Length {0} csNTs.Length:{1}", myNTs.Length, csNTs.Length);
            //var minLength = Math.Min(csNTs.Length, myNTs.Length);
            //for (var i = 0; i < minLength; i++) {
            //    var mynt = myNTs[i];
            //    var csnt = csNTs[i];
            //    if (mynt.Kind != csnt.Kind) {
            //        tw.WriteLine("mynt.Kind != csnt.Kind: {0}----<<<<MMMMMMMM>>>>{1} {2}, <<<<CCCCCCCC>>>>{3} {4}", mynt.GetXXLocation(),
            //            mynt.Kind, mynt.ToString(), csnt.Kind, csnt.ToString());
            //        break;
            //    }
            //    if (mynt.IsToken) {
            //        var myTokenText = mynt.AsToken().ToString();
            //        var csTokenText = csnt.AsToken().ToString();
            //        if (myTokenText != csTokenText) {
            //            tw.WriteLine("myTokenText != csTokenText: {0}----<<<<MMMMMMMM>>>>{1} {2}, <<<<CCCCCCCC>>>>{3} {4}", mynt.GetXXLocation(),
            //                mynt.Kind, mynt.ToString(), csnt.Kind, csnt.ToString());
            //            break;
            //        }
            //    }
            //}
        }

    }
#endif
    internal static class XParser {
        internal static Node Parse(string filePath, string text, IEnumerable<string> preprocessorSymbolList, ErrorList errorList) {
            var node = Node.Null;
            try {
                var chars = new ANTLRStringStream(text, filePath);
                var lexer = new XLexer(chars);
                lexer.AddPpSymbols(preprocessorSymbolList);
                var tokens = new CommonTokenStream(lexer);
                var parser = new XParserEx(tokens);
                node = parser.Main();
            }
            catch (ParsingException pe) {
                errorList.Add(new Error(CompilationSubkind.Parsing, ErrorSeverity.Error, pe.SourceSpan, Error.XStart, pe.Message));
            }
            return node;
        }
    }

    internal static class WParser {
        internal static Node Parse(string filePath, string text, IEnumerable<string> preprocessorSymbolList, ErrorList errorList) {
            var node = Node.Null;
            try {
                var chars = new ANTLRStringStream(text, filePath);
                var lexer = new WLexer(chars);
                lexer.AddPpSymbols(preprocessorSymbolList);
                var tokens = new CommonTokenStream(lexer);
                var parser = new WParserEx(tokens);
                node = parser.Main();
            }
            catch (ParsingException pe) {
                errorList.Add(new Error(CompilationSubkind.Parsing, ErrorSeverity.Error, pe.SourceSpan, Error.WStart, pe.Message));
            }
            return node;
        }
    }
}

namespace Metah.Compilation.AntlrParsing {
    internal sealed class NodeList : List<Node> {
        internal NodeList() { }
        internal void Add(CommonTokenEx token) { base.Add(token.Node); }
    }
    internal static class Extensions {
        internal static void AddTo(this Node node, ref NodeList list) {
            if (list == null) list = new NodeList();
            list.Add(node);
        }
        internal static void AddTo(this CommonTokenEx token, ref NodeList list) {
            if (list == null) list = new NodeList();
            list.Add(token);
        }
        internal static SourceSpan CreateSourceSpan(string filePath, int startIndex, int startLine, int startCol, string text) {
            startCol++;
            int endLine = startLine, endCol = startCol;
            var length = text == null ? 0 : text.Length;
            if (length > 3 && text[0] == '@' && text[1] == '"') {
                for (var i = 0; i < length; i++) {
                    //ANTLR只检查了'\n'(见ANTLRStringStream.Consume())，与它保持一致
                    //完整的:'\r' || '\n' || "\r\n" || '\u0085' || '\u2028' || '\u2029'
                    endCol++;
                    if (text[i] == '\n') {
                        endLine++;
                        endCol = 1;
                    }
                }
            }
            else endCol += length;
            return new SourceSpan(filePath, startIndex, length, new SourcePosition(startLine, startCol), new SourcePosition(endLine, endCol));
        }
    }
    //
    internal sealed class ParsingException : Exception {
        internal ParsingException(string message, SourceSpan sourceSpan)
            : base(message) {
            if (sourceSpan == null) throw new ArgumentNullException("sourceSpan");
            SourceSpan = sourceSpan;
        }
        internal readonly SourceSpan SourceSpan;
        //
        internal static void Throw(BaseRecognizer br, string[] tokenNames, RecognitionException e) {
            SourceSpan sourceSpan;
            var token = e.Token as CommonTokenEx;
            if (token != null) sourceSpan = token.SourceSpan;
            else//lexer exception
                sourceSpan = Extensions.CreateSourceSpan(e.Input.SourceName, e.Index, e.Line, e.CharPositionInLine, null);
            throw new ParsingException(br.GetErrorMessage(e, tokenNames), sourceSpan);
        }
    }
    internal sealed class CommonTokenEx : CommonToken {
        internal CommonTokenEx(ICharStream input, int type, int channel, int start, int stop)
            : base(input, type, channel, start, stop) { }
        private SourceSpan _sourceSpan;
        internal SourceSpan SourceSpan {
            get {
                return _sourceSpan ?? (_sourceSpan = Extensions.CreateSourceSpan(InputStream.SourceName, StartIndex, Line, CharPositionInLine, Type == CharStreamConstants.EndOfFile ? null : Text));
            }
        }
        internal Node Node;// { get; set; }
        internal Node CloneNode(Node kindNode = null, Node textNode = null, SourceSpan sourceSpan = null, string label = null) {
            return Node.Struct(label ?? Node.Label, sourceSpan ?? Node.SourceSpan,
                new KeyValuePair<string, Node>(NodeExtensions.TokenKindName, kindNode ?? Node.Member(NodeExtensions.TokenKindName)),
                new KeyValuePair<string, Node>(NodeExtensions.TokenTextName, textNode ?? Node.Member(NodeExtensions.TokenTextName)));
        }
    }

    internal abstract class CSLexer : Antlr.Runtime.Lexer {
        protected CSLexer(ICharStream input, RecognizerSharedState state) : base(input, state) { }
        protected CSLexer() { }
        public override sealed void Reset() {
            base.Reset();
            if (_ppSymbolSet != null) _ppSymbolSet.Clear();
            if (_ppExpressionStack != null) _ppExpressionStack.Clear();
            if (_ppConditionStack != null) _ppConditionStack.Clear();
            if (_ppRegionStack != null) _ppRegionStack.Clear();
            _tokenQueue.Clear();
        }
        //
        private HashSet<string> _ppSymbolSet;
        private HashSet<string> PpSymbolSet { get { return _ppSymbolSet ?? (_ppSymbolSet = new HashSet<string>()); } }
        protected internal void AddPpSymbol(string symbol) { PpSymbolSet.Add(symbol); }
        protected internal void AddPpSymbols(IEnumerable<string> symbols) {
            foreach (var symbol in symbols) PpSymbolSet.Add(symbol);
        }
        protected internal void RemovePpSymbol(string symbol) { PpSymbolSet.Remove(symbol); }
        //
        protected enum PpExpressionKind { Or, And, Equal, NotEqual, Not, Primary }
        private Stack<bool> _ppExpressionStack;
        private Stack<bool> PpExpressionStack { get { return _ppExpressionStack ?? (_ppExpressionStack = new Stack<bool>()); } }
        protected void SetPpExpression(PpExpressionKind kind, string textValue = null) {
            var value = false;
            switch (kind) {
                case PpExpressionKind.Or:
                case PpExpressionKind.And:
                case PpExpressionKind.Equal:
                case PpExpressionKind.NotEqual:
                    {
                        var rightValue = PpExpressionStack.Pop();
                        var leftValue = PpExpressionStack.Pop();
                        switch (kind) {
                            case PpExpressionKind.Or: value = leftValue || rightValue; break;
                            case PpExpressionKind.And: value = leftValue && rightValue; break;
                            case PpExpressionKind.Equal: value = leftValue == rightValue; break;
                            case PpExpressionKind.NotEqual: value = leftValue != rightValue; break;
                        }
                    }
                    break;
                case PpExpressionKind.Not: value = !PpExpressionStack.Pop(); break;
                case PpExpressionKind.Primary:
                    if (textValue == "true") value = true;
                    else if (textValue == "false") value = false;
                    else value = PpSymbolSet.Contains(textValue);
                    break;
            }
            PpExpressionStack.Push(value);
        }
        protected bool GetPpExpression() { return PpExpressionStack.Pop(); }
        //
        protected enum PpConditionKind { If, Elif, Else, Endif }
        private struct PpCondition {
            internal PpCondition(PpConditionKind kind, bool value) { Kind = kind; Value = value; }
            internal readonly PpConditionKind Kind;
            internal readonly bool Value;
        }
        private Stack<PpCondition> _ppConditionStack;
        private Stack<PpCondition> PpConditionStack { get { return _ppConditionStack ?? (_ppConditionStack = new Stack<PpCondition>()); } }
        protected internal bool HasPpCondition { get { return _ppConditionStack != null && _ppConditionStack.Count > 0; } }
        protected void SetPpCondition(PpConditionKind kind, bool value, int tokenIndex, int tokenLine, int tokenCol) {
            switch (kind) {
                case PpConditionKind.If: break;
                case PpConditionKind.Elif:
                    if (PpConditionStack.Count == 0 || PpConditionStack.Peek().Kind == PpConditionKind.Else)
                        throw new ParsingException("Unexpected #elif", CreateSorceSpan(tokenIndex, tokenLine, tokenCol, "elif"));
                    PpConditionStack.Pop();
                    break;
                case PpConditionKind.Else:
                    if (PpConditionStack.Count == 0 || PpConditionStack.Peek().Kind == PpConditionKind.Else)
                        throw new ParsingException("Unexpected #else", CreateSorceSpan(tokenIndex, tokenLine, tokenCol, "else"));
                    value = !PpConditionStack.Pop().Value;
                    break;
                case PpConditionKind.Endif:
                    if (PpConditionStack.Count == 0)
                        throw new ParsingException("Unexpected #endif", CreateSorceSpan(tokenIndex, tokenLine, tokenCol, "endif"));
                    PpConditionStack.Pop();
                    return;
            }
            if (PpConditionStack.Count > 0) value = PpConditionStack.Peek().Value && value;
            PpConditionStack.Push(new PpCondition(kind, value));
        }
        //
        private Stack<object> _ppRegionStack;
        private Stack<object> PpRegionStack { get { return _ppRegionStack ?? (_ppRegionStack = new Stack<object>()); } }
        protected internal bool HasPpRegion { get { return _ppRegionStack != null && _ppRegionStack.Count > 0; } }
        protected void SetPpRegion(bool isRegion, int tokenIndex, int tokenLine, int tokenCol) {
            if (isRegion) PpRegionStack.Push(null);
            else {
                if (PpRegionStack.Count == 0)
                    throw new ParsingException("Unexpected #endregion", CreateSorceSpan(tokenIndex, tokenLine, tokenCol, "endregion"));
                PpRegionStack.Pop();
            }
        }
        private SourceSpan CreateSorceSpan(int tokenIndex, int tokenLine, int tokenCol, string text) {
            return Extensions.CreateSourceSpan(this.SourceName, tokenIndex, tokenLine, tokenCol, text);
        }
        //
        protected override sealed void ParseNextToken() {
            base.ParseNextToken();
            if (HasPpCondition && !PpConditionStack.Peek().Value)
                state.token = Antlr.Runtime.Tokens.Skip;
        }
        private readonly Queue<CommonTokenEx> _tokenQueue = new Queue<CommonTokenEx>();
        public override sealed IToken NextToken() {
            if (_tokenQueue.Count > 0) return _tokenQueue.Dequeue();
            var token = base.NextToken();
            //if (token.Type == CharStreamConstants.EndOfFile) token = new CommonTokenEx(token);
            if (_tokenQueue.Count > 0) return _tokenQueue.Dequeue();
            return token;
        }
        public override sealed IToken GetEndOfFileToken() {
            var eof = new CommonTokenEx(input, CharStreamConstants.EndOfFile, TokenChannels.Default, input.Index, input.Index);
            eof.Line = Line;
            eof.CharPositionInLine = CharPositionInLine;
            return eof;
        }
        public override sealed IToken Emit() {
            var tokenEx = new CommonTokenEx(input, state.type, state.channel, state.tokenStartCharIndex, CharIndex - 1);
            tokenEx.Line = state.tokenStartLine;
            tokenEx.CharPositionInLine = state.tokenStartCharPositionInLine;
            tokenEx.Text = state.text;
            return EmitCore(tokenEx, false);
        }
        protected IToken EmitCore(CommonTokenEx tokenEx, bool enqueue) {
            tokenEx.Text = tokenEx.Text;
            string label;
            var kindNode = GetKindNode(tokenEx.Type, out label);
            tokenEx.Node = Node.Struct(label, tokenEx.SourceSpan, new KeyValuePair<string, Node>(NodeExtensions.TokenKindName, kindNode),
                new KeyValuePair<string, Node>(NodeExtensions.TokenTextName, Node.Atom(tokenEx.Text)));
            state.token = tokenEx;
            if (enqueue) _tokenQueue.Enqueue(tokenEx);
            return tokenEx;
        }
        protected abstract Node GetKindNode(int type, out string label);
        public override sealed void DisplayRecognitionError(string[] tokenNames, RecognitionException e) {
            ParsingException.Throw(this, tokenNames, e);
        }

    }
    internal abstract class CSParser : Antlr.Runtime.Parser {
        protected CSParser(ITokenStream input, RecognizerSharedState state) : base(input, state) { }
        //
        protected CommonTokenEx _start() { return base.input.LT(1) as CommonTokenEx; }
        private SourceSpan _full(CommonTokenEx startToken) {
            if (startToken == null) return null;
            var endToken = base.input.LT(-1) as CommonTokenEx;
            if (endToken == null) return startToken.SourceSpan;
            if (endToken.TokenIndex < startToken.TokenIndex) throw new InvalidOperationException();// return null;
            return startToken.SourceSpan.MergeWith(endToken.SourceSpan);
        }
        protected Node _box(string label, CommonTokenEx startToken, Node node) {
            return Node.Box(label, _full(startToken), node);
        }
        protected Node _box(string label, CommonTokenEx startToken, CommonTokenEx token) {
            return _box(label, startToken, token != null ? token.Node : null);
        }
        protected Node _struct(string label, CommonTokenEx startToken, params KeyValuePair<string, Node>[] members) {
            return Node.Struct(label, _full(startToken), members);
        }
        protected static KeyValuePair<string, Node> _member(string name, Node node) {
            return new KeyValuePair<string, Node>(name, node);
        }
        protected static KeyValuePair<string, Node> _member(string name, CommonTokenEx token) {
            return _member(name, token != null ? token.Node : null);
        }
        protected static KeyValuePair<string, Node> _cskindmember(Node node) {
            return _member("Kind", node);
        }
        protected Node _list(CommonTokenEx startToken, NodeList list, string label = null) {
            return Node.List(list != null ? _full(startToken) : null, list, label);
        }
        protected static Node _merge(CommonTokenEx t1, CommonTokenEx t2, Node resultKindNode, string errorMessage) {
            var t1SourceSpan = t1.Node.SourceSpan;
            var t2SourceSpan = t2.Node.SourceSpan;
            if (!t1SourceSpan.IsContiguousWith(t2SourceSpan)) throw new ParsingException(errorMessage, t2SourceSpan);
            return t1.CloneNode(resultKindNode, Node.Atom(t1.Text + t2.Text), t1SourceSpan.MergeWith(t2SourceSpan));
        }
        protected struct TokenKindPair {
            internal TokenKindPair(Node token, Node kind) { Token = token; Kind = kind; }
            internal readonly Node Token;
            internal readonly Node Kind;
        }
        //
        public override sealed void DisplayRecognitionError(string[] tokenNames, RecognitionException e) {
            ParsingException.Throw(this, tokenNames, e);
        }
        public override string GetErrorMessage(RecognitionException e, string[] tokenNames) {
            var fpe = e as FailedPredicateException;
            if (fpe != null) {
                var text = fpe.PredicateText;
                var startIdx = text.IndexOf('"');
                int endIdx = -1;
                if (startIdx != -1 && startIdx < text.Length - 1)
                    endIdx = text.IndexOf('"', startIdx + 1);
                if (startIdx != -1 && endIdx != -1)
                    return text.Substring(startIdx, endIdx - startIdx + 1) + " expected";
            }
            return base.GetErrorMessage(e, tokenNames);
        }
        protected void CheckIsEndOfFile() {
            var nextToken = base.input.LT(1);
            if (nextToken.Type != CharStreamConstants.EndOfFile)
                throw new ParsingException("Unexpected input. Make sure the code is compatible with the grammar", ((CommonTokenEx)nextToken).SourceSpan);
            var csLexer = (CSLexer)base.input.TokenSource;
            if (csLexer.HasPpCondition)
                throw new ParsingException("#endif expected", ((CommonTokenEx)nextToken).SourceSpan);
            if (csLexer.HasPpRegion)
                throw new ParsingException("#endregion expected", ((CommonTokenEx)nextToken).SourceSpan);
        }

        #region C# TokenDisplayNameMap
        protected static readonly Dictionary<string, string> CSTokenDisplayNameMap = new Dictionary<string, string>
        {
            {"IdentifierToken", "identifier"},
            {"NumericLiteralToken", "numeric literal"},
            {"CharacterLiteralToken", "character literal"},
            {"StringLiteralToken", "string literal"},
            //
            {"TildeToken", "~"},
            {"ExclamationToken", "!"},
            {"DollarToken", "$"},
            {"PercentToken", "%"},
            {"CaretToken", "^"},
            {"AmpersandToken", "&"},
            {"AsteriskToken", "*"},
            {"OpenParenToken", "("},
            {"CloseParenToken", ")"},
            {"MinusToken", "-"},
            {"PlusToken", "+"},
            {"EqualsToken", "="},
            {"OpenBraceToken", "{"},
            {"CloseBraceToken", "}"},
            {"OpenBracketToken", "["},
            {"CloseBracketToken", "]"},
            {"BarToken", "|"},
            {"BackslashToken", "\\"},
            {"ColonToken", ":"},
            {"SemicolonToken", ";"},
            {"DoubleQuoteToken", "\""},
            {"SingleQuoteToken", "'"},
            {"LessThanToken", "<"},
            {"CommaToken", ","},
            {"GreaterThanToken", ">"},
            {"DotToken", "."},
            {"QuestionToken", "?"},
            {"HashToken", "#"},
            {"SlashToken", "/"},
            //{"SlashGreaterThanToken", "/>"},
            //{"LessThanSlashToken", "</"},
            //{"XmlCommentStartToken", "<!--"},
            //{"XmlCommentEndToken", "-->"},
            //{"XmlCDataStartToken", "<![CDATA["},
            //{"XmlCDataEndToken", "]]>"},
            //{"XmlProcessingInstructionStartToken", "<?"},
            //{"XmlProcessingInstructionEndToken", "?>"},
            {"BarBarToken", "||"},
            {"AmpersandAmpersandToken", "&&"},
            {"MinusMinusToken", "--"},
            {"PlusPlusToken", "++"},
            {"ColonColonToken", "::"},
            {"QuestionQuestionToken", "??"},
            {"MinusGreaterThanToken", "->"},
            {"ExclamationEqualsToken", "!="},
            {"EqualsEqualsToken", "=="},
            {"EqualsGreaterThanToken", "=>"},
            {"LessThanEqualsToken", "<="},
            {"LessThanLessThanToken", "<<"},
            {"LessThanLessThanEqualsToken", "<<="},
            {"GreaterThanEqualsToken", ">="},
            {"GreaterThanGreaterThanToken", ">>"},
            {"GreaterThanGreaterThanEqualsToken", ">>="},
            {"SlashEqualsToken", "/="},
            {"AsteriskEqualsToken", "*="},
            {"BarEqualsToken", "|="},
            {"AmpersandEqualsToken", "&="},
            {"PlusEqualsToken", "+="},
            {"MinusEqualsToken", "-="},
            {"CaretEqualsToken", "^="},
            {"PercentEqualsToken", "%="},
            {"BoolKeyword", "bool"},
            {"ByteKeyword", "byte"},
            {"SByteKeyword", "sbyte"},
            {"ShortKeyword", "short"},
            {"UShortKeyword", "ushort"},
            {"IntKeyword", "int"},
            {"UIntKeyword", "uint"},
            {"LongKeyword", "long"},
            {"ULongKeyword", "ulong"},
            {"DoubleKeyword", "double"},
            {"FloatKeyword", "float"},
            {"DecimalKeyword", "decimal"},
            {"StringKeyword", "string"},
            {"CharKeyword", "char"},
            {"VoidKeyword", "void"},
            {"ObjectKeyword", "object"},
            {"TypeOfKeyword", "typeof"},
            {"SizeOfKeyword", "sizeof"},
            {"NullKeyword", "null"},
            {"TrueKeyword", "true"},
            {"FalseKeyword", "false"},
            {"IfKeyword", "if"},
            {"ElseKeyword", "else"},
            {"WhileKeyword", "while"},
            {"ForKeyword", "for"},
            {"ForEachKeyword", "foreach"},
            {"DoKeyword", "do"},
            {"SwitchKeyword", "switch"},
            {"CaseKeyword", "case"},
            {"DefaultKeyword", "default"},
            {"TryKeyword", "try"},
            {"CatchKeyword", "catch"},
            {"FinallyKeyword", "finally"},
            {"LockKeyword", "lock"},
            {"GotoKeyword", "goto"},
            {"BreakKeyword", "break"},
            {"ContinueKeyword", "continue"},
            {"ReturnKeyword", "return"},
            {"ThrowKeyword", "throw"},
            {"PublicKeyword", "public"},
            {"PrivateKeyword", "private"},
            {"InternalKeyword", "internal"},
            {"ProtectedKeyword", "protected"},
            {"StaticKeyword", "static"},
            {"ReadOnlyKeyword", "readonly"},
            {"SealedKeyword", "sealed"},
            {"ConstKeyword", "const"},
            {"FixedKeyword", "fixed"},
            {"StackAllocKeyword", "stackalloc"},
            {"VolatileKeyword", "volatile"},
            {"NewKeyword", "new"},
            {"OverrideKeyword", "override"},
            {"AbstractKeyword", "abstract"},
            {"VirtualKeyword", "virtual"},
            {"EventKeyword", "event"},
            {"ExternKeyword", "extern"},
            {"RefKeyword", "ref"},
            {"OutKeyword", "out"},
            {"InKeyword", "in"},
            {"IsKeyword", "is"},
            {"AsKeyword", "as"},
            {"ParamsKeyword", "params"},
            {"ArgListKeyword", "__arglist"},
            {"MakeRefKeyword", "__makeref"},
            {"RefTypeKeyword", "__reftype"},
            {"RefValueKeyword", "__refvalue"},
            {"ThisKeyword", "this"},
            {"BaseKeyword", "base"},
            {"NamespaceKeyword", "namespace"},
            {"UsingKeyword", "using"},
            {"ClassKeyword", "class"},
            {"StructKeyword", "struct"},
            {"InterfaceKeyword", "interface"},
            {"EnumKeyword", "enum"},
            {"DelegateKeyword", "delegate"},
            {"CheckedKeyword", "checked"},
            {"UncheckedKeyword", "unchecked"},
            {"UnsafeKeyword", "unsafe"},
            {"OperatorKeyword", "operator"},
            {"ExplicitKeyword", "explicit"},
            {"ImplicitKeyword", "implicit"},
        };
        #endregion
    }
    //
    //
    //X
    //
    //
    partial class XLexer {
        protected override Node GetKindNode(int type, out string label) {
            Node kindNode;
            if (CSTokenKindMap.TryGetValue(type, out kindNode)) label = NodeExtensions.CSTokenLabel;
            else if (XTokenKindMap.TryGetValue(type, out kindNode)) label = NodeExtensions.XTokenLabel;
            else throw new InvalidOperationException("Invalid Antlr token type: " + type);
            return kindNode;
        }
        #region C# TokenKindMap
        //
        //Antlr无法由用户定义token的Type值,所以无法将其移到CSLexer中
        //
        internal static readonly Dictionary<int, Node> CSTokenKindMap = new Dictionary<int, Node>
        {
            {IdentifierToken, CSTokens.IdentifierTokenKind},
            {NumericLiteralToken, CSTokens.NumericLiteralTokenKind},
            {CharacterLiteralToken, CSTokens.CharacterLiteralTokenKind},
            {StringLiteralToken, CSTokens.StringLiteralTokenKind},
            //
            {TildeToken, CSTokens.TildeTokenKind},
            {ExclamationToken, CSTokens.ExclamationTokenKind},
            {DollarToken, CSTokens.DollarTokenKind},
            {PercentToken, CSTokens.PercentTokenKind},
            {CaretToken, CSTokens.CaretTokenKind},
            {AmpersandToken, CSTokens.AmpersandTokenKind},
            {AsteriskToken, CSTokens.AsteriskTokenKind},
            {OpenParenToken, CSTokens.OpenParenTokenKind},
            {CloseParenToken, CSTokens.CloseParenTokenKind},
            {MinusToken, CSTokens.MinusTokenKind},
            {PlusToken, CSTokens.PlusTokenKind},
            {EqualsToken, CSTokens.EqualsTokenKind},
            {OpenBraceToken, CSTokens.OpenBraceTokenKind},
            {CloseBraceToken, CSTokens.CloseBraceTokenKind},
            {OpenBracketToken, CSTokens.OpenBracketTokenKind},
            {CloseBracketToken, CSTokens.CloseBracketTokenKind},
            {BarToken, CSTokens.BarTokenKind},
            {BackslashToken, CSTokens.BackslashTokenKind},
            {ColonToken, CSTokens.ColonTokenKind},
            {SemicolonToken, CSTokens.SemicolonTokenKind},
            {DoubleQuoteToken, CSTokens.DoubleQuoteTokenKind},
            {SingleQuoteToken, CSTokens.SingleQuoteTokenKind},
            {LessThanToken, CSTokens.LessThanTokenKind},
            {CommaToken, CSTokens.CommaTokenKind},
            {GreaterThanToken, CSTokens.GreaterThanTokenKind},
            {DotToken, CSTokens.DotTokenKind},
            {QuestionToken, CSTokens.QuestionTokenKind},
            {HashToken, CSTokens.HashTokenKind},
            {SlashToken, CSTokens.SlashTokenKind},
            //{SlashGreaterThanToken, SlashGreaterThanTokenKind},
            //{LessThanSlashToken, LessThanSlashTokenKind},
            //{XmlCommentStartToken, XmlCommentStartTokenKind},
            //{XmlCommentEndToken, XmlCommentEndTokenKind},
            //{XmlCDataStartToken, XmlCDataStartTokenKind},
            //{XmlCDataEndToken, XmlCDataEndTokenKind},
            //{XmlProcessingInstructionStartToken, XmlProcessingInstructionStartTokenKind},
            //{XmlProcessingInstructionEndToken, XmlProcessingInstructionEndTokenKind},
            {BarBarToken, CSTokens.BarBarTokenKind},
            {AmpersandAmpersandToken, CSTokens.AmpersandAmpersandTokenKind},
            {MinusMinusToken, CSTokens.MinusMinusTokenKind},
            {PlusPlusToken, CSTokens.PlusPlusTokenKind},
            {ColonColonToken, CSTokens.ColonColonTokenKind},
            {QuestionQuestionToken, CSTokens.QuestionQuestionTokenKind},
            {MinusGreaterThanToken, CSTokens.MinusGreaterThanTokenKind},
            {ExclamationEqualsToken, CSTokens.ExclamationEqualsTokenKind},
            {EqualsEqualsToken, CSTokens.EqualsEqualsTokenKind},
            {EqualsGreaterThanToken, CSTokens.EqualsGreaterThanTokenKind},
            {LessThanEqualsToken, CSTokens.LessThanEqualsTokenKind},
            {LessThanLessThanToken, CSTokens.LessThanLessThanTokenKind},
            {LessThanLessThanEqualsToken, CSTokens.LessThanLessThanEqualsTokenKind},
            {GreaterThanEqualsToken, CSTokens.GreaterThanEqualsTokenKind},
            //{GreaterThanGreaterThanToken, GreaterThanGreaterThanTokenKind},
            //{GreaterThanGreaterThanEqualsToken, GreaterThanGreaterThanEqualsTokenKind},
            {SlashEqualsToken, CSTokens.SlashEqualsTokenKind},
            {AsteriskEqualsToken, CSTokens.AsteriskEqualsTokenKind},
            {BarEqualsToken, CSTokens.BarEqualsTokenKind},
            {AmpersandEqualsToken, CSTokens.AmpersandEqualsTokenKind},
            {PlusEqualsToken, CSTokens.PlusEqualsTokenKind},
            {MinusEqualsToken, CSTokens.MinusEqualsTokenKind},
            {CaretEqualsToken, CSTokens.CaretEqualsTokenKind},
            {PercentEqualsToken, CSTokens.PercentEqualsTokenKind},
            {BoolKeyword, CSTokens.BoolKeywordKind},
            {ByteKeyword, CSTokens.ByteKeywordKind},
            {SByteKeyword, CSTokens.SByteKeywordKind},
            {ShortKeyword, CSTokens.ShortKeywordKind},
            {UShortKeyword, CSTokens.UShortKeywordKind},
            {IntKeyword, CSTokens.IntKeywordKind},
            {UIntKeyword, CSTokens.UIntKeywordKind},
            {LongKeyword, CSTokens.LongKeywordKind},
            {ULongKeyword, CSTokens.ULongKeywordKind},
            {DoubleKeyword, CSTokens.DoubleKeywordKind},
            {FloatKeyword, CSTokens.FloatKeywordKind},
            {DecimalKeyword, CSTokens.DecimalKeywordKind},
            {StringKeyword, CSTokens.StringKeywordKind},
            {CharKeyword, CSTokens.CharKeywordKind},
            {VoidKeyword, CSTokens.VoidKeywordKind},
            {ObjectKeyword, CSTokens.ObjectKeywordKind},
            {TypeOfKeyword, CSTokens.TypeOfKeywordKind},
            {SizeOfKeyword, CSTokens.SizeOfKeywordKind},
            {NullKeyword, CSTokens.NullKeywordKind},
            {TrueKeyword, CSTokens.TrueKeywordKind},
            {FalseKeyword, CSTokens.FalseKeywordKind},
            {IfKeyword, CSTokens.IfKeywordKind},
            {ElseKeyword, CSTokens.ElseKeywordKind},
            {WhileKeyword, CSTokens.WhileKeywordKind},
            {ForKeyword, CSTokens.ForKeywordKind},
            {ForEachKeyword, CSTokens.ForEachKeywordKind},
            {DoKeyword, CSTokens.DoKeywordKind},
            {SwitchKeyword, CSTokens.SwitchKeywordKind},
            {CaseKeyword, CSTokens.CaseKeywordKind},
            {DefaultKeyword, CSTokens.DefaultKeywordKind},
            {TryKeyword, CSTokens.TryKeywordKind},
            {CatchKeyword, CSTokens.CatchKeywordKind},
            {FinallyKeyword, CSTokens.FinallyKeywordKind},
            {LockKeyword, CSTokens.LockKeywordKind},
            {GotoKeyword, CSTokens.GotoKeywordKind},
            {BreakKeyword, CSTokens.BreakKeywordKind},
            {ContinueKeyword, CSTokens.ContinueKeywordKind},
            {ReturnKeyword, CSTokens.ReturnKeywordKind},
            {ThrowKeyword, CSTokens.ThrowKeywordKind},
            {PublicKeyword, CSTokens.PublicKeywordKind},
            {PrivateKeyword, CSTokens.PrivateKeywordKind},
            {InternalKeyword, CSTokens.InternalKeywordKind},
            {ProtectedKeyword, CSTokens.ProtectedKeywordKind},
            {StaticKeyword, CSTokens.StaticKeywordKind},
            {ReadOnlyKeyword, CSTokens.ReadOnlyKeywordKind},
            {SealedKeyword, CSTokens.SealedKeywordKind},
            {ConstKeyword, CSTokens.ConstKeywordKind},
            {FixedKeyword, CSTokens.FixedKeywordKind},
            {StackAllocKeyword, CSTokens.StackAllocKeywordKind},
            {VolatileKeyword, CSTokens.VolatileKeywordKind},
            {NewKeyword, CSTokens.NewKeywordKind},
            {OverrideKeyword, CSTokens.OverrideKeywordKind},
            {AbstractKeyword, CSTokens.AbstractKeywordKind},
            {VirtualKeyword, CSTokens.VirtualKeywordKind},
            {EventKeyword, CSTokens.EventKeywordKind},
            {ExternKeyword, CSTokens.ExternKeywordKind},
            {RefKeyword, CSTokens.RefKeywordKind},
            {OutKeyword, CSTokens.OutKeywordKind},
            {InKeyword, CSTokens.InKeywordKind},
            {IsKeyword, CSTokens.IsKeywordKind},
            {AsKeyword, CSTokens.AsKeywordKind},
            {ParamsKeyword, CSTokens.ParamsKeywordKind},
            {ArgListKeyword, CSTokens.ArgListKeywordKind},
            {MakeRefKeyword, CSTokens.MakeRefKeywordKind},
            {RefTypeKeyword, CSTokens.RefTypeKeywordKind},
            {RefValueKeyword, CSTokens.RefValueKeywordKind},
            {ThisKeyword, CSTokens.ThisKeywordKind},
            {BaseKeyword, CSTokens.BaseKeywordKind},
            {NamespaceKeyword, CSTokens.NamespaceKeywordKind},
            {UsingKeyword, CSTokens.UsingKeywordKind},
            {ClassKeyword, CSTokens.ClassKeywordKind},
            {StructKeyword, CSTokens.StructKeywordKind},
            {InterfaceKeyword, CSTokens.InterfaceKeywordKind},
            {EnumKeyword, CSTokens.EnumKeywordKind},
            {DelegateKeyword, CSTokens.DelegateKeywordKind},
            {CheckedKeyword, CSTokens.CheckedKeywordKind},
            {UncheckedKeyword, CSTokens.UncheckedKeywordKind},
            {UnsafeKeyword, CSTokens.UnsafeKeywordKind},
            {OperatorKeyword, CSTokens.OperatorKeywordKind},
            {ExplicitKeyword, CSTokens.ExplicitKeywordKind},
            {ImplicitKeyword, CSTokens.ImplicitKeywordKind},

        };
        #endregion
        #region X TokenKindMap
        internal static readonly Dictionary<int, Node> XTokenKindMap = new Dictionary<int, Node> {
            {AliasKeyword, XTokens.AliasKeywordKind},
            {AttributeKeyword, XTokens.AttributeKeywordKind},
            {AttributesKeyword, XTokens.AttributesKeywordKind},
            {ChoiceKeyword, XTokens.ChoiceKeywordKind},
            {ElementKeyword, XTokens.ElementKeywordKind},
            {ImportKeyword, XTokens.ImportKeywordKind},
            {SeqKeyword, XTokens.SeqKeywordKind},
            {TypeKeyword, XTokens.TypeKeywordKind},
            {UnorderedKeyword, XTokens.UnorderedKeywordKind},
            {XNamespaceKeyword, XTokens.XNamespaceKeywordKind},
            {DotDotToken, XTokens.DotDotTokenKind},
            {HashHashToken, XTokens.HashHashTokenKind},
            {AsteriskAsteriskToken, XTokens.AsteriskAsteriskTokenKind},
            {AtToken, XTokens.AtTokenKind},
        };
        #endregion
    }
    partial class XParser {
        internal Node Main() {
            var node = x_compilation_unit();
            CheckIsEndOfFile();
            return node;
        }
    }
    internal sealed class XParserEx : XParser {
        internal XParserEx(ITokenStream input) : base(input) { }
        internal XParserEx(ITokenStream input, RecognizerSharedState state) : base(input, state) { }
        //
        private static volatile string[] _tokenDisplayNames;
        private static string[] TokenDisplayNames {
            get {
                if (_tokenDisplayNames == null) {
                    var count = XParser.tokenNames.Length;
                    var tokenDisplayNames = new string[count];
                    for (var i = 0; i < count; i++) {
                        var name = XParser.tokenNames[i];
                        string tokenDisplayName;
                        if (!CSTokenDisplayNameMap.TryGetValue(name, out tokenDisplayName))
                            if (!XTokenDisplayNameMap.TryGetValue(name, out tokenDisplayName))
                                tokenDisplayName = name;
                        tokenDisplayNames[i] = tokenDisplayName;
                    }
                    _tokenDisplayNames = tokenDisplayNames;
                }
                return _tokenDisplayNames;
            }
        }
        public override string[] TokenNames { get { return TokenDisplayNames; } }

        #region X TokenDisplayNameMap
        private static readonly Dictionary<string, string> XTokenDisplayNameMap = new Dictionary<string, string> {
            {"AliasKeyword", @"alias"},
            {"AttributeKeyword", @"attribute"},
            {"AttributesKeyword", @"attributes"},
            {"ChoiceKeyword", @"choice"},
            {"ElementKeyword", @"element"},
            {"ImportKeyword", @"import"},
            {"SeqKeyword", @"seq"},
            {"TypeKeyword", @"type"},
            {"UnorderedKeyword", @"unordered"},
            {"XNamespaceKeyword", @"xnamespace"},
            {"DotDotToken", @".."},
            {"HashHashToken", @"##"},
            {"AsteriskAsteriskToken", @"**"},
            {"AtToken", @"@"},
        };
        #endregion
    }
    //
    //
    //W
    //
    //
#if true
    partial class WLexer {
        protected override Node GetKindNode(int type, out string label) {
            Node kindNode;
            if (CSTokenKindMap.TryGetValue(type, out kindNode)) label = NodeExtensions.CSTokenLabel;
            else if (WTokenKindMap.TryGetValue(type, out kindNode)) label = NodeExtensions.WTokenLabel;
            else throw new InvalidOperationException("Invalid Antlr token type: " + type);
            return kindNode;
        }
    #region C# TokenKindMap
        //
        //Antlr无法由用户定义token的Type值,所以无法将其移到CSLexer中
        //
        internal static readonly Dictionary<int, Node> CSTokenKindMap = new Dictionary<int, Node>
        {
            {IdentifierToken, CSTokens.IdentifierTokenKind},
            {NumericLiteralToken, CSTokens.NumericLiteralTokenKind},
            {CharacterLiteralToken, CSTokens.CharacterLiteralTokenKind},
            {StringLiteralToken, CSTokens.StringLiteralTokenKind},
            //
            {TildeToken, CSTokens.TildeTokenKind},
            {ExclamationToken, CSTokens.ExclamationTokenKind},
            {DollarToken, CSTokens.DollarTokenKind},
            {PercentToken, CSTokens.PercentTokenKind},
            {CaretToken, CSTokens.CaretTokenKind},
            {AmpersandToken, CSTokens.AmpersandTokenKind},
            {AsteriskToken, CSTokens.AsteriskTokenKind},
            {OpenParenToken, CSTokens.OpenParenTokenKind},
            {CloseParenToken, CSTokens.CloseParenTokenKind},
            {MinusToken, CSTokens.MinusTokenKind},
            {PlusToken, CSTokens.PlusTokenKind},
            {EqualsToken, CSTokens.EqualsTokenKind},
            {OpenBraceToken, CSTokens.OpenBraceTokenKind},
            {CloseBraceToken, CSTokens.CloseBraceTokenKind},
            {OpenBracketToken, CSTokens.OpenBracketTokenKind},
            {CloseBracketToken, CSTokens.CloseBracketTokenKind},
            {BarToken, CSTokens.BarTokenKind},
            {BackslashToken, CSTokens.BackslashTokenKind},
            {ColonToken, CSTokens.ColonTokenKind},
            {SemicolonToken, CSTokens.SemicolonTokenKind},
            {DoubleQuoteToken, CSTokens.DoubleQuoteTokenKind},
            {SingleQuoteToken, CSTokens.SingleQuoteTokenKind},
            {LessThanToken, CSTokens.LessThanTokenKind},
            {CommaToken, CSTokens.CommaTokenKind},
            {GreaterThanToken, CSTokens.GreaterThanTokenKind},
            {DotToken, CSTokens.DotTokenKind},
            {QuestionToken, CSTokens.QuestionTokenKind},
            {HashToken, CSTokens.HashTokenKind},
            {SlashToken, CSTokens.SlashTokenKind},
            //{SlashGreaterThanToken, SlashGreaterThanTokenKind},
            //{LessThanSlashToken, LessThanSlashTokenKind},
            //{XmlCommentStartToken, XmlCommentStartTokenKind},
            //{XmlCommentEndToken, XmlCommentEndTokenKind},
            //{XmlCDataStartToken, XmlCDataStartTokenKind},
            //{XmlCDataEndToken, XmlCDataEndTokenKind},
            //{XmlProcessingInstructionStartToken, XmlProcessingInstructionStartTokenKind},
            //{XmlProcessingInstructionEndToken, XmlProcessingInstructionEndTokenKind},
            {BarBarToken, CSTokens.BarBarTokenKind},
            {AmpersandAmpersandToken, CSTokens.AmpersandAmpersandTokenKind},
            {MinusMinusToken, CSTokens.MinusMinusTokenKind},
            {PlusPlusToken, CSTokens.PlusPlusTokenKind},
            {ColonColonToken, CSTokens.ColonColonTokenKind},
            {QuestionQuestionToken, CSTokens.QuestionQuestionTokenKind},
            {MinusGreaterThanToken, CSTokens.MinusGreaterThanTokenKind},
            {ExclamationEqualsToken, CSTokens.ExclamationEqualsTokenKind},
            {EqualsEqualsToken, CSTokens.EqualsEqualsTokenKind},
            {EqualsGreaterThanToken, CSTokens.EqualsGreaterThanTokenKind},
            {LessThanEqualsToken, CSTokens.LessThanEqualsTokenKind},
            {LessThanLessThanToken, CSTokens.LessThanLessThanTokenKind},
            {LessThanLessThanEqualsToken, CSTokens.LessThanLessThanEqualsTokenKind},
            {GreaterThanEqualsToken, CSTokens.GreaterThanEqualsTokenKind},
            //{GreaterThanGreaterThanToken, GreaterThanGreaterThanTokenKind},
            //{GreaterThanGreaterThanEqualsToken, GreaterThanGreaterThanEqualsTokenKind},
            {SlashEqualsToken, CSTokens.SlashEqualsTokenKind},
            {AsteriskEqualsToken, CSTokens.AsteriskEqualsTokenKind},
            {BarEqualsToken, CSTokens.BarEqualsTokenKind},
            {AmpersandEqualsToken, CSTokens.AmpersandEqualsTokenKind},
            {PlusEqualsToken, CSTokens.PlusEqualsTokenKind},
            {MinusEqualsToken, CSTokens.MinusEqualsTokenKind},
            {CaretEqualsToken, CSTokens.CaretEqualsTokenKind},
            {PercentEqualsToken, CSTokens.PercentEqualsTokenKind},
            {BoolKeyword, CSTokens.BoolKeywordKind},
            {ByteKeyword, CSTokens.ByteKeywordKind},
            {SByteKeyword, CSTokens.SByteKeywordKind},
            {ShortKeyword, CSTokens.ShortKeywordKind},
            {UShortKeyword, CSTokens.UShortKeywordKind},
            {IntKeyword, CSTokens.IntKeywordKind},
            {UIntKeyword, CSTokens.UIntKeywordKind},
            {LongKeyword, CSTokens.LongKeywordKind},
            {ULongKeyword, CSTokens.ULongKeywordKind},
            {DoubleKeyword, CSTokens.DoubleKeywordKind},
            {FloatKeyword, CSTokens.FloatKeywordKind},
            {DecimalKeyword, CSTokens.DecimalKeywordKind},
            {StringKeyword, CSTokens.StringKeywordKind},
            {CharKeyword, CSTokens.CharKeywordKind},
            {VoidKeyword, CSTokens.VoidKeywordKind},
            {ObjectKeyword, CSTokens.ObjectKeywordKind},
            {TypeOfKeyword, CSTokens.TypeOfKeywordKind},
            {SizeOfKeyword, CSTokens.SizeOfKeywordKind},
            {NullKeyword, CSTokens.NullKeywordKind},
            {TrueKeyword, CSTokens.TrueKeywordKind},
            {FalseKeyword, CSTokens.FalseKeywordKind},
            {IfKeyword, CSTokens.IfKeywordKind},
            {ElseKeyword, CSTokens.ElseKeywordKind},
            {WhileKeyword, CSTokens.WhileKeywordKind},
            {ForKeyword, CSTokens.ForKeywordKind},
            {ForEachKeyword, CSTokens.ForEachKeywordKind},
            {DoKeyword, CSTokens.DoKeywordKind},
            {SwitchKeyword, CSTokens.SwitchKeywordKind},
            {CaseKeyword, CSTokens.CaseKeywordKind},
            {DefaultKeyword, CSTokens.DefaultKeywordKind},
            {TryKeyword, CSTokens.TryKeywordKind},
            {CatchKeyword, CSTokens.CatchKeywordKind},
            {FinallyKeyword, CSTokens.FinallyKeywordKind},
            {LockKeyword, CSTokens.LockKeywordKind},
            {GotoKeyword, CSTokens.GotoKeywordKind},
            {BreakKeyword, CSTokens.BreakKeywordKind},
            {ContinueKeyword, CSTokens.ContinueKeywordKind},
            {ReturnKeyword, CSTokens.ReturnKeywordKind},
            {ThrowKeyword, CSTokens.ThrowKeywordKind},
            {PublicKeyword, CSTokens.PublicKeywordKind},
            {PrivateKeyword, CSTokens.PrivateKeywordKind},
            {InternalKeyword, CSTokens.InternalKeywordKind},
            {ProtectedKeyword, CSTokens.ProtectedKeywordKind},
            {StaticKeyword, CSTokens.StaticKeywordKind},
            {ReadOnlyKeyword, CSTokens.ReadOnlyKeywordKind},
            {SealedKeyword, CSTokens.SealedKeywordKind},
            {ConstKeyword, CSTokens.ConstKeywordKind},
            {FixedKeyword, CSTokens.FixedKeywordKind},
            {StackAllocKeyword, CSTokens.StackAllocKeywordKind},
            {VolatileKeyword, CSTokens.VolatileKeywordKind},
            {NewKeyword, CSTokens.NewKeywordKind},
            {OverrideKeyword, CSTokens.OverrideKeywordKind},
            {AbstractKeyword, CSTokens.AbstractKeywordKind},
            {VirtualKeyword, CSTokens.VirtualKeywordKind},
            {EventKeyword, CSTokens.EventKeywordKind},
            {ExternKeyword, CSTokens.ExternKeywordKind},
            {RefKeyword, CSTokens.RefKeywordKind},
            {OutKeyword, CSTokens.OutKeywordKind},
            {InKeyword, CSTokens.InKeywordKind},
            {IsKeyword, CSTokens.IsKeywordKind},
            {AsKeyword, CSTokens.AsKeywordKind},
            {ParamsKeyword, CSTokens.ParamsKeywordKind},
            {ArgListKeyword, CSTokens.ArgListKeywordKind},
            {MakeRefKeyword, CSTokens.MakeRefKeywordKind},
            {RefTypeKeyword, CSTokens.RefTypeKeywordKind},
            {RefValueKeyword, CSTokens.RefValueKeywordKind},
            {ThisKeyword, CSTokens.ThisKeywordKind},
            {BaseKeyword, CSTokens.BaseKeywordKind},
            {NamespaceKeyword, CSTokens.NamespaceKeywordKind},
            {UsingKeyword, CSTokens.UsingKeywordKind},
            {ClassKeyword, CSTokens.ClassKeywordKind},
            {StructKeyword, CSTokens.StructKeywordKind},
            {InterfaceKeyword, CSTokens.InterfaceKeywordKind},
            {EnumKeyword, CSTokens.EnumKeywordKind},
            {DelegateKeyword, CSTokens.DelegateKeywordKind},
            {CheckedKeyword, CSTokens.CheckedKeywordKind},
            {UncheckedKeyword, CSTokens.UncheckedKeywordKind},
            {UnsafeKeyword, CSTokens.UnsafeKeywordKind},
            {OperatorKeyword, CSTokens.OperatorKeywordKind},
            {ExplicitKeyword, CSTokens.ExplicitKeywordKind},
            {ImplicitKeyword, CSTokens.ImplicitKeywordKind},

        };
    #endregion
    #region W TokenKindMap
        internal static readonly Dictionary<int, Node> WTokenKindMap = new Dictionary<int, Node> {
            {ActivityKeyword, WTokens.ActivityKeywordKind},
            {CancellableKeyword, WTokens.CancellableKeywordKind},
            {ConfirmKeyword, WTokens.ConfirmKeywordKind},
            {CompensableKeyword, WTokens.CompensableKeywordKind},
            {CompensateKeyword, WTokens.CompensateKeywordKind},
            {ContentCorrKeyword, WTokens.ContentCorrKeywordKind},
            {DelayKeyword, WTokens.DelayKeywordKind},
            {FlowKeyword, WTokens.FlowKeywordKind},
            {FIfKeyword, WTokens.FIfKeywordKind},
            {FSwitchKeyword, WTokens.FSwitchKeywordKind},
            {ImportKeyword, WTokens.ImportKeywordKind},
            {NoPersistKeyword, WTokens.NoPersistKeywordKind},
            {ParallelKeyword, WTokens.ParallelKeywordKind},
            {PersistKeyword, WTokens.PersistKeywordKind},
            {PForEachKeyword, WTokens.PForEachKeywordKind},
            {PickKeyword, WTokens.PickKeywordKind},
            {ReceiveKeyword, WTokens.ReceiveKeywordKind},
            {ReceiveReplyKeyword, WTokens.ReceiveReplyKeywordKind},
            {SendKeyword, WTokens.SendKeywordKind},
            {SendReplyKeyword, WTokens.SendReplyKeywordKind},
            {StateMachineKeyword, WTokens.StateMachineKeywordKind},
            {TerminateKeyword, WTokens.TerminateKeywordKind},
            {TransactedKeyword, WTokens.TransactedKeywordKind},
            {TransactedReceiveKeyword, WTokens.TransactedReceiveKeywordKind},
            {HashHashToken, WTokens.HashHashTokenKind},
            {TildeGreaterThanToken, WTokens.TildeGreaterThanTokenKind},
            {LessThanTildeToken, WTokens.LessThanTildeTokenKind},
        };
    #endregion
    }
    partial class WParser {
        internal Node Main() {
            var node = w_compilation_unit();
            CheckIsEndOfFile();
            return node;
        }
    }
    internal sealed class WParserEx : WParser {
        internal WParserEx(ITokenStream input) : base(input) { }
        internal WParserEx(ITokenStream input, RecognizerSharedState state) : base(input, state) { }
        //
        private static volatile string[] _tokenDisplayNames;
        private static string[] TokenDisplayNames {
            get {
                if (_tokenDisplayNames == null) {
                    var count = WParser.tokenNames.Length;
                    var tokenDisplayNames = new string[count];
                    for (var i = 0; i < count; i++) {
                        var name = WParser.tokenNames[i];
                        string tokenDisplayName;
                        if (!CSTokenDisplayNameMap.TryGetValue(name, out tokenDisplayName))
                            if (!WTokenDisplayNameMap.TryGetValue(name, out tokenDisplayName))
                                tokenDisplayName = name;
                        tokenDisplayNames[i] = tokenDisplayName;
                    }
                    _tokenDisplayNames = tokenDisplayNames;
                }
                return _tokenDisplayNames;
            }
        }
        public override string[] TokenNames { get { return TokenDisplayNames; } }

    #region W TokenDisplayNameMap
        private static readonly Dictionary<string, string> WTokenDisplayNameMap = new Dictionary<string, string> {
            {"ActivityKeyword", @"activity"},
            {"CancellableKeyword", @"cancellable"},
            {"ConfirmKeyword", @"confirm"},
            {"CompensableKeyword", @"compensable"},
            {"CompensateKeyword", @"compensate"},
            {"ContentCorrKeyword", @"contentcorr"},
            {"DelayKeyword", @"delay"},
            {"FlowKeyword", @"flow"},
            {"FIfKeyword", @"fif"},
            {"FSwitchKeyword", @"fswitch"},
            {"ImportKeyword", @"import"},
            {"NoPersistKeyword", @"nopersist"},
            {"ParallelKeyword", @"parallel"},
            {"PersistKeyword", @"persist"},
            {"PForEachKeyword", @"pforeach"},
            {"PickKeyword", @"pick"},
            {"ReceiveKeyword", @"receive"},
            {"ReceiveReplyKeyword", @"receivereply"},
            {"SendKeyword", @"send"},
            {"SendReplyKeyword", @"sendreply"},
            {"StateMachineKeyword", @"statemachine"},
            {"TerminateKeyword", @"terminate"},
            {"TransactedKeyword", @"transacted"},
            {"TransactedReceiveKeyword", @"transactedreceive"},
            {"HashHashToken", @"##"},
            {"TildeGreaterThanToken", @"~>"},
            {"LessThanTildeToken", @"<~"},
        };
    #endregion
    }
#endif
}
