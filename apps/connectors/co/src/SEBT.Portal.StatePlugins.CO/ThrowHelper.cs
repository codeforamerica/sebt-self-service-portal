namespace SEBT.Portal.StatePlugins.CO;

internal static class ThrowHelper
{
    public static NotImplementedException CreateCoNotImplementedException()
    {
        return new NotImplementedException("This feature is not implemented for Colorado.");
    }
}
