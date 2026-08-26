using Vokasia.Domain.Common;

namespace Vokasia.Api.Validation;

public static class TeacherMonitoringValidators
{
    public static bool TryParseStatus(string? value, out LearningRecordMonitoringStatus status) =>
        Enum.TryParse(value, ignoreCase: true, out status) && Enum.IsDefined(status);

    public static bool TryParseVisibility(string? value, out LearningRecordMonitoringVisibility visibility) =>
        Enum.TryParse(value, ignoreCase: true, out visibility) && Enum.IsDefined(visibility);

    public static string? Validate(LearningRecordMonitoringStatus status, string? note, string? followUpContext)
    {
        try
        {
            LearningRecordRules.ValidateMonitoringNote(status, note);
        }
        catch (ArgumentException)
        {
            return "Status perlu perhatian atau masalah wajib memiliki alasan.";
        }

        if (note?.Length > 2000)
        {
            return "Catatan monitoring maksimal 2000 karakter.";
        }

        if (followUpContext?.Length > 1000)
        {
            return "Konteks tindak lanjut maksimal 1000 karakter.";
        }

        return null;
    }
}
