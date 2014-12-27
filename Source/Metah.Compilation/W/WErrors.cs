
namespace Metah.Compilation.W {
    public enum ErrorKind {
        InvalidNamespaceName = Error.WStart + 100,
        DuplicateActivityVariableOrParameterName,
        InvalidActivityVariableOrParameterReference,
        ReferencingActivityVariableOrParameterNotAllowed,
        ActivityInvokeNotAllowed,
        ActivityInvokeCannotBeUsedInCSBlockStmOrLambdaExprEtc,
        InvalidActivityInvokeSyntax,
        InvalidActivityInvokeOutOrRefArg,
        TypeVarNotAllowed,
        //
        DuplicateFlowNodeName,
        InvalidFlowNodeReference,
        DuplicateStateMachineNodeName,
        InvalidStateMachineNodeReference,
        StartNodeCannotBeFinal,
        StateMachineMustHaveOneCommonNode,
        StateMachineMustHaveOneFinalNode,
        TransitionConditionRequired,
        //
        RethrowMustBeInCatch,
        PersistCannotBeInNoPersist,
        InvalidCompensationTokenReference,
        //
        CannotFindRequest,
        RequestAlreadyReferencedByAnotherReply,
        InvalidRequestCorrReference,
        RequestWithoutReplyCannotSetRequestCorr,
        //InvalidCallbackCorrHandleReference,
        //InvalidContextCorrHandleReference,
        //InvalidContentCorrHandleReference,
        FirstMemberOfTransactedReceiveMustBeReceive,
        //
    }
}
