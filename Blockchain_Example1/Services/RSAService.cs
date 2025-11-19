using System.Security.Cryptography;
using System.Text;

namespace Blockchain_Example1.Services
{
    public class RSAService
    {
        public bool isGenerated = false;
        public bool isContractGenerated = false;
        string PrivateKey { get; set; }
        string PublicKey { get; set; }
        string ContractPrivateKey {  get; set; }
        string ContractPublicKey { get; set; }
        RSA rsa;
        RSA rsa2;

        public RSAService()
        {
            rsa = RSA.Create();
            rsa2 = RSA.Create();
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

        public (string privateKey, string publicKey) GetContractRSAKeys()
        {
            if (!isContractGenerated)
            {
                ContractPrivateKey = rsa2.ToXmlString(true);
                ContractPublicKey = rsa2.ToXmlString(false);
                isContractGenerated = true;
                return (ContractPrivateKey, ContractPublicKey);
            }
            return (ContractPrivateKey, ContractPublicKey);
        }
    }
}
