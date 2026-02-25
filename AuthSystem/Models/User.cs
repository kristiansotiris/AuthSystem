using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace AuthSystem.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = default!;

        [BsonElement("UserName")]
        [StringLength(50, MinimumLength = 3)]
        public string UserName { get; set; } = default!;

        [BsonElement("Email")]
        [Required]
        [EmailAddress]
        public string Email { get; set; } = default!;

        [BsonElement("googleId")]
        public string? GoogleId { get; set; }

        [BsonElement("EmailVerified")]
        public bool EmailVerified { get; set; }

        [BsonElement("VerificationCode")]
        public string? VerificationCode { get; set; }

        [BsonElement("VerificationToken")]
        public string? VerificationToken { get; set; }

        [BsonElement("PasswordResetToken")]
        public string? PasswordResetToken { get; set; }

        [BsonElement("PasswordResetTokenExpiresAt")]
        public DateTime? PasswordResetTokenExpiresAt { get; set; }

        [BsonElement("VerificationCodeExpiresAt")]
        public DateTime? VerificationCodeExpiresAt { get; set; }

        [BsonElement("PasswordResetAttempts")]
        public int PasswordResetAttempts { get; set; } = 0;

        [BsonElement("VerificationAttempts")]
        public int VerificationAttempts { get; set; } = 0;

        [BsonElement("LastPasswordResetSentAt")]
        public DateTime? LastPasswordResetSentAt { get; set; }

        [BsonElement("LastVerificationSentAt")]
        public DateTime? LastVerificationSentAt { get; set; }

        [BsonElement("PasswordHash")]
        [Required]
        [MinLength(8)]
        public string? PasswordHash { get; set; }

        [BsonElement("Role")]
        public Roles Role { get; set; } = Roles.User;

        [BsonElement("Status")]
        public Status Status { get; set; } = Status.Offline;

        [BsonElement("AccountStatus")]
        public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;

        [BsonElement("LastLoginAt")]
        public DateTime? LastLoginAt { get; set; }

        [BsonElement("PasswordChangedAt")]
        public DateTime? PasswordChangedAt { get; set; }

        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = default!;
    }
}
