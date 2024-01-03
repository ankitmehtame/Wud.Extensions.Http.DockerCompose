using System.Text;

namespace Wud.Extensions.Http.DockerCompose.WebApi;

public static class ExceptionUtils
{
    public static string Cause(this Exception ex)
    {
        if (ex.InnerException == null) return ex.Message;
        var builder = new StringBuilder();
        builder.AppendLine(ex.Message);
        var curEx = ex;
        while (curEx.InnerException != null)
        {
            curEx = curEx.InnerException;
            if (string.IsNullOrWhiteSpace(curEx.Message)) continue;
            builder.AppendLine(curEx.Message);
        }
        return builder.ToString();
    }
}
