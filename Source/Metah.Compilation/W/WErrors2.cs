using System;

namespace Metah.Compilation.W {
    internal sealed class CompilationContext : CompilationContextBase {
        private CompilationContext() { }
        public static string GetErrorMessageFormat(int code) {
            var kind = (ErrorKind)code;
            switch (kind) {
                case ErrorKind.InvalidNamespaceName: return "Invalid namespace name '{0}'";
                case ErrorKind.DuplicateActivityVariableOrParameterName: return "Duplicate activity variable or parameter name '{0}'";
                case ErrorKind.InvalidActivityVariableOrParameterReference: return "Invalid activity variable or parameter reference '{0}'";
                case ErrorKind.ReferenceToActivityVariableOrParameterNotAllowed: return "Reference to activity variable or parameter not allowed";
                case ErrorKind.ActivityInvokeNotAllowed: return "Activity (delegate) invocation not allowed";
                case ErrorKind.ActivityInvokeCannotBeUsedInCSBlockStmOrLambdaExprEtc: return "Activity (delegate) invocation cannot be used in C# block statement, lambda expression, anonymous method or query expression body";
                case ErrorKind.InvalidActivityInvokeSyntax: return "Invalid activity invocation syntax. 'activityInstanceExpr.Invoke(...)' expected";
                case ErrorKind.InvalidActivityInvokeOutOrRefArg: return "Invalid activity invocation out or ref argument. Activity variable or parameter required";
                case ErrorKind.TypeVarNotAllowed: return "Type 'var' not allowed";
                //
                case ErrorKind.DuplicateFlowNodeName: return "Duplicate flow node name '{0}'";
                case ErrorKind.InvalidFlowNodeReference: return "Invalid flow node reference '{0}'";
                case ErrorKind.DuplicateStateMachineNodeName: return "Duplicate state machine node name '{0}'";
                case ErrorKind.InvalidStateMachineNodeReference: return "Invalid state machine node reference '{0}'";
                case ErrorKind.StartNodeCannotBeFinal: return "Start node cannot be final";
                case ErrorKind.StateMachineMustHaveOneCommonNode: return "State machine must have one common node";
                case ErrorKind.StateMachineMustHaveOneFinalNode: return "State machine must have one final node";
                case ErrorKind.TransitionConditionRequired: return "Transition condition required";
                //
                case ErrorKind.RethrowMustBeInCatch: return "Rethrow must be in catch";
                case ErrorKind.PersistCannotBeInNoPersist: return "Persist cannot be in no persist";
                case ErrorKind.InvalidCompensationTokenReference: return "Invalid compensation token reference '{0}'";
                //
                case ErrorKind.CannotFindRequest: return "Cannot find the request. Request-reply correlation required";
                case ErrorKind.RequestAlreadyReferencedByAnotherReply: return "The request already referenced by another reply";
                case ErrorKind.InvalidRequestCorrReference: return "Invalid request-reply correlation reference '{0}'";
                case ErrorKind.RequestWithoutReplyCannotSetRequestCorr: return "Request without reply cannot set request-reply correlation";
                //case ErrorKind.InvalidCallbackCorrHandleReference: return "Invalid callback correlation handle reference '{0}'";
                //case ErrorKind.InvalidContextCorrHandleReference: return "Invalid context correlation handle reference '{0}'";
                //case ErrorKind.InvalidContentCorrHandleReference: return "Invalid content correlation handle reference '{0}'";
                case ErrorKind.FirstMemberOfTransactedReceiveMustBeReceive: return "First member of transacted receive must be receive";
                //
                //
                default: throw new InvalidOperationException("Invalid W error kind: " + kind);
            }
        }
    }
}
