namespace Wud.Extensions.Http.DockerCompose.WebApi;

public static class FileUtils
{
    public static void Move(string sourceFile, string destFile, bool overwrite)
    {
        try
        {
            File.Move(sourceFile, destFile, overwrite);
        }
        catch (Exception)
        {
            var workedWithCopy = false;
            try
            {
                File.Copy(sourceFile, destFile, overwrite);
                File.Delete(sourceFile);
                workedWithCopy = true;
            }
            catch (Exception)
            {}
            if (!workedWithCopy) throw;
        }
    }
}
