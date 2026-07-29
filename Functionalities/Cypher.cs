using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace ProyectoIntegracion.Functionalities
{
    public class Cypher
    {
        private static byte[] _key = Encoding.ASCII.GetBytes("ahgsdahske9qwk");
        private static byte[] _iv = Encoding.ASCII.GetBytes("Devjoker7.37hAES");
        public static string Encrypt(string text)
        {
            text = text.Trim();
            byte[] inputBytes = Encoding.ASCII.GetBytes(text);
            byte[] encripted;
            RijndaelManaged cripto = new RijndaelManaged();
            using (MemoryStream ms = new MemoryStream(inputBytes.Length))
            {
                using (CryptoStream objCryptoStream = new CryptoStream(ms, cripto.CreateEncryptor(_key, _iv), CryptoStreamMode.Write))
                {
                    objCryptoStream.Write(inputBytes, 0, inputBytes.Length);
                    objCryptoStream.FlushFinalBlock();
                    objCryptoStream.Close();
                }
                encripted = ms.ToArray();
            }
            return Convert.ToBase64String(encripted);
        }

        public static string Decrypt(string encryptedText)
        {
            encryptedText = encryptedText.Trim();
            byte[] inputBytes = Convert.FromBase64String(encryptedText);
            byte[] resultBytes = new byte[inputBytes.Length];
            string text = String.Empty;
            RijndaelManaged cripto = new RijndaelManaged();
            using (MemoryStream ms = new MemoryStream(inputBytes))
            {
                using (CryptoStream objCryptoStream = new CryptoStream(ms, cripto.CreateDecryptor(_key, _iv), CryptoStreamMode.Read))
                {
                    using (StreamReader sr = new StreamReader(objCryptoStream, true))
                    {
                        text = sr.ReadToEnd();
                    }
                }
            }
            return text;
        }
    }
}