using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class StateMachineTests
{
    [Fact]
    public void InitialStateIsCompact()
    {
        Assert.Equal(ActivityState.Compact, StateMachine.InitialState);
    }

    [Fact]
    public void OpenMovesOneStepAtATime()
    {
        Assert.Equal(ActivityState.Compact, StateMachine.Next(ActivityState.Minimal, StateTransition.Open));
        Assert.Equal(ActivityState.Expanded, StateMachine.Next(ActivityState.Compact, StateTransition.Open));
        Assert.Null(StateMachine.Next(ActivityState.Expanded, StateTransition.Open));
    }

    [Fact]
    public void CloseMovesOneStepAtATime()
    {
        Assert.Equal(ActivityState.Compact, StateMachine.Next(ActivityState.Expanded, StateTransition.Close));
        Assert.Equal(ActivityState.Minimal, StateMachine.Next(ActivityState.Compact, StateTransition.Close));
        Assert.Null(StateMachine.Next(ActivityState.Minimal, StateTransition.Close));
    }

    [Fact]
    public void AutoCollapseOnlyFromExpanded()
    {
        Assert.Equal(ActivityState.Compact, StateMachine.Next(ActivityState.Expanded, StateTransition.AutoCollapse));
        Assert.Null(StateMachine.Next(ActivityState.Compact, StateTransition.AutoCollapse));
        Assert.Null(StateMachine.Next(ActivityState.Minimal, StateTransition.AutoCollapse));
    }

    [Fact]
    public void StepsSplitJumpIntoSingleSteps()
    {
        Assert.Equal(new[] { ActivityState.Compact, ActivityState.Expanded }, StateMachine.Steps(ActivityState.Minimal, ActivityState.Expanded));
        Assert.Equal(new[] { ActivityState.Compact, ActivityState.Minimal }, StateMachine.Steps(ActivityState.Expanded, ActivityState.Minimal));
        Assert.Empty(StateMachine.Steps(ActivityState.Compact, ActivityState.Compact));
    }

    [Fact]
    public void AutoCollapseDelayIsFourSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(4), StateMachine.AutoCollapseDelay);
    }
}
