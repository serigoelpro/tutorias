using System.IO;
using System.Web;

namespace Plataforma_Web.Models
{
    public class FileHelper
    {
        public static string UploadPhoto(HttpPostedFileBase file, string folder)
        {
            var path = string.Empty;
            var pic = string.Empty;


            if(file != null)
            {
                folder = "~/Imagenes";
                pic = Path.GetFileName(file.FileName);
                path = Path.Combine(HttpContext.Current.Server.MapPath(folder), pic);

                //Esto sube el archivo al servidor
                file.SaveAs(path);

                using (MemoryStream ms = new MemoryStream())
                {
                    file.InputStream.CopyTo(ms);
                    byte[] array = ms.GetBuffer();
                }
            }
            return pic;
        }
    }
}