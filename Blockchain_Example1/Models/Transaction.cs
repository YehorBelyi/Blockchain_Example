﻿using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Blockchain_Example1.Models
{
    public class Transaction
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string FromAddress { get; set; } = string.Empty;
        [Required]
        public string ToAddress { get; set; } = string.Empty;
        [Required]
        public decimal Amount { get; set; } = decimal.Zero;
        public decimal Fee { get; set; }
        public string Signature { get; set; } = string.Empty;
        public string? Note {  get; set; }

        public string CanonicalPayload()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2:0.########}| {3:0.########}", 
                FromAddress,ToAddress, Amount, Fee);
        }
    }
}
