using System.IO;

namespace AtsManager.Services
{
    public static class PdfLinkHelper
    {
        private const string PdfBasePath = @"C:\descargasSRI";

        public static string GetUrl(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return string.Empty;

            var relative = Path.GetRelativePath(PdfBasePath, fullPath);
            return "/descargas/" + relative.Replace('\\', '/');
        }
    }
}
