//#define DumpClassifier
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Shell;
using EnvDTE;
using Metah.Compilation;
using Metah.MSBuild;

namespace Metah.VisualStudio.Editors {
    internal abstract class LanguageClassifierBase : IClassifier {
        protected LanguageClassifierBase(ITextBuffer textBuffer, IStandardClassificationService standardService, IClassificationTypeRegistryService registryService, HashSet<string> keywordSet) {
            _keywordSet = keywordSet;
            _keywordType = standardService.Keyword;
            _commentType = standardService.Comment;
            _stringLiteralType = standardService.StringLiteral;
            //_characterLiteralType = standardService.CharacterLiteral;
            //_typeNameType = standardService.SymbolDefinition;
            //if (textBuffer.ContentType.TypeName == ContentTypeDefinitions.EContentType)
            //    _eKeywordType = registryService.GetClassificationType(ClassificationDefinitions.EKeyword);
            //else _eKeywordType = _keywordType;
            //
            var snapshot = textBuffer.CurrentSnapshot;
            var lineCount = snapshot.LineCount;
            for (var i = 0; i < lineCount; i++) {
                _lineInfoList.Add(new LineInfo(false));
                var snapshotLine = snapshot.GetLineFromLineNumber(i);
                ProcessLine(snapshot, i, snapshotLine.Start.Position, snapshotLine.GetTextIncludingLineBreak(), false);
            }
            //
            textBuffer.Changed += OnTextBufferChanged;
        }
        private readonly HashSet<string> _keywordSet;
        private static readonly HashSet<string> _csKeywordSet = CSTokens.KeywordSet;
        private readonly IClassificationType _keywordType;
        private readonly IClassificationType _commentType;
        private readonly IClassificationType _stringLiteralType;
        //private readonly IClassificationType _characterLiteralType;
        //private readonly IClassificationType _typeNameType;
        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
        private readonly List<LineInfo> _lineInfoList = new List<LineInfo>();
        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e) {
            foreach (var change in e.Changes) {
                var lineNumber = e.After.GetLineNumberFromPosition(change.NewPosition);
                var lineCountDelta = change.LineCountDelta;
                if (lineCountDelta < 0) {
                    _lineInfoList.RemoveRange(lineNumber, -lineCountDelta);
                    _lineInfoList[lineNumber].IsDirty = true;
                }
                else {
                    _lineInfoList[lineNumber].IsDirty = true;
                    if (lineCountDelta == 0) { }
                    else if (lineCountDelta == 1) _lineInfoList.Insert(lineNumber, new LineInfo(true));
                    else {
                        for (var i = 0; i < lineCountDelta; i++)
                            _lineInfoList.Insert(lineNumber + i, new LineInfo(true));
                    }
                }
            }
        }
        private sealed class LineInfo {
            internal LineInfo(bool isDirty) { IsDirty = isDirty; }
            internal bool IsDirty;
            internal string Text;
            internal readonly List<Token> TokenList = new List<Token>();
            internal LineStates States;
            internal bool IsStatesOn(LineStates states) { return (States & states) != 0; }
            internal void AddStates(LineStates states) { States |= states; }
            internal bool NeedScanNextLine { get { return States != 0; } }
            internal void GetClassificationSpans(List<ClassificationSpan> classificationSpanList, ITextSnapshot snapshot, int startPosition) {
                foreach (var token in TokenList)
                    classificationSpanList.Add(new ClassificationSpan(new SnapshotSpan(snapshot, startPosition + token.StartIndex, token.Length), token.ClassificationType));
            }
        }
        [Flags]
        private enum LineStates {
            None = 0,
            InDelimitedComment = 0x0001,
            InVerbatimStringLiteral = 0x0002,
            //LastIsTypeKeyword = 0x0004,
        }
        private enum TokenKind { DelimitedComment, SingleLineComment, VerbatimStringLiteral, StringLiteral, CharacterLiteral, Identifier }
        private struct Token {
            internal Token(TokenKind kind, int startIndex, int endIndex, IClassificationType classificationType) {
                Kind = kind;
                StartIndex = startIndex;
                EndIndex = endIndex;
                ClassificationType = classificationType;
            }
            internal readonly TokenKind Kind;
            internal readonly int StartIndex;
            internal readonly int EndIndex;
            internal int Length { get { return EndIndex - StartIndex + 1; } }
            internal readonly IClassificationType ClassificationType;
        }
        private enum CharState { None, InDelimitedComment, InVerbatimStringLiteral, InSingleLineComment, InStringLiteral, InCharacterLiteral, InIdentifier }
        private void AddToken(LineInfo lineInfo, string text, int startIndex, int endIndex, ref CharState charState/*, ref bool lastIsTypeKeyword*/) {
            TokenKind kind;
            switch (charState) {
                case CharState.InDelimitedComment: kind = TokenKind.DelimitedComment; break;
                case CharState.InSingleLineComment: kind = TokenKind.SingleLineComment; break;
                case CharState.InVerbatimStringLiteral: kind = TokenKind.VerbatimStringLiteral; break;
                case CharState.InStringLiteral: kind = TokenKind.StringLiteral; break;
                case CharState.InCharacterLiteral: kind = TokenKind.CharacterLiteral; break;
                case CharState.InIdentifier: kind = TokenKind.Identifier; break;
                default: throw new InvalidOperationException();
            }
            charState = CharState.None;
            IClassificationType classificationType = null;
            switch (kind) {
                case TokenKind.DelimitedComment:
                case TokenKind.SingleLineComment:
                    classificationType = _commentType;
                    break;
                case TokenKind.VerbatimStringLiteral:
                case TokenKind.StringLiteral:
                    classificationType = _stringLiteralType;
                    break;
                case TokenKind.CharacterLiteral:
                    classificationType = _stringLiteralType;//_characterLiteralType;
                    break;
                case TokenKind.Identifier:
                    var tokenText = text.Substring(startIndex, endIndex - startIndex + 1);
                    if (_keywordSet.Contains(tokenText)) {
                        classificationType = _keywordType;
                        //if (tokenText == ETokens.DollarEntityKeywordText || tokenText == ETokens.DollarStructKeywordText || tokenText == ETokens.DollarEnumKeywordText
                        //    || tokenText == ETokens.DollarContextKeywordText || tokenText == ETokens.DollarFunctionKeywordText)
                        //    lastIsTypeKeyword = true;
                    }
                    else if (_csKeywordSet.Contains(tokenText)) {
                        classificationType = _keywordType;
                        //if (tokenText == "class" || tokenText == "struct" || tokenText == "interface" || tokenText == "enum")
                        //    lastIsTypeKeyword = true;
                    }
                    //else if (lastIsTypeKeyword) {
                    //    classificationType = _typeNameType;
                    //    lastIsTypeKeyword = false;
                    //}
                    break;
            }
            if (classificationType != null) lineInfo.TokenList.Add(new Token(kind, startIndex, endIndex, classificationType));
        }
        private static bool IsIdentifierStartChar(char ch) { return ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch == '_'; }
        private static bool IsIdentifierChar(char ch) { return IsIdentifierStartChar(ch) || ch >= '0' && ch <= '9'; }
        private static bool IsNewLineChar(char ch) { return ch == '\r' || ch == '\n'; }
        private static void ProcessChar(string text, int length, char ch, ref int idx, ref CharState charState, ref int startIndex) {
            charState = CharState.None;
            if (IsIdentifierStartChar(ch)) {
                charState = CharState.InIdentifier;
                startIndex = idx;
            }
            else if (ch == '"') {
                charState = CharState.InStringLiteral;
                startIndex = idx;
            }
            else if (ch == '/') {
                if (idx + 1 < length) {
                    var nextch = text[idx + 1];
                    if (nextch == '/') {
                        charState = CharState.InSingleLineComment;
                        startIndex = idx;
                        idx++;
                    }
                    else if (nextch == '*') {
                        charState = CharState.InDelimitedComment;
                        startIndex = idx;
                        idx++;
                    }
                }
            }
            //else if (ch == '$') {
            //    if (idx + 1 < length) {
            //        var nextch = text[idx + 1];
            //        if (IsIdentifierStartChar(nextch)) {
            //            charState = CharState.InIdentifier;
            //            startIndex = idx;
            //            idx++;
            //        }
            //    }
            //}
            else if (ch == '@') {
                if (idx + 1 < length) {
                    var nextch = text[idx + 1];
                    if (nextch == '"') {
                        charState = CharState.InVerbatimStringLiteral;
                        startIndex = idx;
                        idx++;
                    }
                    else if (IsIdentifierStartChar(nextch)) {
                        charState = CharState.InIdentifier;
                        startIndex = idx;
                        idx++;
                    }
                }
            }
            else if (ch == '\'') {
                charState = CharState.InCharacterLiteral;
                startIndex = idx;
            }
        }
        private void ProcessLine(ITextSnapshot snapshot, int lineNumber, int startPosition, string text, bool onTextChanged = true) {
            var lineInfo = _lineInfoList[lineNumber];
            lineInfo.Text = text;
            var oldNeedScanNextLine = false;
            if (onTextChanged) {
                oldNeedScanNextLine = lineInfo.NeedScanNextLine;
                lineInfo.TokenList.Clear();
                lineInfo.IsDirty = false;
                lineInfo.States = LineStates.None;
            }
            var length = text.Length;
            if (length == 0) return;
            //
            var charState = CharState.None;
            //var lastIsTypeKeyword = false;
            if (lineNumber > 0) {
                var lastLineInfo = _lineInfoList[lineNumber - 1];
                if (lastLineInfo.IsStatesOn(LineStates.InDelimitedComment)) charState = CharState.InDelimitedComment;
                else if (lastLineInfo.IsStatesOn(LineStates.InVerbatimStringLiteral)) charState = CharState.InVerbatimStringLiteral;
                //if (lastLineInfo.IsStatesOn(LineStates.LastIsTypeKeyword)) lastIsTypeKeyword = true;
            }
            var startIndex = 0;
            for (var idx = 0; idx < length; idx++) {
                var ch = text[idx];
                if (charState == CharState.InDelimitedComment) {
                    if (ch == '*' && idx + 1 < length && text[idx + 1] == '/') {
                        idx++;
                        AddToken(lineInfo, text, startIndex, idx, ref charState/*, ref lastIsTypeKeyword*/);
                    }
                }
                else if (charState == CharState.InVerbatimStringLiteral) {
                    if (ch == '"' && idx + 1 < length) {
                        if (text[idx + 1] == '"') idx++;
                        else AddToken(lineInfo, text, startIndex, idx, ref charState/*, ref lastIsTypeKeyword*/);
                    }
                }
                else if (charState != CharState.None) {
                    if (IsNewLineChar(ch)) {
                        AddToken(lineInfo, text, startIndex, idx - 1, ref charState/*, ref lastIsTypeKeyword*/);
                        ProcessChar(text, length, ch, ref idx, ref charState, ref startIndex);
                    }
                    else {
                        if (charState == CharState.InStringLiteral) {
                            if (ch == '"' && text[idx - 1] != '\\')
                                AddToken(lineInfo, text, startIndex, idx, ref charState/*, ref lastIsTypeKeyword*/);
                        }
                        else if (charState == CharState.InCharacterLiteral) {
                            if (ch == '\'' && text[idx - 1] != '\\')
                                AddToken(lineInfo, text, startIndex, idx, ref charState/*, ref lastIsTypeKeyword*/);
                        }
                        else if (charState == CharState.InIdentifier) {
                            if (!IsIdentifierChar(ch)) {
                                AddToken(lineInfo, text, startIndex, idx - 1, ref charState/*, ref lastIsTypeKeyword*/);
                                ProcessChar(text, length, ch, ref idx, ref charState, ref startIndex);
                            }
                        }
                    }
                }
                else {
                    ProcessChar(text, length, ch, ref idx, ref charState, ref startIndex);
                }
            }
            if (charState == CharState.InDelimitedComment) lineInfo.AddStates(LineStates.InDelimitedComment);
            else if (charState == CharState.InVerbatimStringLiteral) lineInfo.AddStates(LineStates.InVerbatimStringLiteral);
            //if (lastIsTypeKeyword) lineInfo.AddStates(LineStates.LastIsTypeKeyword);
            if (charState != CharState.None) AddToken(lineInfo, text, startIndex, length - 1, ref charState/*, ref lastIsTypeKeyword*/);
            //
            if (onTextChanged) {
                var nextLineLength = 0;
                if ((lineInfo.NeedScanNextLine || oldNeedScanNextLine) && lineNumber < _lineInfoList.Count - 1)
                    nextLineLength = snapshot.GetLineFromLineNumber(lineNumber + 1).LengthIncludingLineBreak;
                if (nextLineLength > 0) {
                    _lineInfoList[lineNumber + 1].IsDirty = true;
                    if (ClassificationChanged != null)
                        ClassificationChanged(this, new ClassificationChangedEventArgs(new SnapshotSpan(snapshot, startPosition + length, nextLineLength)));
                }
            }
#if DumpClassifier
            var sb = new StringBuilder();
            sb.AppendFormat("===ProcessLine()=== Line:{0}, Text:'{1}', States:{2}\r\n\t", lineNumber, text, lineInfo.States);
            foreach (var token in lineInfo.TokenList)
                sb.AppendFormat("[Token:'{0}',Kind:{1},Classification:{2}], ", text.Substring(token.StartIndex, token.Length), token.Kind, token.ClassificationType.Classification);
            sb.AppendLine();
            Dump(sb.ToString());
#endif

        }
        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan snapshotSpan) {
            var classificationSpanList = new List<ClassificationSpan>();
            var snapshot = snapshotSpan.Snapshot;
            var endPosition = snapshotSpan.End.Position;
            var line = snapshotSpan.Start.GetContainingLine();
            int position;
            while (true) {
                var lineNumber = line.LineNumber;
#if DumpClassifier
                Dump("---GetClassificationSpans()--- line:{0}\r\n".InvariantFormat(lineNumber));
#endif
                position = line.Start.Position;
                var lineText = line.GetTextIncludingLineBreak();
                var lineInfo = _lineInfoList[lineNumber];
                if (lineInfo.IsDirty || lineText != lineInfo.Text) ProcessLine(snapshot, lineNumber, position, lineText);
                lineInfo.GetClassificationSpans(classificationSpanList, snapshot, position);
                if (position + lineText.Length >= endPosition) break;
                line = snapshot.GetLineFromLineNumber(lineNumber + 1);
            }
            return classificationSpanList;
        }
#if DumpClassifier
        private static void Dump(string str) {
            System.IO.File.AppendAllText(@"d:\classfierdump.txt", str);
        }
#endif
    }
    //quick & dirty
    internal abstract class LanguageErrorTaggerProviderBase : ITaggerProvider {
        protected LanguageErrorTaggerProviderBase(string errorStoreFileName, Func<string, BuildErrorStore> errorStoreLoader) {
            _errorStoreFileName = errorStoreFileName;
            _errorStoreLoader = errorStoreLoader;
        }
        private readonly string _errorStoreFileName;
        private readonly Func<string, BuildErrorStore> _errorStoreLoader;
        [Import]
        internal SVsServiceProvider ServiceProvider = null;
        private const string _prjKindCSharpProject = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        private static object _locker = new object();
        private sealed class ProjectData {
            internal ProjectData(FileSystemWatcher fileWatcher, BuildErrorStore errorStore) {
                FileWatcher = fileWatcher;
                ErrorStore = errorStore;
            }
            internal readonly FileSystemWatcher FileWatcher;
            internal BuildErrorStore ErrorStore;
            private Dictionary<string, LanguageErrorTagger> _taggers;//key:file path
            internal Dictionary<string, LanguageErrorTagger> Taggers { get { return _taggers ?? (_taggers = new Dictionary<string, LanguageErrorTagger>()); } }
        }
        private static readonly Dictionary<string, ProjectData> _projectDatas = new Dictionary<string, ProjectData>();//key: project path
        public ITagger<T> CreateTagger<T>(ITextBuffer textBuffer) where T : ITag {
            var dte = (DTE)ServiceProvider.GetService(typeof(DTE));
            foreach (Project proj in dte.Solution.Projects) {
                if (proj.Kind == _prjKindCSharpProject) {
                    var projPath = (string)proj.Properties.Item("FullPath").Value;
                    lock (_locker) {
                        if (!_projectDatas.ContainsKey(projPath)) {
                            var fileWatcher = new FileSystemWatcher(Path.Combine(projPath, "obj"), _errorStoreFileName);
                            fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                            fileWatcher.Changed += OnFileWatcherChanged;
                            _projectDatas.Add(projPath, new ProjectData(fileWatcher, _errorStoreLoader(Path.Combine(projPath, "obj", _errorStoreFileName))));
                            fileWatcher.EnableRaisingEvents = true;
                        }
                    }
                }
            }
            return (ITagger<T>)(ITagger<IErrorTag>)textBuffer.Properties.GetOrCreateSingletonProperty<LanguageErrorTagger>(() => new LanguageErrorTagger(textBuffer));
        }
        internal static void AddTagger(string filePath, LanguageErrorTagger tagger) {
            lock (_locker) {
                foreach (var pair in _projectDatas) {
                    if (filePath.StartsWith(pair.Key, StringComparison.OrdinalIgnoreCase)) {
                        var projectData = pair.Value;
                        projectData.Taggers[filePath] = tagger;
                        if (projectData.ErrorStore != null) {
                            BuildErrorUnit errorUnit;
                            if (projectData.ErrorStore.TryGetValue(filePath, out errorUnit) && errorUnit.LastWriteTime == File.GetLastWriteTime(filePath))
                                tagger.Set(errorUnit);
                        }
                        return;
                    }
                }
            }
        }
        private void OnFileWatcherChanged(object sender, FileSystemEventArgs e) {
            var errorStore = _errorStoreLoader(e.FullPath);
            if (errorStore != null) {
                lock (_locker) {
                    foreach (var projData in _projectDatas.Values) {
                        if (sender == projData.FileWatcher) {
                            projData.ErrorStore = errorStore;
                            foreach (var pair in projData.Taggers) {
                                BuildErrorUnit errorUnit;
                                if (errorStore.TryGetValue(pair.Key, out errorUnit))
                                    pair.Value.Set(errorUnit);
                                else pair.Value.Clear();
                            }
                            return;
                        }
                    }
                }
            }
        }
    }
    internal sealed class LanguageErrorTagger : SimpleTagger<IErrorTag> {
        internal LanguageErrorTagger(ITextBuffer textBuffer)
            : base(textBuffer) {
            _textBuffer = textBuffer;
            var textDocument = textBuffer.Properties.GetProperty<ITextDocument>(typeof(ITextDocument));
            LanguageErrorTaggerProviderBase.AddTagger(textDocument.FilePath, this);
        }
        private readonly ITextBuffer _textBuffer;
        internal void Clear() {
            using (Update()) RemoveTagSpans(_ => true);
        }
        internal void Set(BuildErrorUnit errorUnit) {
            var snapshot = _textBuffer.CurrentSnapshot;
            var length = snapshot.Length;
            using (Update()) {
                RemoveTagSpans(_ => true);
                foreach (var error in errorUnit.ErrorList) {
                    var sourceSpan = error.SourceSpan;
                    if (sourceSpan.EndIndex <= length)
                        CreateTagSpan(snapshot.CreateTrackingSpan(sourceSpan.StartIndex, sourceSpan.Length, SpanTrackingMode.EdgeExclusive),
                            new ErrorTag(error.IsError ? PredefinedErrorTypeNames.SyntaxError/*it's red*/ : PredefinedErrorTypeNames.CompilerError/*it's blue*/, error.Message));
                }
            }
        }
    }
}
