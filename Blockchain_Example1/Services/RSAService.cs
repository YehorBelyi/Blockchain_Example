using System.Security.Cryptography;
using System.Text;

namespace Blockchain_Example1.Services
{
    public class RSAService
    {
        public bool isGenerated = false;
        string PrivateKey { get; set; }
        string PublicKey { get; set; }
        RSA rsa;

        public RSAService()
        {
            rsa = RSA.Create();
        }
        public (string privateKey, string publicKey) GetRSAKeys()
        {
            if (!isGenerated)
            {
                PrivateKey = rsa.ToXmlString(true);
                PublicKey = rsa.ToXmlString(false);
                isGenerated = true;
                return (PrivateKey, PublicKey);
            }
            return (PrivateKey, PublicKey);
        }

    }
}
