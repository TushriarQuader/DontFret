namespace DontFret.Models;
/// <summary>
/// EmailVerificationCode class
/// All this does is store the email verification code and token for a user, along with expiration time and whether it's been used or not. 
/// This is used for email verification and password reset functionality.
/// </summary>
public class EmailVerificationCode
{
    public int Id { get; set; }// PK

    public string UserId { get; set; } = null!;// FK to User
    public User User { get; set; } = null!;// Actual User object (navigation property)

    public string Code { get; set; } = null!;// Code that is sent to the user's email for verification

    public string Token { get; set; } = null!;// 

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public int Attempts { get; set; }
}
