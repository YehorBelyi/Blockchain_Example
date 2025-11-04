    using Blockchain_Example1.Enums;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Globalization;

    namespace Blockchain_Example1.Models
    {
        public class Transaction
        {
            [Key]
            public int Id { get; set; }
            [Required]
            [MaxLength((int)Restrictions.MaxAddressChars)]
            public string FromAddress { get; set; } = string.Empty;
            [Required]
            [MaxLength((int)Restrictions.MaxAddressChars)]
            public string ToAddress { get; set; } = string.Empty;
            [Required]
            public decimal Amount { get; set; } = decimal.Zero;
            public decimal Fee { get; set; }
            [MaxLength((int)Restrictions.MaxKeyChars)]
            public string Signature { get; set; } = string.Empty;
            public string? Note {  get; set; }

            public int? BlockId { get; set; }

            [ForeignKey(nameof(BlockId))]
            public Block? Block { get; set; }

            public string CanonicalPayload()
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2:0.########}|{3:0.########}", 
                    FromAddress,ToAddress, Amount, Fee);
            }
        }
    }
