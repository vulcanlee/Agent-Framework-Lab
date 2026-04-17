using System.Reflection;
using AiTopicPulse.Cli.Analysis;
using OpenAI.Responses;

namespace AiTopicPulse.Cli.Tests.Analysis;

public sealed class ResponseUpdateStatusFormatterTests
{
    [Fact]
    public void TryFormat_returns_human_readable_messages_for_code_interpreter_updates()
    {
        object started = CreateNonPublic<StreamingResponseCodeInterpreterCallInProgressUpdate>(1, 0, "item-1");
        object interpreting = CreateNonPublic<StreamingResponseCodeInterpreterCallInterpretingUpdate>(2, 0, "item-1");
        object completed = CreateNonPublic<StreamingResponseCodeInterpreterCallCompletedUpdate>(3, 0, "item-1");

        Assert.Equal("Code Interpreter started.", ResponseUpdateStatusFormatter.TryFormat(started));
        Assert.Equal("Code Interpreter is analyzing the dataset.", ResponseUpdateStatusFormatter.TryFormat(interpreting));
        Assert.Equal("Code Interpreter finished running.", ResponseUpdateStatusFormatter.TryFormat(completed));
    }

    [Fact]
    public void TryFormat_ignores_text_delta_updates()
    {
        object delta = CreateNonPublic<StreamingResponseOutputTextDeltaUpdate>(1, "item-1", 0, 0, "hello");

        Assert.Null(ResponseUpdateStatusFormatter.TryFormat(delta));
    }

    private static T CreateNonPublic<T>(params object[] arguments)
    {
        ConstructorInfo constructor = typeof(T).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(ctor => ctor.GetParameters().Length == arguments.Length);

        return (T)constructor.Invoke(arguments);
    }
}
