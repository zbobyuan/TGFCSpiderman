using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Security;

namespace taiyuanhitech.TGFCSpiderman.Configuration
{
    class AuthConfigElement : ConfigurationElement, IAuthConfig
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("公孙龙曰：白马非马");
        [ConfigurationProperty("former")]
        public string UserName
        {
            get { return (string)this["former"]; }
            set { this["former"] = value; }
        }

        [ConfigurationProperty("later")]
        public string Later
        {
            get { return (string)this["later"]; }
            set { this["later"] = value; }
        }

        public string Password
        {
            get
            {
                if (string.IsNullOrEmpty(Later))
                    return "";

                string encryptedStr = Later;
                try
                {
                    byte[] decryptedData = System.Security.Cryptography.ProtectedData.Unprotect(
                        Convert.FromBase64String(encryptedStr),
                        Entropy,
                        System.Security.Cryptography.DataProtectionScope.CurrentUser);
                    return Encoding.Unicode.GetString(decryptedData);
                }
                catch
                {
                    return "";
                }
            }
            set
            {
                byte[] encryptedData = System.Security.Cryptography.ProtectedData.Protect(
                        Encoding.Unicode.GetBytes(value),
                        Entropy,
                        System.Security.Cryptography.DataProtectionScope.CurrentUser);
                Later = Convert.ToBase64String(encryptedData);
            }
        }
    }
}
