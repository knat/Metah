//Handmade.cs
using System;
using System.Threading;
using System.Collections.Generic;
using System.Activities;
using System.Activities.Expressions;
using System.Activities.Statements;

namespace Handmade {
    public sealed class SequentialNumberGuess : Activity {
        public InArgument<int> MaxNumber { get; set; }
        public OutArgument<int> Turns { get; set; }
        private Activity GetImplementation() {
            var target = new Variable<int>();
            var guess = new Variable<int>();
            return new Sequence {
                Variables = { target, guess },
                Activities = {
                    new Assign<int> {
                        To = new OutArgument<int>(target),
                        Value = new InArgument<int>(ctx => new Random().Next(1, MaxNumber.Get(ctx) + 1))
                    },
                    new DoWhile {
                        Body = new Sequence {
                            Activities = {
                                new Prompt {
                                    BookmarkName = "EnterGuess",
                                    Text = new InArgument<string>(ctx =>
                                        "Please enter a number between 1 and " + MaxNumber.Get(ctx)),
                                    Result = new OutArgument<int>(guess)
                                },
                                new Assign<int> {
                                    To = new OutArgument<int>(ctx => Turns.Get(ctx)),
                                    Value = new InArgument<int>(ctx => Turns.Get(ctx) + 1)
                                },
                                new If {
                                    Condition = new InArgument<bool>(ctx => guess.Get(ctx) != target.Get(ctx)),
                                    Then = new If {
                                        Condition = new InArgument<bool>(ctx => guess.Get(ctx) < target.Get(ctx)),
                                        Then = new WriteLine { Text = "Your guess is too low."},
                                        Else = new WriteLine { Text = "Your guess is too high."}
                                    }
                                }
                            }
                        },
                        Condition = new LambdaValue<bool>(ctx => guess.Get(ctx) != target.Get(ctx))
                    }
                }
            };
        }
        private Func<Activity> _implementation;
        protected override Func<Activity> Implementation {
            get {
                return _implementation ?? (_implementation = GetImplementation);
            }
            set { throw new NotSupportedException(); }
        }
    }

    public sealed class Prompt : Activity<int> {
        public InArgument<string> BookmarkName { get; set; }
        public InArgument<string> Text { get; set; }
        private Activity GetImplementation() {
            return new Sequence {
                Activities = {
                    new WriteLine {
                        Text = new InArgument<string>(ctx => Text.Get(ctx))
                    },
                    new ReadInt {
                        BookmarkName = new InArgument<string>(ctx => BookmarkName.Get(ctx)),
                        Result = new OutArgument<int>(ctx => Result.Get(ctx))
                    }
                }
            };
        }
        private Func<Activity> _implementation;
        protected override Func<Activity> Implementation {
            get {
                return _implementation ?? (_implementation = GetImplementation);
            }
            set { throw new NotSupportedException(); }
        }
    }

    public sealed class ReadInt : NativeActivity<int> {
        public InArgument<string> BookmarkName { get; set; }
        protected override void Execute(NativeActivityContext context) {
            context.CreateBookmark(BookmarkName.Get(context), OnReadComplete);
        }
        protected override bool CanInduceIdle { get { return true; } }
        void OnReadComplete(NativeActivityContext context, Bookmark bookmark, object state) {
            Result.Set(context, (int)state);
        }
    }

    class Program {
        static void Main2() {
            var syncEvent = new AutoResetEvent(false);
            var idleEvent = new AutoResetEvent(false);
            var wfApp = new WorkflowApplication(new SequentialNumberGuess(),
                new Dictionary<string, object>() { { "MaxNumber", 100 } });
            wfApp.Completed = (WorkflowApplicationCompletedEventArgs e) => {
                Console.WriteLine("Congratulations, you guessed the number in {0} turns.", e.Outputs["Turns"]);
                syncEvent.Set();
            };
            wfApp.Aborted = (WorkflowApplicationAbortedEventArgs e) => {
                Console.WriteLine(e.Reason);
                syncEvent.Set();
            };
            wfApp.OnUnhandledException = (WorkflowApplicationUnhandledExceptionEventArgs e) => {
                Console.WriteLine(e.UnhandledException.ToString());
                return UnhandledExceptionAction.Terminate;
            };
            wfApp.Idle = (WorkflowApplicationIdleEventArgs e) => {
                idleEvent.Set();
            };
            wfApp.Run();
            var handles = new WaitHandle[] { syncEvent, idleEvent };
            while (WaitHandle.WaitAny(handles) == 1) {
                var isValidEntry = false;
                while (!isValidEntry) {
                    int guess;
                    if (!int.TryParse(Console.ReadLine(), out guess)) {
                        Console.WriteLine("Please enter an integer.");
                    }
                    else {
                        isValidEntry = true;
                        wfApp.ResumeBookmark("EnterGuess", guess);
                    }
                }
            }
        }
    }
}
