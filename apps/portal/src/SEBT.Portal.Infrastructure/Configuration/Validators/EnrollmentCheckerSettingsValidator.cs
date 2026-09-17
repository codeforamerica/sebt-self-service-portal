using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration.Validators;

/// <summary>
/// Rejects income screening figures that would screen households against the wrong numbers.
///
/// This section hot-reloads from AWS AppConfig, and a reload this validator rejects fails every consumer
/// of the section, the checker's features endpoint included, until the value is corrected. So it only
/// rejects values that are wrong rather than unfinished. Banner copy is deliberately not checked: a blank
/// banner is not worth taking the features endpoint down.
/// </summary>
public class EnrollmentCheckerSettingsValidator : IValidateOptions<EnrollmentCheckerSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, EnrollmentCheckerSettings options)
    {
        var income = options.IncomeEligibility;
        var problems = new List<string>();

        if (income.BaseThreshold < 0)
        {
            problems.Add($"EnrollmentChecker:IncomeEligibility:BaseThreshold must not be negative (got {income.BaseThreshold}).");
        }

        if (income.PerMemberIncrement < 0)
        {
            problems.Add($"EnrollmentChecker:IncomeEligibility:PerMemberIncrement must not be negative (got {income.PerMemberIncrement}).");
        }

        if (income.MaxHouseholdSize < 0)
        {
            problems.Add($"EnrollmentChecker:IncomeEligibility:MaxHouseholdSize must not be negative (got {income.MaxHouseholdSize}).");
        }

        // With only one of the pair set, IncomeEligibilitySettings.IsConfigured is false and the checker
        // silently stops screening income.
        if ((income.BaseThreshold > 0) != (income.MaxHouseholdSize > 0))
        {
            problems.Add(
                "EnrollmentChecker:IncomeEligibility:BaseThreshold and MaxHouseholdSize must be set together; " +
                "with only one, the checker silently drops income screening.");
        }

        return problems.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(problems);
    }
}
