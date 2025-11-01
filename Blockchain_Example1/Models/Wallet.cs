using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace Blockchain_Example1.Models
{
    public class Wallet
    {
        [Key]
        public int Id { get; set; }
        public string Address { get; set; } = string.Empty;
        public string PublicKeyXml { get; set; } = string.Empty;
        public string DisplayName {  get; set; } = string.Empty;
        public DateTime JoinedOn { get; set; } = DateTime.UtcNow;

        // Generate Wallet adress based on public key
        public static string DereveAddressFromPublicKeyXml(string publicKeyXml)
        {
            var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(publicKeyXml));
            var hex20 = BitConverter.ToString(hash, 0, 20).Replace("-", "");
            return "ADDR_" + hex20;
        }

    }
}
