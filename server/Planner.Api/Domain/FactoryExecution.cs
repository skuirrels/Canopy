namespace Planner.Api.Domain;

public sealed record FactoryScheduledStep(FactoryStage Stage, FactoryStep Step, FactoryStageRun StageRun, FactoryStepRun StepRun);

public static class FactoryExecution
{
    // Display groups/positions have no scheduling meaning. Ready stages execute
    // serially; each stage's explicit step order is retained.
    public static FactoryScheduledStep? Next(FactoryRun run, IReadOnlyList<FactoryStageRun> stages, IReadOnlyList<FactoryStepRun> steps)
    {
        var stageById = stages.ToDictionary(x => x.StageId);
        var stepById = steps.ToDictionary(x => (x.StageRunId, x.StepId));
        foreach (var stage in run.Snapshot.Environment.Definition.Stages)
        {
            var current = stageById[stage.Id];
            if (current.State is not ("pending" or "running") || stage.DependsOn.Any(id => stageById[id].State != "succeeded")) continue;
            foreach (var step in stage.Steps)
            {
                var attempt = stepById[(current.Id, step.Id)];
                if (attempt.State != "succeeded") return new(stage, step, current, attempt);
            }
        }
        return null;
    }
}
