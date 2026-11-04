using UnityEngine;

namespace Training
{
    public static class TrainerStepBudget
    {
        public static long Estimate(TrainingRequest request, int rovers, int decisionPeriod, float fixedDeltaTime)
        {
            switch (request.IterationUnit)
            {
                case IterationUnit.Episodes:
                    return request.MaxIterations * DecisionsIn(request.EpisodeSeconds, decisionPeriod, fixedDeltaTime);
                case IterationUnit.Epochs:
                    return request.MaxIterations * DecisionsIn(request.EpochSeconds, decisionPeriod, fixedDeltaTime) * Mathf.Max(1, rovers);
                default:
                    return request.MaxIterations * 1000L;
            }
        }

        public static long DecisionsIn(float seconds, int decisionPeriod, float fixedDeltaTime)
        {
            double physicsSteps = seconds / Mathf.Max(1e-4f, fixedDeltaTime);
            return (long)System.Math.Ceiling(physicsSteps / Mathf.Max(1, decisionPeriod)) + 1;
        }
    }
}
