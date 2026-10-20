using System.ComponentModel;

namespace CondoAgents.Agents.Tools;

/// <summary>Arithmetic the model is told not to do itself; the result carries the inputs so the critic can check them against evidence.</summary>
public static class ReserveFundProjectionTool
{
    public static ReserveFundProjection Project(
        [Description("Current fund balance in dollars.")] decimal balance,
        [Description("Annual contribution in dollars for the first year.")] decimal annualContribution,
        [Description("Yearly indexation of the contribution, in percent, for example 3.")] decimal indexationPercent,
        [Description("Number of years to project.")] int years,
        [Description("Target balance the study recommends, in dollars.")] decimal target)
    {
        if (years is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(years), "Project between 1 and 50 years.");
        }

        var projected = balance;
        var contribution = annualContribution;
        for (var i = 0; i < years; i++)
        {
            projected += contribution;
            contribution *= 1 + indexationPercent / 100m;
        }

        projected = Math.Round(projected, 0);
        return new ReserveFundProjection(balance, annualContribution, indexationPercent, years, target, projected, Math.Round(projected - target, 0), projected >= target);
    }
}

public sealed record ReserveFundProjection(
    decimal Balance,
    decimal AnnualContribution,
    decimal IndexationPercent,
    int Years,
    decimal Target,
    decimal ProjectedBalance,
    decimal Shortfall,
    bool MeetsTarget);