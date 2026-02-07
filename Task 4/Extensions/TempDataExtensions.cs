using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Task_4.Extensions;

public static class TempDataExtensions
{
    public static void SetSuccessMessage(this ITempDataDictionary tempData, string message)
    {
        tempData["SuccessMessage"] = message;
    }

    public static void SetErrorMessage(this ITempDataDictionary tempData, string message)
    {
        tempData["ErrorMessage"] = message;
    }

    public static void SetInfoMessage(this ITempDataDictionary tempData, string message)
    {
        tempData["InfoMessage"] = message;
    }

    public static void SetWarningMessage(this ITempDataDictionary tempData, string message)
    {
        tempData["WarningMessage"] = message;
    }
}
