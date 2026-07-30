using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.EnrollmentCheck;

public class CheckEnrollmentCommand : ICommand<EnrollmentCheckOutcome>
{
    public required IList<ChildInput> Children { get; init; }
    public string? IpAddress { get; init; }

    public class ChildInput
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required DateOnly DateOfBirth { get; init; }
        public string? SchoolName { get; init; }
        public string? SchoolCode { get; init; }
    }
}
