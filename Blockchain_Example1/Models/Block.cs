using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Blockchain_Example1.Services;
using System.Diagnostics;

namespace Blockchain_Example1.Models
{
    // class for block
    public class Block
    {
        [Key]
        public int Index { get; set; }
        // this field is not needed anymore, since we have List<Transaction> Transactions
        //public string Data { get; set; } = string.Empty; // information that block contains

        public List<Transaction> Transactions { get; set;  } = new();
        public int TransactionsCount => Transactions.Count;
        public string PreviousHash { get; set; } = string.Empty; // genesis - first block which starts the chain
        public string Hash { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string? Signature { get; private set; } = string.Empty;// digital signature, private key
        public string? PublicKeyXml { get; private set; } = string.Empty;

        // Mining block, Proof of Work (POW)
        public int Nonce { get; set; } // amount of attempts to guess the hash
        public int Difficulty { get; private set; } // amount of zeros (0), algorithm difficulty
        public long MiningDurationMs { get; set; } // time taken to guess

        public bool IsMined { get; set; } = false;


        public Block(string previousHash) {
            PreviousHash = previousHash;
            Timestamp = DateTime.UtcNow.ToString("f");
            Hash = ComputeHash();
        }

        public void SetTransaction(List<Transaction> transactions)
        {
            transactions.Clear();
            transactions.AddRange(Transactions);
        }

        // Return info about all transactions in the list of specific block
        private string CanonicalizeTransactions()
        {
            var sb = new StringBuilder();
            foreach (var tx in Transactions)
            {
                sb.Append(tx.CanonicalPayload());
                sb.Append("|");
            }
            return sb.ToString();
        }

        // Method that creates hash based on data inside the block
        public string ComputeHash()
        {
            // [27.10.2025] Added to hash these parameters: Nonce and difficulty
            // [31.10.2025] Added to hash Transactions list instead of data
            var raw = PreviousHash + Timestamp + Nonce + Difficulty + CanonicalizeTransactions(); // constructing info about specific block
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return BitConverter.ToString(bytes).Replace("-", "");
            }
        }


        //// RSA: function to sign block with RSA algorithm
        //public void Sign(RSAParameters privateKey, string publicKey)
        //{
        //    var rsa = RSA.Create();
        //    rsa.ImportParameters(privateKey);
        //    byte[] data = Encoding.UTF8.GetBytes(Hash);
        //    // Pkcs1 - a template to create private key based on block hash
        //    // using hash to generate private key
        //    byte[] sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        //    // Generating signature
        //    Signature = Convert.ToBase64String(sig);
        //    PublicKeyXml = publicKey;
        //}

        // RSA: function to sign block with RSA algorithm
        public void Sign(string privateKeyXml, string publicKey)
        {
            var rsa = RSA.Create();
            rsa.FromXmlString(privateKeyXml);
            byte[] data = Encoding.UTF8.GetBytes(Hash);
            // Pkcs1 - a template to create private key based on block hash
            // using hash to generate private key
            byte[] sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            // Generating signature
            Signature = Convert.ToBase64String(sig);
            PublicKeyXml = publicKey;
        }

        // RSA: verify
        public bool Verify()
        {
            // If signature is empty - return false
            if (String.IsNullOrWhiteSpace(Signature)) return false;

            try
            {
                var rsa = RSA.Create();
                rsa.FromXmlString(PublicKeyXml);
                // reading starting hash
                byte[] data = Encoding.UTF8.GetBytes(Hash);
                // generating signature back to byte array
                byte[] sign = Convert.FromBase64String(Signature);
                // checking whether this block was signed with 'sign'
                return rsa.VerifyData(data, sign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            } catch {
                return false;
            }
        }

        public void SetSignature(string signature) => Signature = signature;

        // Synchronious mining
        public void Mine(int difficulty)
        {
            Difficulty = difficulty;
            // Creating string that contains this amount of zeros, BUT THAT STILL IS NOT HASH!
            string target = new string('0', Difficulty);

            // Starting timer
            var sw = Stopwatch.StartNew();
            // Trying to guess the hash untill we guess the right one
            do
            {
                Nonce++;
                Hash = ComputeHash();
            } while (!Hash.StartsWith(target, StringComparison.Ordinal));
            // Stopping timer
            sw.Stop();
            // Get time taken to guess
            MiningDurationMs = sw.ElapsedMilliseconds;
        }

        // Asynchronius mining
        public async Task MineAsync(int difficulty)
        {
            await Task.Run(() =>
            {
                Difficulty = difficulty;
                // Creating string that contains this amount of zeros, BUT THAT STILL IS NOT HASH!
                string target = new string('0', Difficulty);

                // Starting timer
                var sw = Stopwatch.StartNew();
                // Trying to guess the hash untill we guess the right one
                do
                {
                    Nonce++;
                    Hash = ComputeHash();
                } while (!Hash.StartsWith(target, StringComparison.Ordinal));
                // Stopping timer
                sw.Stop();
                // Get time taken to guess
                MiningDurationMs = sw.ElapsedMilliseconds;
            });
        }

        // [Mining]: Check if guessed hash is valid
        public bool HashValidProof()
        {
            string target = new string('0', Difficulty);
            return Hash == ComputeHash() && Hash.StartsWith(target, StringComparison.Ordinal);
        }
    }
}
