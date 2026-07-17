
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;
using SEBT.Portal.UseCases.Household;

public static class PluginTypeMappingExtensions
{
    extension(CaseRef)
    {
        public static CaseRef FromDto(CaseRefDto dto)
        {
            return new CaseRef
            {
                SummerEbtCaseId = dto.SummerEbtCaseId,
                ApplicationId = dto.ApplicationId,
                ApplicationStudentId = dto.ApplicationStudentId,
            };
        }
    }

    extension(CaseRefDto)
    {
        public static CaseRefDto FromCaseRef(CaseRef r) =>
            new CaseRefDto(r.SummerEbtCaseId, r.ApplicationId, r.ApplicationStudentId);
    }
}
